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

internal sealed class RegistrationService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender) : AuthServiceBase(db, configuration, auditService, otpService, emailSender), IRegistrationService
{
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

}
