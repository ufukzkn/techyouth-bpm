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

public class AuthService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender) : IAuthService
{
    private const int FallbackSessionDurationMinutes = 1;
    private const int DefaultMaxFailedLoginAttempts = 5;
    private const int DefaultLockoutMinutes = 10;
    private const int DefaultEmailVerificationMinutes = 1440;
    private const int DefaultEmailVerificationResendCooldownMinutes = 5;
    private const int DefaultRefreshTokenDurationMinutes = 43200;
    private const int DefaultPasswordResetMinutes = 30;
    private const string GenericForgotPasswordMessage = "If the account exists, a password reset email was sent.";

    public AuthService(AppDbContext db, IConfiguration configuration)
        : this(db, configuration, new SystemAuditService(db), new OtpService(), new DemoEmailSender())
    {
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.CommunityCode))
        {
            errors.Add("Community code is required.");
        }

        if (errors.Count > 0)
        {
            return Result<RegisterResponse>.Failure(errors);
        }

        var communityCode = request.CommunityCode.Trim().ToUpperInvariant();
        var community = await db.Communities
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.InviteCode == communityCode && item.IsActive, cancellationToken);
        if (community is null)
        {
            return Result<RegisterResponse>.Failure("Community code is invalid.");
        }

        var exists = await db.Users.AnyAsync(
            user => user.Username == username || user.Email == email,
            cancellationToken);
        if (exists)
        {
            return Result<RegisterResponse>.Failure("Username or email is already registered.");
        }

        var unassignedRoleId = await db.CommunityRoles
            .Where(role => role.CommunityId == community.Id && role.TemplateKey == CommunityRoleTemplates.Unassigned)
            .Select(role => (Guid?)role.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (unassignedRoleId is null)
        {
            return Result<RegisterResponse>.Failure("Community role was not found.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Email = email,
            Password = PasswordHasher.Hash(request.Password),
            Role = Role.User,
            Status = UserStatus.PendingApproval,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };
        user.CommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            CommunityRoleId = unassignedRoleId.Value,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyCommunityManagersAsync(
            community.Id,
            "User.PendingApproval",
            "Yeni kayit onayi bekliyor",
            $"{user.DisplayName} kullanicisi {community.Name} topluluguna katilmak icin onay bekliyor.",
            "User",
            user.Id.ToString(),
            cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.RegisterRequested",
            "User",
            user.Id.ToString(),
            $"User '{user.Username}' registered and is waiting for admin approval.",
            cancellationToken);

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Username, user.Email, user.Status));
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery()
            .SingleOrDefaultAsync(item => item.Username == request.Username, cancellationToken);

        if (user is null)
        {
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure("Account is temporarily locked after multiple failed login attempts.");
        }

        if (!PasswordMatches(request.Password, user.Password))
        {
            user.FailedLoginCount += 1;

            if (user.FailedLoginCount >= GetInt("Auth:MaxFailedLoginAttempts", DefaultMaxFailedLoginAttempts))
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(GetInt("Auth:LockoutMinutes", DefaultLockoutMinutes));
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user.Id,
                user.LockedUntil is null ? "Auth.LoginFailed" : "Auth.AccountLocked",
                "User",
                user.Id.ToString(),
                user.LockedUntil is null
                    ? $"Failed login attempt for '{user.Username}'."
                    : $"User '{user.Username}' was temporarily locked after failed login attempts.",
                cancellationToken);
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        if (user.Status != UserStatus.Active)
        {
            return Result<LoginResponse>.Failure(user.Status == UserStatus.PendingApproval
                ? "Account is waiting for admin approval."
                : "Account is not active.");
        }

        if (!HasActiveCommunityAccess(user))
        {
            return Result<LoginResponse>.Failure("The user's community is not active.");
        }

        if (!PasswordHasher.IsHashed(user.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        var rawToken = SessionTokenHasher.CreateToken();
        var csrfToken = SessionTokenHasher.CreateToken();
        var rawRefreshToken = request.RememberMe ? SessionTokenHasher.CreateToken() : string.Empty;
        var refreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetRefreshTokenDurationMinutes());
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(rememberMe: false)),
            IpAddress = TrimOrNull(ipAddress, 128),
            UserAgent = TrimOrNull(userAgent, 512),
            RememberedDevice = request.RememberMe
        };

        db.UserSessions.Add(session);
        if (request.RememberMe)
        {
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = SessionTokenHasher.Hash(rawRefreshToken),
                UserId = user.Id,
                UserSessionId = session.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = refreshTokenExpiresAt,
                CreatedByIpAddress = TrimOrNull(ipAddress, 128),
                CreatedByUserAgent = TrimOrNull(userAgent, 512)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.LoginSucceeded",
            "Session",
            session.Id.ToString(),
            $"User '{user.Username}' signed in.",
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            rawToken,
            user.ToDto(),
            session.ExpiresAt,
            csrfToken,
            rawRefreshToken,
            request.RememberMe ? refreshTokenExpiresAt : null));
    }

    public async Task<Result<LoginResponse>> RefreshSessionAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponse>.Failure("A valid refresh token is required.");
        }

        var refreshTokenHash = SessionTokenHasher.Hash(refreshToken);
        var storedRefreshToken = await db.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(token => token.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .Include(token => token.UserSession)
            .SingleOrDefaultAsync(token => token.Token == refreshTokenHash, cancellationToken);

        if (storedRefreshToken is null)
        {
            return Result<LoginResponse>.Failure("A valid refresh token is required.");
        }

        if (storedRefreshToken.RevokedAt is not null)
        {
            await RevokeAllSessionsForUserAsync(storedRefreshToken.UserId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                storedRefreshToken.UserId,
                "Auth.RefreshReuseDetected",
                "Session",
                storedRefreshToken.UserSessionId.ToString(),
                "A revoked refresh token was reused; active sessions were revoked.",
                cancellationToken);
            return Result<LoginResponse>.Failure("Refresh session is no longer valid.");
        }

        if (storedRefreshToken.ExpiresAt <= DateTime.UtcNow
            || storedRefreshToken.User is null
            || storedRefreshToken.UserSession is null
            || storedRefreshToken.User.Status != UserStatus.Active
            || !HasActiveCommunityAccess(storedRefreshToken.User))
        {
            storedRefreshToken.RevokedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Result<LoginResponse>.Failure("Refresh session is no longer valid.");
        }

        var user = storedRefreshToken.User;
        var previousSession = storedRefreshToken.UserSession;
        previousSession.RevokedAt = DateTime.UtcNow;
        storedRefreshToken.RevokedAt = DateTime.UtcNow;

        var rawToken = SessionTokenHasher.CreateToken();
        var rawRefreshToken = SessionTokenHasher.CreateToken();
        var csrfToken = SessionTokenHasher.CreateToken();
        var refreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetRefreshTokenDurationMinutes());
        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(rememberMe: false)),
            IpAddress = TrimOrNull(ipAddress, 128),
            UserAgent = TrimOrNull(userAgent, 512),
            RememberedDevice = true
        };
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawRefreshToken),
            UserId = user.Id,
            UserSessionId = newSession.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshTokenExpiresAt,
            CreatedByIpAddress = TrimOrNull(ipAddress, 128),
            CreatedByUserAgent = TrimOrNull(userAgent, 512)
        };
        storedRefreshToken.ReplacedByRefreshTokenId = newRefreshToken.Id;
        db.UserSessions.Add(newSession);
        db.RefreshTokens.Add(newRefreshToken);

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.Refresh",
            "Session",
            newSession.Id.ToString(),
            $"Remembered session for '{user.Username}' was refreshed.",
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            rawToken,
            user.ToDto(),
            newSession.ExpiresAt,
            csrfToken,
            rawRefreshToken,
            refreshTokenExpiresAt));
    }

    public async Task<UserDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = SessionTokenHasher.Hash(token);
        var session = await db.UserSessions
            .Include(item => item.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(item => item.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .SingleOrDefaultAsync(
                item => item.Token == tokenHash && item.ExpiresAt > DateTime.UtcNow && item.RevokedAt == null,
                cancellationToken);

        if (session?.User is null
            || session.User.Status != UserStatus.Active
            || !HasActiveCommunityAccess(session.User))
        {
            if (session is not null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return null;
        }

        session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return session.User.ToDto();
    }

    public async Task<Result<UserDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var errors = ValidateProfile(displayName, email);
        if (errors.Count > 0)
        {
            return Result<UserDto>.Failure(errors);
        }

        var emailExists = await db.Users.AnyAsync(
            item => item.Id != user.Id && item.Email == email,
            cancellationToken);
        if (emailExists)
        {
            return Result<UserDto>.Failure("Email is already registered.");
        }

        var oldDisplayName = user.DisplayName;
        var oldEmail = user.Email;
        var emailChanged = !string.Equals(oldEmail, email, StringComparison.OrdinalIgnoreCase);

        user.DisplayName = displayName;
        user.Email = email;
        if (emailChanged)
        {
            user.IsEmailVerified = false;
            user.EmailVerificationCode = null;
            user.EmailVerificationCodeExpiresAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            emailChanged ? "User.ProfileAndEmailUpdated" : "User.ProfileUpdated",
            "User",
            user.Id.ToString(),
            emailChanged
                ? $"Profile changed for '{user.Username}': display name '{oldDisplayName}' -> '{user.DisplayName}', email '{oldEmail}' -> '{user.Email}'. Email verification was reset."
                : $"Profile changed for '{user.Username}': display name '{oldDisplayName}' -> '{user.DisplayName}'.",
            cancellationToken);

        return Result<UserDto>.Success(user.ToDto());
    }

    public async Task<Result<UserDto>> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        if (!PasswordMatches(request.CurrentPassword, user.Password))
        {
            return Result<UserDto>.Failure("Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result<UserDto>.Failure("Password must be at least 8 characters.");
        }

        var wasTemporary = user.MustChangePassword;
        user.Password = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            wasTemporary ? "Auth.TemporaryPasswordChanged" : "Auth.PasswordChanged",
            "User",
            user.Id.ToString(),
            wasTemporary
                ? $"User '{user.Username}' changed the temporary password required on first sign-in."
                : $"User '{user.Username}' changed their password.",
            cancellationToken);

        return Result<UserDto>.Success(user.ToDto());
    }

    public async Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user is null)
        {
            return Result<ForgotPasswordResponse>.Success(GenericForgotPasswordResponse());
        }

        var rawResetToken = SessionTokenHasher.CreateToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(GetInt("Auth:PasswordResetMinutes", DefaultPasswordResetMinutes));
        user.PasswordResetToken = SessionTokenHasher.Hash(rawResetToken);
        user.PasswordResetTokenExpiresAt = expiresAt;
        var resetUrl = BuildPasswordResetUrl(user, rawResetToken);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "TechYouth BPM sifre sifirlama kodu",
                    BuildPasswordResetBody(user.DisplayName, user.Username, rawResetToken, resetUrl, expiresAt),
                    user.Username,
                    true),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            return Result<ForgotPasswordResponse>.Success(GenericForgotPasswordResponse());
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.PasswordResetRequested",
            "User",
            user.Id.ToString(),
            $"Password reset was requested for '{user.Username}'.",
            cancellationToken);

        return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(
            GenericForgotPasswordMessage,
            emailSender.ExposesVerificationCode ? rawResetToken : string.Empty,
            expiresAt));
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user is null
            || string.IsNullOrWhiteSpace(user.PasswordResetToken)
            || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt <= DateTime.UtcNow
            || !string.Equals(
                SessionTokenHasher.Hash(request.Token.Trim()),
                user.PasswordResetToken,
                StringComparison.Ordinal))
        {
            return Result.Failure("Password reset token is invalid or expired.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Failure("Password must be at least 8 characters.");
        }

        user.Password = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await RevokeAllSessionsForUserAsync(user.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.PasswordResetCompleted",
            "User",
            user.Id.ToString(),
            $"Password reset was completed for '{user.Username}' and active sessions were revoked.",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AdminPasswordResetResponse>> ResetPasswordByAdminAsync(
        Guid userId,
        AdminPasswordResetRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<AdminPasswordResetResponse>.Failure("Only SuperAdmin users can reset user passwords.");
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<AdminPasswordResetResponse>.Failure("User not found.");
        }

        if (user.Role == Role.SuperAdmin && user.Id != currentUser.Id)
        {
            return Result<AdminPasswordResetResponse>.Failure("SuperAdmin passwords cannot be reset from management panel.");
        }

        var temporaryPassword = request.UseManualPassword
            ? (request.TemporaryPassword ?? string.Empty).Trim()
            : GenerateTemporaryPassword();
        if (temporaryPassword.Length < 8)
        {
            return Result<AdminPasswordResetResponse>.Failure("Password must be at least 8 characters.");
        }

        user.Password = PasswordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await RevokeAllSessionsForUserAsync(user.Id, cancellationToken);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "TechYouth BPM gecici sifre sifirlama",
                    BuildTemporaryPasswordBody(user.DisplayName, user.Username, temporaryPassword),
                    user.Username,
                    true),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            return Result<AdminPasswordResetResponse>.Failure("Temporary password email could not be sent.");
        }

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = "User.PasswordReset",
            Title = "Sifreniz sifirlandi",
            Message = "Gecici sifre e-posta adresinize gonderildi. Ilk giriste sifrenizi degistirmeniz gerekir.",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "User.PasswordResetByAdmin",
            "User",
            user.Id.ToString(),
            $"SuperAdmin '{currentUser.Username}' reset password for user '{user.Username}'.",
            cancellationToken);

        return Result<AdminPasswordResetResponse>.Success(new AdminPasswordResetResponse("Temporary password was sent by email."));
    }

    public async Task<Result<UserAdminDto>> CreateUserAsync(
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var platformRole = request.Role == Role.SuperAdmin ? Role.SuperAdmin : Role.User;
        if (platformRole == Role.SuperAdmin)
        {
            if (!currentUser.IsSuperAdmin())
            {
                return Result<UserAdminDto>.Failure("Only SuperAdmin users can create SuperAdmin accounts.");
            }

            if (request.Status != UserStatus.Active)
            {
                return Result<UserAdminDto>.Failure("SuperAdmin users must stay active.");
            }
        }

        if (!CanManageUsers(currentUser, request.CommunityId))
        {
            return Result<UserAdminDto>.Failure("Current user cannot create users in this community.");
        }

        var username = request.Username.Trim();
        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var errors = ValidateProfile(displayName, email);
        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add("Username is required.");
        }

        var temporaryPassword = string.IsNullOrWhiteSpace(request.TemporaryPassword)
            ? GenerateTemporaryPassword()
            : request.TemporaryPassword;

        if (temporaryPassword.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }

        if (errors.Count > 0)
        {
            return Result<UserAdminDto>.Failure(errors);
        }

        var exists = await db.Users.AnyAsync(
            user => user.Username == username || user.Email == email,
            cancellationToken);
        if (exists)
        {
            return Result<UserAdminDto>.Failure("Username or email is already registered.");
        }

        var hasCommunities = await db.Communities.AnyAsync(cancellationToken);
        var targetCommunityId = platformRole == Role.SuperAdmin
            ? null
            : await ResolveTargetCommunityIdAsync(currentUser, request.CommunityId, cancellationToken);
        if (targetCommunityId is null && hasCommunities && platformRole != Role.SuperAdmin)
        {
            return Result<UserAdminDto>.Failure("A community is required for the new user.");
        }

        var targetCommunityRoleId = targetCommunityId is null
            ? null
            : await ResolveTargetCommunityRoleIdAsync(
                targetCommunityId.Value,
                request.CommunityRoleId,
                platformRole,
                cancellationToken);
        if (targetCommunityRoleId is null && hasCommunities && platformRole != Role.SuperAdmin)
        {
            return Result<UserAdminDto>.Failure("Community role was not found.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Email = email,
            Password = PasswordHasher.Hash(temporaryPassword),
            Role = platformRole,
            Status = request.Status,
            IsEmailVerified = false,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };
        if (targetCommunityId is not null && targetCommunityRoleId is not null)
        {
            user.CommunityMemberships.Add(new UserCommunityMembership
            {
                Id = Guid.NewGuid(),
                CommunityId = targetCommunityId.Value,
                CommunityRoleId = targetCommunityRoleId.Value,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "TechYouth BPM gecici sifre bilgisi",
                    BuildTemporaryPasswordBody(user.DisplayName, user.Username, temporaryPassword),
                    user.Username,
                    true),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            return Result<UserAdminDto>.Failure("Temporary password email could not be sent.");
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "User.CreatedByAdmin",
            "User",
            user.Id.ToString(),
            $"Admin '{currentUser.Username}' created user '{user.Username}' with role {user.Role}, status {user.Status} and temporary-password requirement.",
            cancellationToken);

        var saved = await UserQuery().SingleAsync(item => item.Id == user.Id, cancellationToken);
        return Result<UserAdminDto>.Success(saved.ToAdminDto());
    }

    public async Task<Result> DeleteUserAsync(
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, null))
        {
            return Result.Failure("Community management permission is required to delete users.");
        }

        if (currentUser.Id == userId)
        {
            return Result.Failure("Admin users cannot delete their own account.");
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        if (user.Role == Role.SuperAdmin)
        {
            return Result.Failure("SuperAdmin users cannot be deleted.");
        }

        if (!CanManageUsers(currentUser, user.ToDto().CommunityId))
        {
            return Result.Failure("Current user cannot delete users in this community.");
        }

        var hasWorkflowHistory =
            await db.FormDefinitions.AnyAsync(
                form => form.CreatedByUserId == userId || form.UpdatedByUserId == userId,
                cancellationToken)
            || await db.ProcessInstances.AnyAsync(process => process.StartedByUserId == userId, cancellationToken)
            || await db.ProcessTasks.AnyAsync(task => task.CompletedByUserId == userId, cancellationToken)
            || await db.AuditLogs.AnyAsync(log => log.UserId == userId, cancellationToken);

        if (hasWorkflowHistory)
        {
            return Result.Failure("User has workflow history and cannot be deleted.");
        }

        var sessions = await db.UserSessions.Where(session => session.UserId == userId).ToListAsync(cancellationToken);
        db.UserSessions.RemoveRange(sessions);

        var actorLogs = await db.SystemAuditLogs
            .Where(log => log.ActorUserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var log in actorLogs)
        {
            log.ActorUserId = null;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "User.DeletedByAdmin",
            "User",
            userId.ToString(),
            $"Admin '{currentUser.Username}' deleted user '{user.Username}'.",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PagedResult<UserAdminDto>>> ListUsersAsync(
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, request.CommunityId))
        {
            return Result<PagedResult<UserAdminDto>>.Failure("Community management permission is required to list users.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = UserQuery();

        if (!currentUser.IsSuperAdmin())
        {
            query = query.Where(user => user.CommunityMemberships.Any(membership =>
                membership.IsActive && membership.CommunityId == currentUser.CommunityId));
        }

        if (request.CommunityId is not null)
        {
            query = query.Where(user => user.CommunityMemberships.Any(membership =>
                membership.IsActive && membership.CommunityId == request.CommunityId));
        }

        if (request.CommunityRoleId is not null)
        {
            query = query.Where(user => user.CommunityMemberships.Any(membership =>
                membership.IsActive && membership.CommunityRoleId == request.CommunityRoleId));
        }

        var requestedStatuses = request.Statuses?.Distinct().ToArray()
            ?? (request.Status is { } status ? [status] : []);
        if (requestedStatuses.Length > 0)
        {
            query = query.Where(user => requestedStatuses.Contains(user.Status));
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim().ToLowerInvariant();
            query = query.Where(user =>
                user.Username.ToLower().Contains(search)
                || user.DisplayName.ToLower().Contains(search)
                || user.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.Status)
            .ThenBy(user => user.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<UserAdminDto>>.Success(new PagedResult<UserAdminDto>(
            users.Select(user => user.ToAdminDto()).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<UserAdminDto>> UpdateUserAccessAsync(
        Guid userId,
        UpdateUserAccessRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, request.CommunityId))
        {
            return Result<UserAdminDto>.Failure("Community management permission is required to update user access.");
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure("User not found.");
        }

        var userDto = user.ToDto();
        if (!CanManageUsers(currentUser, userDto.CommunityId))
        {
            return Result<UserAdminDto>.Failure("Current user cannot update users in this community.");
        }

        if (user.Role == Role.SuperAdmin && request.Status != UserStatus.Active)
        {
            return Result<UserAdminDto>.Failure("SuperAdmin users must stay active.");
        }

        if (request.Role == Role.SuperAdmin && user.Role != Role.SuperAdmin)
        {
            return Result<UserAdminDto>.Failure("Existing users cannot be promoted to SuperAdmin.");
        }

        if (request.Role == Role.SuperAdmin && !currentUser.IsSuperAdmin())
        {
            return Result<UserAdminDto>.Failure("Only SuperAdmin users can assign SuperAdmin role.");
        }

        if (user.Role == Role.SuperAdmin && request.Role != Role.SuperAdmin)
        {
            return Result<UserAdminDto>.Failure("SuperAdmin users cannot be changed to a standard user.");
        }

        var oldStatus = user.Status;
        var oldRole = user.Role;
        var oldCommunityRoleName = userDto.CommunityRoleName;
        var updatedCommunityRoleName = oldCommunityRoleName;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.Role = user.Role == Role.SuperAdmin ? Role.SuperAdmin : Role.User;
        user.Status = request.Status;

        if (request.CommunityId is not null || request.CommunityRoleId is not null)
        {
            var targetCommunityId = await ResolveTargetCommunityIdAsync(currentUser, request.CommunityId ?? userDto.CommunityId, cancellationToken);
            if (targetCommunityId is null)
            {
                return Result<UserAdminDto>.Failure("A community is required.");
            }

            var targetCommunityRoleId = await ResolveTargetCommunityRoleIdAsync(
                targetCommunityId.Value,
                request.CommunityRoleId,
                user.Role,
                cancellationToken);
            if (targetCommunityRoleId is null)
            {
                return Result<UserAdminDto>.Failure("Community role was not found.");
            }
            updatedCommunityRoleName = await db.CommunityRoles
                .Where(role => role.Id == targetCommunityRoleId.Value)
                .Select(role => role.Name)
                .SingleAsync(cancellationToken);

            foreach (var membership in user.CommunityMemberships.Where(membership => membership.IsActive))
            {
                membership.IsActive = false;
            }

            await db.SaveChangesAsync(cancellationToken);
            db.UserCommunityMemberships.Add(new UserCommunityMembership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CommunityId = targetCommunityId.Value,
                CommunityRoleId = targetCommunityRoleId.Value,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (request.Status != UserStatus.Active)
        {
            await RevokeAllSessionsForUserAsync(user.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "User.AccessUpdated",
            "User",
            user.Id.ToString(),
            $"User '{user.Username}' access changed from {oldRole}/{oldStatus} to {user.Role}/{user.Status}.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (oldStatus != user.Status || !string.Equals(oldCommunityRoleName, updatedCommunityRoleName, StringComparison.Ordinal))
        {
            var accessMessage = !string.Equals(oldCommunityRoleName, updatedCommunityRoleName, StringComparison.Ordinal)
                ? $"Size {updatedCommunityRoleName ?? "Atanmadi"} topluluk rolu atandi."
                : $"Hesap durumunuz {user.Status} olarak guncellendi.";
            await NotifyUserAsync(
                user.Id,
                "User.AccessUpdated",
                "Yetki bilgileriniz guncellendi",
                accessMessage,
                "User",
                user.Id.ToString(),
                cancellationToken);
        }
        var updated = await UserQuery().SingleAsync(item => item.Id == user.Id, cancellationToken);
        return Result<UserAdminDto>.Success(updated.ToAdminDto());
    }

    public async Task<Result<IReadOnlyList<UserSessionDto>>> ListSessionsAsync(
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        var currentTokenHash = SessionTokenHasher.Hash(currentToken);
        var sessions = await db.UserSessions
            .Where(session => session.UserId == currentUser.Id && session.RevokedAt == null && session.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new UserSessionDto(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastSeenAt,
                session.Token == currentTokenHash,
                session.IpAddress,
                session.UserAgent,
                session.RememberedDevice))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserSessionDto>>.Success(sessions);
    }

    public async Task<Result<IReadOnlyList<UserSessionDto>>> ListUserSessionsAsync(
        Guid userId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, null))
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("Community management permission is required to view user sessions.");
        }

        var managedUser = await UserQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (managedUser is null)
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("User not found.");
        }

        if (!CanManageUsers(currentUser, managedUser.ToDto().CommunityId))
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("Current user cannot view sessions in this community.");
        }

        var currentTokenHash = SessionTokenHasher.Hash(currentToken);
        var sessions = await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(session => session.LastSeenAt ?? session.CreatedAt)
            .Select(session => new UserSessionDto(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastSeenAt,
                session.Token == currentTokenHash,
                session.IpAddress,
                session.UserAgent,
                session.RememberedDevice))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserSessionDto>>.Success(sessions);
    }

    public async Task<Result> LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure("A valid session token is required.");
        }

        var tokenHash = SessionTokenHasher.Hash(token);
        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Token == tokenHash && item.RevokedAt == null,
            cancellationToken);

        if (session is not null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                session.UserId,
                "Auth.Logout",
                "Session",
                session.Id.ToString(),
                "User session was revoked by logout.",
                cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeSessionAsync(
        Guid sessionId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == currentUser.Id && item.RevokedAt == null,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure("Session not found.");
        }

        session.RevokedAt = DateTime.UtcNow;
        await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.SessionRevoked",
            "Session",
            session.Id.ToString(),
            "User revoked an active session.",
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, null))
        {
            return Result.Failure("Community management permission is required to revoke user sessions.");
        }

        var managedUser = await UserQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (managedUser is null)
        {
            return Result.Failure("User not found.");
        }

        if (!CanManageUsers(currentUser, managedUser.ToDto().CommunityId))
        {
            return Result.Failure("Current user cannot revoke sessions in this community.");
        }

        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId && item.RevokedAt == null,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure("Session not found.");
        }

        session.RevokedAt = DateTime.UtcNow;
        await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.AdminSessionRevoked",
            "Session",
            session.Id.ToString(),
            $"Admin '{currentUser.Username}' revoked a session for user '{userId}'.",
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EmailVerificationStartResponse>> StartEmailVerificationAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<EmailVerificationStartResponse>.Failure("User not found.");
        }

        return await StartEmailVerificationForUserAsync(user, currentUser.Id, cancellationToken);
    }

    public async Task<Result<UserDto>> ConfirmEmailVerificationAsync(
        EmailVerificationConfirmRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        if (user.IsEmailVerified)
        {
            return Result<UserDto>.Success(user.ToDto());
        }

        var otpVerification = otpService.VerifyEmailVerificationCode(user, request.Code);
        if (!otpVerification.IsSuccess)
        {
            return Result<UserDto>.Failure(otpVerification.Errors);
        }

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.EmailVerified",
            "User",
            user.Id.ToString(),
            $"Email '{user.Email}' was verified.",
            cancellationToken);

        return Result<UserDto>.Success(user.ToDto());
    }

    public async Task<Result<EmailVerificationStartResponse>> StartPublicEmailVerificationAsync(
        PublicEmailVerificationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user is null)
        {
            return Result<EmailVerificationStartResponse>.Success(new EmailVerificationStartResponse(
                "Verification code sent if the account exists.",
                string.Empty,
                DateTime.UtcNow));
        }

        var result = await StartEmailVerificationForUserAsync(user, user.Id, cancellationToken);
        return result;
    }

    public async Task<Result<RegisterResponse>> ConfirmPublicEmailVerificationAsync(
        PublicEmailVerificationConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user is null)
        {
            return Result<RegisterResponse>.Failure("User not found.");
        }

        if (!user.IsEmailVerified)
        {
            var otpVerification = otpService.VerifyEmailVerificationCode(user, request.Code);
            if (!otpVerification.IsSuccess)
            {
                return Result<RegisterResponse>.Failure(otpVerification.Errors);
            }

            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationCodeExpiresAt = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        await auditService.LogAsync(
            user.Id,
            "Auth.EmailVerified",
            "User",
            user.Id.ToString(),
            $"Email '{user.Email}' was verified before sign-in.",
            cancellationToken);

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Username, user.Email, user.Status));
    }

    private static bool PasswordMatches(string password, string storedPassword) =>
        PasswordHasher.IsHashed(storedPassword)
            ? PasswordHasher.Verify(password, storedPassword)
            : string.Equals(password, storedPassword, StringComparison.Ordinal);

    private int GetSessionDurationMinutes(bool rememberMe)
    {
        var configuredDuration = rememberMe
            ? configuration["Auth:RememberMeDurationMinutes"]
            : configuration["Auth:SessionDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : FallbackSessionDurationMinutes;
    }

    private int GetRefreshTokenDurationMinutes()
    {
        var configuredDuration = configuration["Auth:RefreshTokenDurationMinutes"]
            ?? configuration["Auth:RememberMeDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : DefaultRefreshTokenDurationMinutes;
    }

    private int GetInt(string key, int fallback)
    {
        var configuredValue = configuration[key];
        return int.TryParse(configuredValue, out var value) && value > 0 ? value : fallback;
    }

    private static List<string> ValidateProfile(string displayName, string email)
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

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string GenerateTemporaryPassword()
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

    private static char RandomChar(string characters)
    {
        var index = System.Security.Cryptography.RandomNumberGenerator.GetInt32(characters.Length);
        return characters[index];
    }

    private bool IsSandboxDelivery(User user)
    {
        return IsMailtrapSandboxMode()
            || (IsRoutingMode() && !IsPrimarySmtpAllowed(user) && IsSandboxSmtpConfigured());
    }

    private bool IsMailtrapSandboxMode()
    {
        var provider = configuration["Email:Provider"] ?? "Demo";
        var host = configuration["Email:Smtp:Host"] ?? string.Empty;
        return provider.Equals("Mailtrap", StringComparison.OrdinalIgnoreCase)
            && host.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRoutingMode()
    {
        var provider = configuration["Email:Provider"] ?? "Demo";
        return provider.Equals("Routing", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSandboxSmtpConfigured()
    {
        return !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:Smtp:Host"]);
    }

    private bool IsPrimarySmtpAllowed(User user)
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

    private IReadOnlyList<string> GetCsv(string key)
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

    private Task<User?> FindUserByUsernameOrEmailAsync(string value, CancellationToken cancellationToken)
    {
        var lookup = value.Trim().ToLowerInvariant();
        return UserQuery().SingleOrDefaultAsync(
            user => user.Username.ToLower() == lookup || user.Email.ToLower() == lookup,
            cancellationToken);
    }

    private async Task<Result<EmailVerificationStartResponse>> StartEmailVerificationForUserAsync(
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

    private static string BuildEmailVerificationBody(
        string displayName,
        string code,
        DateTime expiresAt,
        bool isSandboxMode)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeCode = WebUtility.HtmlEncode(code);
        var expiry = WebUtility.HtmlEncode(FormatTurkeyTime(expiresAt));
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
                          <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#647187;">Kod gecerliligi: <strong>{expiry}</strong></p>
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

    private static string BuildTemporaryPasswordBody(string displayName, string username, string temporaryPassword)
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

    private static ForgotPasswordResponse GenericForgotPasswordResponse() =>
        new(GenericForgotPasswordMessage);

    private string BuildPasswordResetUrl(User user, string resetToken)
    {
        var configuredBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var baseUrl = configuredBaseUrl.TrimEnd('/');
        return $"{baseUrl}/?auth=reset&usernameOrEmail={Uri.EscapeDataString(user.Username)}&token={Uri.EscapeDataString(resetToken)}";
    }

    private static string BuildPasswordResetBody(
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

    private static string FormatTurkeyTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var turkeyTimeZone = ResolveTurkeyTimeZone();
        var turkeyTime = TimeZoneInfo.ConvertTimeFromUtc(utcValue, turkeyTimeZone);
        return $"{turkeyTime:dd.MM.yyyy HH:mm} GMT+3";
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
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

    private async Task RevokeAllSessionsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
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

    private async Task RevokeRefreshTokensForSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var refreshTokens = await db.RefreshTokens
            .Where(token => token.UserSessionId == sessionId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
        }
    }

    private IQueryable<User> UserQuery() =>
        db.Users
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions);

    private static bool HasActiveCommunityAccess(User user) =>
        user.Role == Role.SuperAdmin
        || user.CommunityMemberships.Count == 0
        || user.CommunityMemberships.Any(membership =>
            membership.IsActive
            && membership.CommunityRole is not null
            && (membership.Community?.IsActive == true
                || membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.CommunityManageAdmins)));

    private bool CanManageUsers(UserDto currentUser, Guid? targetCommunityId)
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

    private async Task<Guid?> ResolveTargetCommunityIdAsync(
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

    private async Task<Guid?> ResolveTargetCommunityRoleIdAsync(
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

    private async Task<UserCommunityMembership?> BuildDefaultMembershipAsync(Role role, CancellationToken cancellationToken)
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

    private async Task NotifyUserAsync(
        Guid userId,
        string type,
        string title,
        string message,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
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

    private async Task NotifyCommunityManagersAsync(
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
