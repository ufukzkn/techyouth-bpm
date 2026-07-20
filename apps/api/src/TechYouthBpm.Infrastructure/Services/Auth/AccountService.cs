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

internal sealed class AccountService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender) : AuthServiceBase(db, configuration, auditService, otpService, emailSender), IAccountService
{
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

}
