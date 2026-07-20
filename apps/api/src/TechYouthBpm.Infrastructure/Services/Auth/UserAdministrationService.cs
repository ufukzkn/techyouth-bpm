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

internal sealed class UserAdministrationService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender) : AuthServiceBase(db, configuration, auditService, otpService, emailSender), IUserAdministrationService
{
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

    public async Task<Result<PagedResult<UserAdminDto>>> ListCommunityUsersAsync(
        Guid communityId,
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunityUsers(currentUser, communityId))
        {
            return Result<PagedResult<UserAdminDto>>.Failure("Current user cannot view community users.");
        }

        return await ListUsersAsync(currentUser, request with { CommunityId = communityId }, cancellationToken);
    }

    public async Task<Result<UserAdminDto>> CreateCommunityUserAsync(
        Guid communityId,
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunityUsers(currentUser, communityId))
        {
            return Result<UserAdminDto>.Failure("Current user cannot create community users.");
        }

        return await CreateUserAsync(request with { CommunityId = communityId }, currentUser, cancellationToken);
    }

    public async Task<Result<UserAdminDto>> UpdateCommunityMembershipAsync(
        Guid communityId,
        Guid userId,
        UpdateUserMembershipRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunityUsers(currentUser, communityId))
        {
            return Result<UserAdminDto>.Failure("Current user cannot update community memberships.");
        }

        var user = await UserQuery().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure("User not found.");
        }

        if (!currentUser.IsSuperAdmin()
            && !user.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId))
        {
            return Result<UserAdminDto>.Failure("Current user cannot update memberships outside this community.");
        }

        var roleExists = await db.CommunityRoles.AnyAsync(
            role => role.Id == request.CommunityRoleId && role.CommunityId == communityId,
            cancellationToken);
        if (!roleExists)
        {
            return Result<UserAdminDto>.Failure("Community role was not found.");
        }

        var currentCommunityId = user.ToDto().CommunityId;
        foreach (var membership in user.CommunityMemberships)
        {
            membership.IsActive = false;
        }

        await DeactivateTeamMembershipsIfCommunityChangedAsync(user.Id, currentCommunityId, communityId, cancellationToken);

        var newMembership = new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CommunityId = communityId,
            CommunityRoleId = request.CommunityRoleId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        db.UserCommunityMemberships.Add(newMembership);

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "CommunityMembership.Updated",
            "User",
            user.Id.ToString(),
            $"User '{user.Username}' membership was updated.",
            cancellationToken);

        return Result<UserAdminDto>.Success(user.ToAdminDto());
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

            await DeactivateTeamMembershipsIfCommunityChangedAsync(
                user.Id,
                userDto.CommunityId,
                targetCommunityId.Value,
                cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);
        var updated = await UserQuery().SingleAsync(item => item.Id == user.Id, cancellationToken);
        return Result<UserAdminDto>.Success(updated.ToAdminDto());
    }

}
