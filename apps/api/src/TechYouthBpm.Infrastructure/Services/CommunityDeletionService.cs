using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

public sealed class CommunityDeletionService(
    AppDbContext db,
    ISessionValidationCache sessionCache) : ICommunityDeletionService
{
    public async Task<Result<CommunityDeletionImpactDto>> GetImpactAsync(
        Guid communityId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<CommunityDeletionImpactDto>.Failure(
                "Only SuperAdmin users can inspect permanent community deletion.");
        }

        var community = await db.Communities
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == communityId, cancellationToken);
        if (community is null)
        {
            return Result<CommunityDeletionImpactDto>.Failure("Community was not found.");
        }

        return Result<CommunityDeletionImpactDto>.Success(
            await BuildImpactAsync(community, cancellationToken));
    }

    public async Task<Result<CommunityPurgeResultDto>> PurgeAsync(
        Guid communityId,
        PurgeCommunityRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<CommunityPurgeResultDto>.Failure(
                "Only SuperAdmin users can permanently delete communities.");
        }

        var community = await db.Communities
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == communityId, cancellationToken);
        if (community is null)
        {
            return Result<CommunityPurgeResultDto>.Failure("Community was not found.");
        }

        var validationErrors = ValidateRequest(community, request);
        if (validationErrors.Count > 0)
        {
            return Result<CommunityPurgeResultDto>.Failure(validationErrors);
        }

        var deletingUser = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == currentUser.Id, cancellationToken);
        if (deletingUser is null
            || !PasswordHasher.IsHashed(deletingUser.Password)
            || !PasswordHasher.Verify(request.CurrentPassword, deletingUser.Password))
        {
            return Result<CommunityPurgeResultDto>.Failure("Current SuperAdmin password is incorrect.");
        }

        var targetUserIds = await db.UserCommunityMemberships
            .AsNoTracking()
            .Where(membership => membership.CommunityId == communityId)
            .Select(membership => membership.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var preservedUserIds = await FindPreservedUserIdsAsync(
            communityId,
            targetUserIds,
            cancellationToken);
        var exclusiveUserIds = targetUserIds.Except(preservedUserIds).ToArray();
        var processIds = await db.ProcessInstances
            .AsNoTracking()
            .Where(process => process.CommunityId == communityId)
            .Select(process => process.Id)
            .ToListAsync(cancellationToken);
        var processDefinitionIds = await db.ProcessDefinitions
            .AsNoTracking()
            .Where(definition => definition.CommunityId == communityId)
            .Select(definition => definition.Id)
            .ToListAsync(cancellationToken);
        var formIds = await db.FormDefinitions
            .AsNoTracking()
            .Where(form => form.CommunityId == communityId)
            .Select(form => form.Id)
            .ToListAsync(cancellationToken);
        var teamIds = await db.Teams
            .AsNoTracking()
            .Where(team => team.CommunityId == communityId)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

        var impact = await BuildImpactAsync(community, cancellationToken);
        var archive = await BuildArchiveAsync(
            community,
            currentUser,
            request.Reason.Trim(),
            impact,
            cancellationToken);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.CommunityDeletionArchives.Add(archive);

            await db.UserSessions
                .Where(session => targetUserIds.Contains(session.UserId) && session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
            await db.RefreshTokens
                .Where(token => targetUserIds.Contains(token.UserId) && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

            await db.ProcessInstances
                .Where(process => process.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.ProcessDefinitions
                .Where(definition => definition.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.FormDefinitions
                .Where(form => form.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);

            await db.Notifications
                .Where(notification =>
                    notification.CommunityId == communityId
                    || exclusiveUserIds.Contains(notification.UserId)
                    || (notification.EntityType == "ProcessInstance"
                        && notification.EntityId != null
                        && processIds.Select(id => id.ToString()).Contains(notification.EntityId))
                    || (notification.EntityType == "Team"
                        && notification.EntityId != null
                        && teamIds.Select(id => id.ToString()).Contains(notification.EntityId)))
                .ExecuteDeleteAsync(cancellationToken);

            await db.Teams
                .Where(team => team.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.UserCommunityMemberships
                .Where(membership => membership.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.CommunityRoles
                .Where(role => role.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.SystemAuditLogs
                .Where(log => log.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);

            if (exclusiveUserIds.Length > 0)
            {
                await db.Users
                    .Where(user => exclusiveUserIds.Contains(user.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await db.Communities
                .Where(item => item.Id == communityId)
                .ExecuteDeleteAsync(cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        foreach (var userId in targetUserIds)
        {
            sessionCache.InvalidateUser(userId);
        }
        sessionCache.InvalidateCommunity(communityId);

        return Result<CommunityPurgeResultDto>.Success(new CommunityPurgeResultDto(
            archive.Id,
            community.Id,
            community.Name,
            archive.DeletedAt,
            impact));
    }

    private async Task<CommunityDeletionImpactDto> BuildImpactAsync(
        Community community,
        CancellationToken cancellationToken)
    {
        var targetUserIds = await db.UserCommunityMemberships
            .AsNoTracking()
            .Where(membership => membership.CommunityId == community.Id)
            .Select(membership => membership.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var preservedUserIds = await FindPreservedUserIdsAsync(
            community.Id,
            targetUserIds,
            cancellationToken);
        var processIds = db.ProcessInstances
            .Where(process => process.CommunityId == community.Id)
            .Select(process => process.Id);

        return new CommunityDeletionImpactDto(
            community.Id,
            community.Name,
            community.IsActive,
            targetUserIds.Count,
            preservedUserIds.Count,
            await db.CommunityRoles.CountAsync(role => role.CommunityId == community.Id, cancellationToken),
            await db.Teams.CountAsync(team => team.CommunityId == community.Id, cancellationToken),
            await db.FormDefinitions.CountAsync(form => form.CommunityId == community.Id, cancellationToken),
            await db.ProcessDefinitions.CountAsync(definition => definition.CommunityId == community.Id, cancellationToken),
            await db.ProcessInstances.CountAsync(process => process.CommunityId == community.Id, cancellationToken),
            await db.ProcessTasks.CountAsync(task => processIds.Contains(task.ProcessInstanceId), cancellationToken),
            await db.Notifications.CountAsync(notification =>
                notification.CommunityId == community.Id
                || (notification.CommunityId == null && targetUserIds.Contains(notification.UserId)), cancellationToken),
            await db.SystemAuditLogs.CountAsync(log => log.CommunityId == community.Id, cancellationToken),
            await db.ProcessStepExecutions.CountAsync(step => processIds.Contains(step.ProcessInstanceId), cancellationToken));
    }

    private async Task<HashSet<Guid>> FindPreservedUserIdsAsync(
        Guid communityId,
        IReadOnlyCollection<Guid> targetUserIds,
        CancellationToken cancellationToken)
    {
        if (targetUserIds.Count == 0)
        {
            return [];
        }

        var preserved = (await db.Users
                .AsNoTracking()
                .Where(user => targetUserIds.Contains(user.Id) && user.Role == Role.SuperAdmin)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        await AddIdsAsync(preserved, db.UserCommunityMemberships
            .Where(membership =>
                targetUserIds.Contains(membership.UserId)
                && membership.CommunityId != communityId)
            .Select(membership => membership.UserId), cancellationToken);
        await AddIdsAsync(preserved, db.TeamMemberships
            .Where(membership =>
                targetUserIds.Contains(membership.UserId)
                && membership.Team != null
                && membership.Team.CommunityId != communityId)
            .Select(membership => membership.UserId), cancellationToken);
        await AddIdsAsync(preserved, db.Teams
            .Where(team =>
                team.CreatedByUserId != null
                && targetUserIds.Contains(team.CreatedByUserId.Value)
                && team.CommunityId != communityId)
            .Select(team => team.CreatedByUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.FormDefinitions
            .Where(form =>
                form.CommunityId != communityId
                && targetUserIds.Contains(form.CreatedByUserId))
            .Select(form => form.CreatedByUserId), cancellationToken);
        await AddIdsAsync(preserved, db.FormDefinitions
            .Where(form =>
                form.CommunityId != communityId
                && form.UpdatedByUserId != null
                && targetUserIds.Contains(form.UpdatedByUserId.Value))
            .Select(form => form.UpdatedByUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessDefinitions
            .Where(definition =>
                definition.CommunityId != communityId
                && targetUserIds.Contains(definition.CreatedByUserId))
            .Select(definition => definition.CreatedByUserId), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessDefinitions
            .Where(definition =>
                definition.CommunityId != communityId
                && definition.UpdatedByUserId != null
                && targetUserIds.Contains(definition.UpdatedByUserId.Value))
            .Select(definition => definition.UpdatedByUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessInstances
            .Where(process =>
                process.CommunityId != communityId
                && targetUserIds.Contains(process.StartedByUserId))
            .Select(process => process.StartedByUserId), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessTasks
            .Where(task =>
                task.ProcessInstance != null
                && task.ProcessInstance.CommunityId != communityId
                && task.AssignedUserId != null
                && targetUserIds.Contains(task.AssignedUserId.Value))
            .Select(task => task.AssignedUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessTasks
            .Where(task =>
                task.ProcessInstance != null
                && task.ProcessInstance.CommunityId != communityId
                && task.ClaimedByUserId != null
                && targetUserIds.Contains(task.ClaimedByUserId.Value))
            .Select(task => task.ClaimedByUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.ProcessTasks
            .Where(task =>
                task.ProcessInstance != null
                && task.ProcessInstance.CommunityId != communityId
                && task.CompletedByUserId != null
                && targetUserIds.Contains(task.CompletedByUserId.Value))
            .Select(task => task.CompletedByUserId!.Value), cancellationToken);
        await AddIdsAsync(preserved, db.SystemAuditLogs
            .Where(log =>
                log.ActorUserId != null
                && targetUserIds.Contains(log.ActorUserId.Value)
                && log.CommunityId != communityId)
            .Select(log => log.ActorUserId!.Value), cancellationToken);

        return preserved;
    }

    private async Task<CommunityDeletionArchive> BuildArchiveAsync(
        Community community,
        UserDto currentUser,
        string reason,
        CommunityDeletionImpactDto impact,
        CancellationToken cancellationToken)
    {
        var archive = new CommunityDeletionArchive
        {
            Id = Guid.NewGuid(),
            OriginalCommunityId = community.Id,
            CommunityName = community.Name,
            DeletedByUserId = currentUser.Id,
            DeletedByUsername = currentUser.Username,
            DeletedByDisplayName = currentUser.DisplayName,
            Reason = reason,
            DeletedAt = DateTime.UtcNow,
            UserCount = impact.UserCount,
            PreservedUserCount = impact.PreservedUserCount,
            CommunityRoleCount = impact.CommunityRoleCount,
            TeamCount = impact.TeamCount,
            FormCount = impact.FormCount,
            WorkflowCount = impact.WorkflowCount,
            ProcessCount = impact.ProcessCount,
            TaskCount = impact.TaskCount,
            NotificationCount = impact.NotificationCount,
            SystemAuditCount = impact.SystemAuditCount,
            ProcessStepCount = impact.ProcessStepCount
        };

        var systemEvents = await db.SystemAuditLogs
            .AsNoTracking()
            .Where(log => log.CommunityId == community.Id)
            .Select(log => new
            {
                log.Id,
                log.ActorUserId,
                ActorDisplayName = log.ActorUser != null ? log.ActorUser.DisplayName : "System",
                ActorUsername = log.ActorUser != null ? log.ActorUser.Username : "system",
                log.Category,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in systemEvents)
        {
            archive.Events.Add(new ArchivedAuditEvent
            {
                Id = Guid.NewGuid(),
                CommunityDeletionArchiveId = archive.Id,
                OriginalEventId = item.Id,
                Source = "system",
                Category = NormalizeCategory(item.Category),
                Action = item.Action,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                ActorUserId = item.ActorUserId,
                ActorDisplayName = item.ActorDisplayName,
                ActorUsername = item.ActorUsername,
                Description = $"{item.ActorDisplayName} performed {item.Action} on {item.EntityType}.",
                OccurredAt = item.CreatedAt
            });
        }

        var stepEvents = await db.ProcessStepExecutions
            .AsNoTracking()
            .Where(step => step.ProcessInstance != null
                && step.ProcessInstance.CommunityId == community.Id)
            .Select(step => new
            {
                step.Id,
                step.ProcessInstanceId,
                step.CompletedByUserId,
                ActorDisplayName = step.CompletedByUser != null
                    ? step.CompletedByUser.DisplayName
                    : "System",
                ActorUsername = step.CompletedByUser != null
                    ? step.CompletedByUser.Username
                    : "system",
                step.NodeTitle,
                step.TeamNameSnapshot,
                step.CommunityRoleNameSnapshot,
                step.Action,
                step.Status,
                OccurredAt = step.CompletedAt ?? step.EnteredAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in stepEvents)
        {
            var action = item.Action?.ToString() ?? $"Step.{item.Status}";
            archive.Events.Add(new ArchivedAuditEvent
            {
                Id = Guid.NewGuid(),
                CommunityDeletionArchiveId = archive.Id,
                OriginalEventId = item.Id,
                Source = "process",
                Category = SystemAuditCategories.Processes,
                Action = action,
                EntityType = "ProcessInstance",
                EntityId = item.ProcessInstanceId.ToString(),
                ActorUserId = item.CompletedByUserId,
                ActorDisplayName = item.ActorDisplayName,
                ActorUsername = item.ActorUsername,
                Description = $"{item.ActorDisplayName} completed workflow step '{item.NodeTitle}' with {action}.",
                NodeTitle = item.NodeTitle,
                TeamName = item.TeamNameSnapshot,
                CommunityRoleName = item.CommunityRoleNameSnapshot,
                OccurredAt = item.OccurredAt
            });
        }

        return archive;
    }

    private static List<string> ValidateRequest(Community community, PurgeCommunityRequest request)
    {
        var errors = new List<string>();
        if (community.IsActive)
        {
            errors.Add("Community must be inactive before permanent deletion.");
        }
        if (!string.Equals(community.Name, request.ConfirmationName?.Trim(), StringComparison.Ordinal))
        {
            errors.Add("Community name confirmation does not match.");
        }
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            errors.Add("Current SuperAdmin password is required.");
        }
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 10)
        {
            errors.Add("Deletion reason must be at least 10 characters.");
        }
        else if (reason.Length > 500)
        {
            errors.Add("Deletion reason cannot exceed 500 characters.");
        }
        return errors;
    }

    private static string NormalizeCategory(string? category) =>
        SystemAuditCategories.IsKnown(category?.Trim().ToLowerInvariant())
            ? category!.Trim().ToLowerInvariant()
            : SystemAuditCategories.Other;

    private static async Task AddIdsAsync(
        ISet<Guid> target,
        IQueryable<Guid> query,
        CancellationToken cancellationToken)
    {
        foreach (var id in await query.Distinct().ToListAsync(cancellationToken))
        {
            target.Add(id);
        }
    }
}
