using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class SystemAuditService(AppDbContext db) : ISystemAuditService
{
    public Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default) =>
        LogAsync(actorUserId, action, entityType, entityId, description, new SystemAuditContext(), cancellationToken);

    public async Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        SystemAuditContext context,
        CancellationToken cancellationToken = default)
    {
        var resolvedCommunityId = context.CommunityId
            ?? await ResolveCommunityIdAsync(actorUserId, entityType, entityId, cancellationToken);
        var communityId = await KeepExistingCommunityIdAsync(resolvedCommunityId, cancellationToken);
        var category = SystemAuditCategories.IsKnown(context.Category)
            ? context.Category!.ToLowerInvariant()
            : SystemAuditCategories.Resolve(action, entityType);

        db.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            CommunityId = communityId,
            Category = category,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            MetadataJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                action,
                entityType,
                entityId,
                details = context.Metadata
            }),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task LogAsync(
        UserDto actor,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default) =>
        LogAsync(
            actor.Id,
            action,
            entityType,
            entityId,
            description,
            new SystemAuditContext(actor.CommunityId),
            cancellationToken);

    public Task LogAsync(
        UserDto actor,
        string action,
        string entityType,
        string? entityId,
        string description,
        SystemAuditContext context,
        CancellationToken cancellationToken = default) =>
        LogAsync(
            actor.Id,
            action,
            entityType,
            entityId,
            description,
            context with { CommunityId = context.CommunityId ?? actor.CommunityId },
            cancellationToken);

    public async Task<Result<PagedResult<SystemAuditLogDto>>> ListAsync(
        UserDto currentUser,
        SystemAuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionNames.AuditView))
        {
            return Result<PagedResult<SystemAuditLogDto>>.Failure("Community management permission is required to view system audit logs.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = db.SystemAuditLogs
            .Include(log => log.ActorUser)
            .ThenInclude(user => user!.CommunityMemberships)
            .AsQueryable();

        query = ApplySearchFilter(ApplyCategoryFilter(ApplyCommunityScope(query, currentUser), request.Category), request.Query);

        var totalCount = await query.CountAsync(cancellationToken);
        var logs = await ApplySort(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new
            {
                log.Id,
                log.ActorUserId,
                log.CommunityId,
                log.Category,
                ActorDisplayName = log.ActorUser != null ? log.ActorUser.DisplayName : "System",
                ActorUsername = log.ActorUser != null ? log.ActorUser.Username : "system",
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description,
                log.MetadataJson,
                log.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var userEntityIds = logs
            .Where(log => log.EntityType == "User" && Guid.TryParse(log.EntityId, out _))
            .Select(log => Guid.Parse(log.EntityId!))
            .Distinct()
            .ToArray();
        var entityUsers = await db.Users
            .Where(user => userEntityIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var items = logs.Select(log =>
        {
            User? entityUser = null;
            if (log.EntityType == "User" && Guid.TryParse(log.EntityId, out var entityUserId))
            {
                entityUsers.TryGetValue(entityUserId, out entityUser);
            }

            return new SystemAuditLogDto(
                log.Id,
                log.ActorUserId,
                log.ActorDisplayName,
                log.ActorUsername,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description,
                log.CreatedAt,
                entityUser?.DisplayName,
                entityUser?.Username,
                log.CommunityId,
                log.Category,
                log.MetadataJson);
        }).ToArray();

        return Result<PagedResult<SystemAuditLogDto>>.Success(new PagedResult<SystemAuditLogDto>(
            items,
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<SystemAuditCategoryCountsDto>> CountByCategoryAsync(
        UserDto currentUser,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionNames.AuditView))
        {
            return Result<SystemAuditCategoryCountsDto>.Failure("Community management permission is required to view system audit logs.");
        }

        var baseQuery = ApplyCommunityScope(db.SystemAuditLogs.Include(log => log.ActorUser), currentUser);
        var aggregate = await baseQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                All = group.Count(),
                Identity = group.Count(log => log.Category == SystemAuditCategories.Identity),
                Access = group.Count(log => log.Category == SystemAuditCategories.Access),
                Forms = group.Count(log => log.Category == SystemAuditCategories.Forms),
                Processes = group.Count(log => log.Category == SystemAuditCategories.Processes),
                Tasks = group.Count(log => log.Category == SystemAuditCategories.Tasks)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var counts = aggregate is null
            ? new SystemAuditCategoryCountsDto(0, 0, 0, 0, 0, 0)
            : new SystemAuditCategoryCountsDto(
                aggregate.All,
                aggregate.Identity,
                aggregate.Access,
                aggregate.Forms,
                aggregate.Processes,
                aggregate.Tasks);

        return Result<SystemAuditCategoryCountsDto>.Success(counts);
    }

    private static IQueryable<SystemAuditLog> ApplySearchFilter(IQueryable<SystemAuditLog> query, string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        var search = searchQuery.Trim().ToLowerInvariant();
        return query.Where(log =>
            (log.ActorUser != null && log.ActorUser.DisplayName.ToLower().Contains(search))
            || (log.ActorUser != null && log.ActorUser.Username.ToLower().Contains(search))
            || log.Action.ToLower().Contains(search)
            || log.EntityType.ToLower().Contains(search)
            || (log.EntityId != null && log.EntityId.ToLower().Contains(search))
            || log.Description.ToLower().Contains(search));
    }

    private static IQueryable<SystemAuditLog> ApplyCategoryFilter(IQueryable<SystemAuditLog> query, string? category)
    {
        var normalized = category?.Trim().ToLowerInvariant();
        return SystemAuditCategories.IsKnown(normalized) && normalized != SystemAuditCategories.Other
            ? query.Where(log => log.Category == normalized)
            : query;
    }

    private static IQueryable<SystemAuditLog> ApplySort(
        IQueryable<SystemAuditLog> query,
        string? sortBy,
        string? sortDirection)
    {
        var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "action" => ascending
                ? query.OrderBy(log => log.Action).ThenBy(log => log.CreatedAt)
                : query.OrderByDescending(log => log.Action).ThenByDescending(log => log.CreatedAt),
            "actor" => ascending
                ? query.OrderBy(log => log.ActorUser == null ? string.Empty : log.ActorUser.Username).ThenBy(log => log.CreatedAt)
                : query.OrderByDescending(log => log.ActorUser == null ? string.Empty : log.ActorUser.Username).ThenByDescending(log => log.CreatedAt),
            _ => ascending
                ? query.OrderBy(log => log.CreatedAt)
                : query.OrderByDescending(log => log.CreatedAt)
        };
    }

    private IQueryable<SystemAuditLog> ApplyCommunityScope(IQueryable<SystemAuditLog> query, UserDto currentUser)
    {
        if (currentUser.IsSuperAdmin())
        {
            return query;
        }

        if (currentUser.CommunityId is null)
        {
            return query.Where(log => false);
        }

        var communityId = currentUser.CommunityId.Value;
        return query.Where(log =>
            log.CommunityId == communityId
            || (log.CommunityId == null
                && log.ActorUser != null
                && log.ActorUser.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId))
            || (log.CommunityId == null
                && log.EntityType == "User"
                && log.EntityId != null
                && db.Users.Any(user =>
                    user.Id.ToString() == log.EntityId
                    && user.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId))));
    }

    private async Task<Guid?> ResolveCommunityIdAsync(
        Guid? actorUserId,
        string entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(entityId, out var entityGuid))
        {
            var entityCommunityId = entityType switch
            {
                "Community" => entityGuid,
                "CommunityRole" => await db.CommunityRoles
                    .Where(role => role.Id == entityGuid)
                    .Select(role => (Guid?)role.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "Team" => await db.Teams
                    .Where(team => team.Id == entityGuid)
                    .Select(team => (Guid?)team.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "FormDefinition" => await db.FormDefinitions
                    .Where(form => form.Id == entityGuid)
                    .Select(form => (Guid?)form.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "FormDefinitionVersion" => await db.FormDefinitionVersions
                    .Where(version => version.Id == entityGuid)
                    .Select(version => (Guid?)version.FormDefinition!.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "ProcessDefinition" => await db.ProcessDefinitions
                    .Where(definition => definition.Id == entityGuid)
                    .Select(definition => (Guid?)definition.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "ProcessInstance" => await db.ProcessInstances
                    .Where(process => process.Id == entityGuid)
                    .Select(process => (Guid?)process.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "ProcessTask" => await db.ProcessTasks
                    .Where(task => task.Id == entityGuid)
                    .Select(task => (Guid?)task.ProcessInstance!.CommunityId)
                    .SingleOrDefaultAsync(cancellationToken),
                "User" => await db.UserCommunityMemberships
                    .Where(membership => membership.UserId == entityGuid && membership.IsActive)
                    .Select(membership => (Guid?)membership.CommunityId)
                    .FirstOrDefaultAsync(cancellationToken),
                _ => null
            };

            if (entityCommunityId is not null)
            {
                return entityCommunityId;
            }
        }

        return actorUserId is null
            ? null
            : await db.UserCommunityMemberships
                .Where(membership => membership.UserId == actorUserId && membership.IsActive)
                .Select(membership => (Guid?)membership.CommunityId)
                .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> KeepExistingCommunityIdAsync(
        Guid? communityId,
        CancellationToken cancellationToken)
    {
        if (communityId is null)
        {
            return null;
        }

        var trackedCommunity = db.ChangeTracker
            .Entries<Community>()
            .FirstOrDefault(entry => entry.Entity.Id == communityId.Value);
        if (trackedCommunity is not null)
        {
            return trackedCommunity.State == EntityState.Deleted ? null : communityId;
        }

        return await db.Communities
            .AsNoTracking()
            .AnyAsync(community => community.Id == communityId.Value, cancellationToken)
                ? communityId
                : null;
    }
}
