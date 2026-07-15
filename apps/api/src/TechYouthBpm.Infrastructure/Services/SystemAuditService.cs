using Microsoft.EntityFrameworkCore;
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
    public async Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default)
    {
        db.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
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
        LogAsync(actor.Id, action, entityType, entityId, description, cancellationToken);

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
                ActorDisplayName = log.ActorUser != null ? log.ActorUser.DisplayName : "System",
                ActorUsername = log.ActorUser != null ? log.ActorUser.Username : "system",
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description,
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
                entityUser?.Username);
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

        var baseQuery = ApplySearchFilter(ApplyCommunityScope(db.SystemAuditLogs.Include(log => log.ActorUser), currentUser), query);
        var counts = new SystemAuditCategoryCountsDto(
            await baseQuery.CountAsync(cancellationToken),
            await ApplyCategoryFilter(baseQuery, "identity").CountAsync(cancellationToken),
            await ApplyCategoryFilter(baseQuery, "access").CountAsync(cancellationToken),
            await ApplyCategoryFilter(baseQuery, "forms").CountAsync(cancellationToken),
            await ApplyCategoryFilter(baseQuery, "processes").CountAsync(cancellationToken),
            await ApplyCategoryFilter(baseQuery, "tasks").CountAsync(cancellationToken));

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

    private static IQueryable<SystemAuditLog> ApplyCategoryFilter(IQueryable<SystemAuditLog> query, string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "identity" => query.Where(log =>
                log.Action == "Auth.AccountLocked"
                || log.Action == "Auth.EmailVerificationRequested"
                || log.Action == "Auth.EmailVerified"
                || log.Action == "Auth.LoginFailed"
                || log.Action == "Auth.LoginSucceeded"
                || log.Action == "Auth.Logout"
                || log.Action == "Auth.PasswordChanged"
                || log.Action == "Auth.RegisterRequested"
                || log.Action == "Auth.SessionRevoked"
                || log.Action == "Auth.TemporaryPasswordChanged"
                || log.Action == "User.ProfileAndEmailUpdated"
                || log.Action == "User.ProfileUpdated"),
            "access" => query.Where(log =>
                log.Action == "Auth.AdminSessionRevoked"
                || log.Action == "User.AccessUpdated"
                || log.Action == "User.CreatedByAdmin"
                || log.Action == "User.DeletedByAdmin"
                || log.Action.StartsWith("Team.")),
            "forms" => query.Where(log => log.Action.StartsWith("FormDefinition.") || log.EntityType == "FormDefinition"),
            "processes" => query.Where(log => log.Action.StartsWith("Process.") || log.EntityType == "ProcessInstance"),
            "tasks" => query.Where(log => log.Action.StartsWith("Task.") || log.EntityType == "ProcessTask"),
            _ => query
        };

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
            (log.ActorUser != null
                && log.ActorUser.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId))
            || (log.EntityType == "User"
                && log.EntityId != null
                && db.Users.Any(user =>
                    user.Id.ToString() == log.EntityId
                    && user.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId))));
    }
}
