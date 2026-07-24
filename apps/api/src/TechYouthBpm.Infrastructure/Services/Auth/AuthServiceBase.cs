using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

internal abstract class AuthServiceBase
{
    protected const int FallbackSessionDurationMinutes = 1;
    protected const int DefaultMaxFailedLoginAttempts = 5;
    protected const int DefaultLockoutMinutes = 10;
    protected const int DefaultEmailVerificationMinutes = 1440;
    protected const int DefaultEmailVerificationResendCooldownMinutes = 5;
    protected const int DefaultRefreshTokenDurationMinutes = 43200;
    protected const int DefaultPasswordResetMinutes = 30;
    protected const string GenericForgotPasswordMessage = "If the account exists, a password reset email was sent.";

    protected readonly AppDbContext db;
    protected readonly IConfiguration configuration;
    protected readonly ISystemAuditService auditService;
    protected readonly IOtpService otpService;
    protected readonly IEmailSender emailSender;
    protected readonly ISessionValidationCache sessionCache;

    protected AuthServiceBase(
        AppDbContext db,
        IConfiguration configuration,
        ISystemAuditService auditService,
        IOtpService otpService,
        IEmailSender emailSender,
        ISessionValidationCache? sessionCache = null)
    {
        this.db = db;
        this.configuration = configuration;
        this.auditService = auditService;
        this.otpService = otpService;
        this.emailSender = emailSender;
        this.sessionCache = sessionCache ?? NullSessionValidationCache.Instance;
    }

    protected static bool PasswordMatches(string password, string storedPassword) =>
        PasswordHasher.IsHashed(storedPassword)
            ? PasswordHasher.Verify(password, storedPassword)
            : string.Equals(password, storedPassword, StringComparison.Ordinal);

    protected int GetSessionDurationMinutes(bool rememberMe)
    {
        var configuredDuration = rememberMe
            ? configuration["Auth:RememberMeDurationMinutes"]
            : configuration["Auth:SessionDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : FallbackSessionDurationMinutes;
    }

    protected int GetRefreshTokenDurationMinutes()
    {
        var configuredDuration = configuration["Auth:RefreshTokenDurationMinutes"]
            ?? configuration["Auth:RememberMeDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : DefaultRefreshTokenDurationMinutes;
    }

    protected int GetInt(string key, int fallback)
    {
        var configuredValue = configuration[key];
        return int.TryParse(configuredValue, out var value) && value > 0 ? value : fallback;
    }

    protected static List<string> ValidateProfile(string displayName, string email)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("A valid email is required.");
        }

        return errors;
    }

    protected static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    protected static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        const string all = lower + upper + digits + symbols;

        var characters = new List<char>
        {
            RandomChar(upper),
            RandomChar(lower),
            RandomChar(digits),
            RandomChar(symbols)
        };

        for (var index = characters.Count; index < 14; index += 1)
        {
            characters.Add(RandomChar(all));
        }

        for (var index = characters.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = System.Security.Cryptography.RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters.ToArray());
    }

    protected static char RandomChar(string characters)
    {
        var index = System.Security.Cryptography.RandomNumberGenerator.GetInt32(characters.Length);
        return characters[index];
    }

    protected bool IsSandboxDelivery(User user)
    {
        return IsMailtrapSandboxMode()
            || (IsRoutingMode() && !IsPrimarySmtpAllowed(user) && IsSandboxSmtpConfigured());
    }

    protected bool IsMailtrapSandboxMode()
    {
        var provider = configuration["Email:Provider"] ?? "Demo";
        var host = configuration["Email:Smtp:Host"] ?? string.Empty;
        return provider.Equals("Mailtrap", StringComparison.OrdinalIgnoreCase)
            && host.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
    }

    protected bool IsRoutingMode()
    {
        var provider = configuration["Email:Provider"] ?? "Demo";
        return provider.Equals("Routing", StringComparison.OrdinalIgnoreCase);
    }

    protected bool IsSandboxSmtpConfigured()
    {
        return !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:Smtp:Host"]);
    }

    protected bool IsPrimarySmtpAllowed(User user)
    {
        var allowedRecipients = GetCsv("Email:AllowedRecipients");
        if (allowedRecipients.Count > 0
            && !allowedRecipients.Contains(user.Email.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var allowedUsernames = GetCsv("Email:AllowedUsernames");
        if (allowedUsernames.Count > 0
            && !allowedUsernames.Contains(user.Username.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    protected IReadOnlyList<string> GetCsv(string key)
    {
        var configuredValue = configuration[key];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return [];
        }

        return configuredValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    protected Task<User?> FindUserByUsernameOrEmailAsync(string value, CancellationToken cancellationToken)
    {
        var lookup = value.Trim().ToLowerInvariant();
        return UserQuery().SingleOrDefaultAsync(
            user => user.Username.ToLower() == lookup || user.Email.ToLower() == lookup,
            cancellationToken);
    }

    protected async Task<Result<EmailVerificationStartResponse>> StartEmailVerificationForUserAsync(
        User user,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (user.IsEmailVerified)
        {
            return Result<EmailVerificationStartResponse>.Failure("Email is already verified.");
        }

        var verificationMinutes = GetInt("Auth:EmailVerificationMinutes", DefaultEmailVerificationMinutes);
        var resendCooldownMinutes = GetInt(
            "Auth:EmailVerificationResendCooldownMinutes",
            DefaultEmailVerificationResendCooldownMinutes);
        if (user.EmailVerificationCodeExpiresAt is { } currentExpiry)
        {
            var lastIssuedAt = currentExpiry.AddMinutes(-verificationMinutes);
            if (lastIssuedAt.AddMinutes(resendCooldownMinutes) > DateTime.UtcNow)
            {
                return Result<EmailVerificationStartResponse>.Failure(
                    "Verification code was sent recently. Please wait before requesting another code.");
            }
        }

        var otp = otpService.IssueEmailVerificationCode(user, verificationMinutes);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "TechYouth BPM e-posta dogrulama kodu",
                    BuildEmailVerificationBody(
                        user.DisplayName,
                        otp.DemoCode,
                        otp.ExpiresAt,
                        verificationMinutes,
                        IsSandboxDelivery(user)),
                    user.Username,
                    true),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            return Result<EmailVerificationStartResponse>.Failure(
                exception.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase)
                    ? "Email recipient is not allowed for SMTP delivery."
                    : "Verification email could not be sent.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            actorUserId,
            "Auth.EmailVerificationRequested",
            "User",
            user.Id.ToString(),
            $"Email verification requested for '{user.Email}'.",
            cancellationToken);

        return Result<EmailVerificationStartResponse>.Success(new EmailVerificationStartResponse(
            emailSender.ExposesVerificationCode
                ? "Verification code generated for local demo."
                : "Verification code sent by email.",
            emailSender.ExposesVerificationCode ? otp.DemoCode : string.Empty,
            otp.ExpiresAt));
    }

    protected static string BuildEmailVerificationBody(
        string displayName,
        string code,
        DateTime expiresAt,
        int verificationMinutes,
        bool isSandboxMode)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeCode = WebUtility.HtmlEncode(code);
        var expiry = WebUtility.HtmlEncode(FormatTurkeyTime(expiresAt));
        var validity = WebUtility.HtmlEncode(FormatValidityDuration(verificationMinutes));
        var deliveryNote = isSandboxMode
            ? "Gelistirme ortaminda bu e-posta Mailtrap Sandbox inbox icinde goruntulenir; gercek alici inbox teslimati icin production mail provider gerekir."
            : "Bu e-posta yapilandirilmis SMTP saglayicisi uzerinden gercek alici inbox teslimati icin gonderilmistir.";
        var safeDeliveryNote = WebUtility.HtmlEncode(deliveryNote);

        return $"""
            <!doctype html>
            <html lang="tr">
            <body style="margin:0;padding:0;background:#f6f7fb;font-family:Arial,Helvetica,sans-serif;color:#18243a;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f6f7fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d8deea;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18243a;color:#ffffff;padding:22px 26px;">
                          <div style="font-size:13px;letter-spacing:.04em;text-transform:uppercase;color:#ffb06a;">TechYouth BPM</div>
                          <h1 style="margin:8px 0 0;font-size:24px;line-height:1.25;">E-posta dogrulama kodu</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:26px;">
                          <p style="margin:0 0 14px;font-size:15px;line-height:1.6;">Merhaba <strong>{safeName}</strong>,</p>
                          <p style="margin:0 0 18px;font-size:15px;line-height:1.6;">TechYouth BPM hesabinin e-posta dogrulamasi icin asagidaki kodu kullan.</p>
                          <div style="margin:20px 0;padding:18px 20px;border-radius:10px;background:#fff0e3;border:1px solid #ffd1aa;text-align:center;">
                            <div style="font-size:12px;color:#647187;text-transform:uppercase;letter-spacing:.08em;">Dogrulama kodu</div>
                            <div style="margin-top:8px;font-size:34px;line-height:1;font-weight:800;letter-spacing:.18em;color:#d95f05;">{safeCode}</div>
                          </div>
                          <p style="margin:0 0 4px;font-size:14px;line-height:1.6;color:#647187;">Bu kod <strong>{validity}</strong> gecerlidir.</p>
                          <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#647187;">Son kullanim: <strong>{expiry}</strong></p>
                          <p style="margin:0 0 12px;font-size:13px;line-height:1.6;color:#647187;">{safeDeliveryNote}</p>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#647187;">Bu istegi sen baslatmadiysan e-postayi yok sayabilirsin.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    protected static string FormatValidityDuration(int totalMinutes)
    {
        if (totalMinutes > 0 && totalMinutes % 1440 == 0)
        {
            var days = totalMinutes / 1440;
            return days == 1 ? "24 saat" : $"{days} gun";
        }

        if (totalMinutes > 0 && totalMinutes % 60 == 0)
        {
            var hours = totalMinutes / 60;
            return $"{hours} saat";
        }

        return $"{Math.Max(1, totalMinutes)} dakika";
    }

    protected static string BuildTemporaryPasswordBody(string displayName, string username, string temporaryPassword)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);

        return $"""
            <!doctype html>
            <html lang="tr">
            <body style="margin:0;padding:0;background:#f6f7fb;font-family:Arial,Helvetica,sans-serif;color:#18243a;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f6f7fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d8deea;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18243a;color:#ffffff;padding:22px 26px;">
                          <div style="font-size:13px;letter-spacing:.04em;text-transform:uppercase;color:#ffb06a;">TechYouth BPM</div>
                          <h1 style="margin:8px 0 0;font-size:24px;line-height:1.25;">Gecici sifre bilgisi</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:26px;">
                          <p style="margin:0 0 14px;font-size:15px;line-height:1.6;">Merhaba <strong>{safeName}</strong>,</p>
                          <p style="margin:0 0 18px;font-size:15px;line-height:1.6;">TechYouth BPM hesabin bir yonetici tarafindan olusturuldu. Ilk giristen sonra sifreni degistirmen zorunludur.</p>
                          <div style="display:block;margin:18px 0;padding:16px 18px;border-radius:10px;background:#f0f3f8;border:1px solid #d8deea;">
                            <div style="font-size:13px;color:#647187;">Kullanici adi</div>
                            <div style="margin-top:4px;font-size:18px;font-weight:700;color:#18243a;">{safeUsername}</div>
                          </div>
                          <div style="display:block;margin:18px 0;padding:16px 18px;border-radius:10px;background:#fff0e3;border:1px solid #ffd1aa;">
                            <div style="font-size:13px;color:#647187;">Gecici sifre</div>
                            <div style="margin-top:6px;font-size:24px;font-weight:800;color:#d95f05;letter-spacing:.04em;">{safePassword}</div>
                          </div>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#647187;">Bu bilgileri beklemiyorsan sistem yoneticisiyle iletisime gec.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    protected static ForgotPasswordResponse GenericForgotPasswordResponse() =>
        new(GenericForgotPasswordMessage);

    protected string BuildPasswordResetUrl(User user, string resetToken)
    {
        var configuredBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var baseUrl = configuredBaseUrl.TrimEnd('/');
        return $"{baseUrl}/?auth=reset&usernameOrEmail={Uri.EscapeDataString(user.Username)}&token={Uri.EscapeDataString(resetToken)}";
    }

    protected static string BuildPasswordResetBody(
        string displayName,
        string username,
        string resetToken,
        string resetUrl,
        DateTime expiresAt)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var safeToken = WebUtility.HtmlEncode(resetToken);
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);
        var expiry = WebUtility.HtmlEncode(FormatTurkeyTime(expiresAt));

        return $"""
            <!doctype html>
            <html lang="tr">
            <body style="margin:0;padding:0;background:#f6f7fb;font-family:Arial,Helvetica,sans-serif;color:#18243a;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f6f7fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d8deea;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18243a;color:#ffffff;padding:22px 26px;">
                          <div style="font-size:13px;letter-spacing:.04em;text-transform:uppercase;color:#ffb06a;">TechYouth BPM</div>
                          <h1 style="margin:8px 0 0;font-size:24px;line-height:1.25;">Sifre sifirlama kodu</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:26px;">
                          <p style="margin:0 0 14px;font-size:15px;line-height:1.6;">Merhaba <strong>{safeName}</strong>,</p>
                          <p style="margin:0 0 18px;font-size:15px;line-height:1.6;"><strong>{safeUsername}</strong> kullanicisi icin sifre sifirlama istegi alindi.</p>
                          <p style="margin:0 0 18px;font-size:15px;line-height:1.6;">Sifreyi sifirlamak icin asagidaki butona tiklayabilirsin.</p>
                          <div style="margin:20px 0;text-align:center;">
                            <a href="{safeResetUrl}" style="display:inline-block;padding:13px 18px;border-radius:10px;background:#f26a21;color:#ffffff;text-decoration:none;font-weight:800;">Sifreyi sifirla</a>
                          </div>
                          <div style="margin:20px 0;padding:18px 20px;border-radius:10px;background:#fff0e3;border:1px solid #ffd1aa;text-align:center;">
                            <div style="font-size:12px;color:#647187;text-transform:uppercase;letter-spacing:.08em;">Sifre sifirlama token'i</div>
                            <div style="margin-top:8px;font-size:18px;line-height:1.4;font-weight:800;word-break:break-all;color:#d95f05;">{safeToken}</div>
                          </div>
                          <p style="margin:0 0 12px;font-size:13px;line-height:1.6;color:#647187;">Buton calismazsa token'i login ekranindaki sifre sifirlama alanina elle yapistirabilirsin.</p>
                          <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#647187;">Gecerlilik: <strong>{expiry}</strong></p>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#647187;">Bu istegi sen baslatmadiysan e-postayi yok sayabilir veya yoneticiye haber verebilirsin.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    protected static string FormatTurkeyTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var turkeyTimeZone = ResolveTurkeyTimeZone();
        var turkeyTime = TimeZoneInfo.ConvertTimeFromUtc(utcValue, turkeyTimeZone);
        return $"{turkeyTime:dd.MM.yyyy HH:mm} GMT+3";
    }

    protected static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
    }

    protected async Task RevokeAllSessionsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        sessionCache.InvalidateUser(userId);
        var sessions = await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAt = DateTime.UtcNow;
        }

        var refreshTokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
        }
    }

    protected async Task RevokeRefreshTokensForSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var refreshTokens = await db.RefreshTokens
            .Where(token => token.UserSessionId == sessionId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
        }
    }

    protected IQueryable<User> UserQuery() =>
        db.Users
            .AsSplitQuery()
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .Include(user => user.TeamMemberships)
            .ThenInclude(teamMembership => teamMembership.Team);

    protected async Task DeactivateTeamMembershipsIfCommunityChangedAsync(
        Guid userId,
        Guid? currentCommunityId,
        Guid targetCommunityId,
        CancellationToken cancellationToken)
    {
        if (currentCommunityId is null || currentCommunityId == targetCommunityId)
        {
            return;
        }

        var memberships = await db.TeamMemberships
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.IsActive = false;
            membership.IsLead = false;
            membership.UpdatedAt = DateTime.UtcNow;
        }
    }

    protected static bool HasActiveCommunityAccess(User user) =>
        user.Role == Role.SuperAdmin
        || user.CommunityMemberships.Count == 0
        || user.CommunityMemberships.Any(membership =>
            membership.IsActive
            && membership.CommunityRole is not null
            && (membership.Community?.IsActive == true
                || membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.CommunityManageAdmins)));

    protected bool CanManageUsers(UserDto currentUser, Guid? targetCommunityId)
    {
        if (currentUser.IsSuperAdmin())
        {
            return true;
        }

        if (!currentUser.HasPermission(PermissionNames.CommunityManageUsers))
        {
            return false;
        }

        return targetCommunityId is null || currentUser.CommunityId == targetCommunityId;
    }

    protected static bool CanManageCommunityUsers(UserDto currentUser, Guid communityId) =>
        currentUser.IsSuperAdmin()
        || (currentUser.CommunityId == communityId
            && currentUser.HasPermission(PermissionNames.CommunityManageUsers));

    protected async Task<Guid?> ResolveTargetCommunityIdAsync(
        UserDto currentUser,
        Guid? requestedCommunityId,
        CancellationToken cancellationToken)
    {
        var communityId = currentUser.IsSuperAdmin()
            ? requestedCommunityId ?? currentUser.CommunityId
            : currentUser.CommunityId;

        if (communityId is null)
        {
            return null;
        }

        var exists = await db.Communities.AnyAsync(community => community.Id == communityId && community.IsActive, cancellationToken);
        return exists ? communityId : null;
    }

    protected async Task<Guid?> ResolveTargetCommunityRoleIdAsync(
        Guid communityId,
        Guid? requestedCommunityRoleId,
        Role requestedPlatformRole,
        CancellationToken cancellationToken)
    {
        if (requestedCommunityRoleId is not null)
        {
            var exists = await db.CommunityRoles.AnyAsync(
                role => role.Id == requestedCommunityRoleId && role.CommunityId == communityId,
                cancellationToken);
            return exists ? requestedCommunityRoleId : null;
        }

        var templateKey = requestedPlatformRole == Role.SuperAdmin
            ? CommunityRoleTemplates.CommunityAdmin
            : CommunityRoleTemplates.Unassigned;

        return await db.CommunityRoles
            .Where(role => role.CommunityId == communityId && role.TemplateKey == templateKey)
            .Select(role => (Guid?)role.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected async Task<UserCommunityMembership?> BuildDefaultMembershipAsync(Role role, CancellationToken cancellationToken)
    {
        var communityId = await db.Communities
            .Where(community => community.IsActive)
            .OrderBy(community => community.Name == "Sportif Faaliyetler" ? 0 : 1)
            .ThenBy(community => community.Name)
            .Select(community => (Guid?)community.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (communityId is null)
        {
            return null;
        }

        var communityRoleId = await ResolveTargetCommunityRoleIdAsync(communityId.Value, null, role, cancellationToken);
        if (communityRoleId is null)
        {
            return null;
        }

        return new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId.Value,
            CommunityRoleId = communityRoleId.Value,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    protected async Task NotifyUserAsync(
        Guid userId,
        string type,
        string title,
        string message,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        var communityId = await db.UserCommunityMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .Select(membership => (Guid?)membership.CommunityId)
            .SingleOrDefaultAsync(cancellationToken);

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    protected async Task NotifyCommunityManagersAsync(
        Guid communityId,
        string type,
        string title,
        string message,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        var managerIds = await db.Users
            .Where(user => user.Status == UserStatus.Active
                && user.CommunityMemberships.Any(membership =>
                    membership.IsActive
                    && membership.CommunityId == communityId
                    && membership.CommunityRole != null
                    && membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.CommunityManageUsers)))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var managerId in managerIds)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                UserId = managerId,
                Type = type,
                Title = title,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (managerIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
