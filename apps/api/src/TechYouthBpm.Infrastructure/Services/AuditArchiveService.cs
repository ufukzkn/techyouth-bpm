using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public sealed class AuditArchiveService(AppDbContext db) : IAuditArchiveService
{
    public async Task<Result<IReadOnlyList<CommunityDeletionArchiveDto>>> ListArchivesAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<IReadOnlyList<CommunityDeletionArchiveDto>>.Failure(
                "Only SuperAdmin users can view deleted community archives.");
        }

        var archives = await db.CommunityDeletionArchives
            .AsNoTracking()
            .OrderByDescending(archive => archive.DeletedAt)
            .Select(archive => new CommunityDeletionArchiveDto(
                archive.Id,
                archive.OriginalCommunityId,
                archive.CommunityName,
                archive.DeletedByUserId,
                archive.DeletedByUsername,
                archive.DeletedByDisplayName,
                archive.Reason,
                archive.DeletedAt,
                archive.UserCount,
                archive.PreservedUserCount,
                archive.CommunityRoleCount,
                archive.TeamCount,
                archive.FormCount,
                archive.WorkflowCount,
                archive.ProcessCount,
                archive.TaskCount,
                archive.NotificationCount,
                archive.SystemAuditCount,
                archive.ProcessStepCount))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CommunityDeletionArchiveDto>>.Success(archives);
    }

    public async Task<Result<PagedResult<ArchivedAuditEventDto>>> ListEventsAsync(
        Guid archiveId,
        UserDto currentUser,
        SystemAuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<PagedResult<ArchivedAuditEventDto>>.Failure(
                "Only SuperAdmin users can view deleted community archives.");
        }

        if (!await db.CommunityDeletionArchives.AsNoTracking().AnyAsync(
                archive => archive.Id == archiveId,
                cancellationToken))
        {
            return Result<PagedResult<ArchivedAuditEventDto>>.Failure("Deleted community archive was not found.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = db.ArchivedAuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.CommunityDeletionArchiveId == archiveId);

        query = ApplyCategoryFilter(query, request.Category);
        query = ApplySearchFilter(query, request.Query);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplySort(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(auditEvent => new ArchivedAuditEventDto(
                auditEvent.Id,
                auditEvent.OriginalEventId,
                auditEvent.Source,
                auditEvent.Category,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.ActorUserId,
                auditEvent.ActorDisplayName,
                auditEvent.ActorUsername,
                auditEvent.EntityDisplayName,
                auditEvent.EntityUsername,
                auditEvent.Description,
                auditEvent.NodeTitle,
                auditEvent.TeamName,
                auditEvent.CommunityRoleName,
                auditEvent.OccurredAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ArchivedAuditEventDto>>.Success(
            new PagedResult<ArchivedAuditEventDto>(items, page, pageSize, totalCount));
    }

    public async Task<Result<SystemAuditCategoryCountsDto>> CountEventsByCategoryAsync(
        Guid archiveId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<SystemAuditCategoryCountsDto>.Failure(
                "Only SuperAdmin users can view deleted community archives.");
        }

        if (!await db.CommunityDeletionArchives.AsNoTracking().AnyAsync(
                archive => archive.Id == archiveId,
                cancellationToken))
        {
            return Result<SystemAuditCategoryCountsDto>.Failure("Deleted community archive was not found.");
        }

        var aggregate = await db.ArchivedAuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.CommunityDeletionArchiveId == archiveId)
            .GroupBy(_ => 1)
            .Select(group => new SystemAuditCategoryCountsDto(
                group.Count(),
                group.Count(auditEvent => auditEvent.Category == SystemAuditCategories.Identity),
                group.Count(auditEvent => auditEvent.Category == SystemAuditCategories.Access),
                group.Count(auditEvent => auditEvent.Category == SystemAuditCategories.Forms),
                group.Count(auditEvent => auditEvent.Category == SystemAuditCategories.Processes),
                group.Count(auditEvent => auditEvent.Category == SystemAuditCategories.Tasks)))
            .SingleOrDefaultAsync(cancellationToken);

        return Result<SystemAuditCategoryCountsDto>.Success(
            aggregate ?? new SystemAuditCategoryCountsDto(0, 0, 0, 0, 0, 0));
    }

    private static IQueryable<ArchivedAuditEvent> ApplyCategoryFilter(
        IQueryable<ArchivedAuditEvent> query,
        string? category)
    {
        var normalized = category?.Trim().ToLowerInvariant();
        return SystemAuditCategories.IsKnown(normalized) && normalized != SystemAuditCategories.Other
            ? query.Where(auditEvent => auditEvent.Category == normalized)
            : query;
    }

    private static IQueryable<ArchivedAuditEvent> ApplySearchFilter(
        IQueryable<ArchivedAuditEvent> query,
        string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return query;
        }

        var search = searchQuery.Trim().ToLowerInvariant();
        return query.Where(auditEvent =>
            auditEvent.ActorDisplayName.ToLower().Contains(search)
            || auditEvent.ActorUsername.ToLower().Contains(search)
            || auditEvent.Action.ToLower().Contains(search)
            || auditEvent.EntityType.ToLower().Contains(search)
            || (auditEvent.EntityId != null && auditEvent.EntityId.ToLower().Contains(search))
            || (auditEvent.EntityDisplayName != null && auditEvent.EntityDisplayName.ToLower().Contains(search))
            || auditEvent.Description.ToLower().Contains(search)
            || auditEvent.NodeTitle.ToLower().Contains(search)
            || auditEvent.TeamName.ToLower().Contains(search)
            || auditEvent.CommunityRoleName.ToLower().Contains(search));
    }

    private static IQueryable<ArchivedAuditEvent> ApplySort(
        IQueryable<ArchivedAuditEvent> query,
        string? sortBy,
        string? sortDirection)
    {
        var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "action" => ascending
                ? query.OrderBy(auditEvent => auditEvent.Action).ThenBy(auditEvent => auditEvent.OccurredAt)
                : query.OrderByDescending(auditEvent => auditEvent.Action).ThenByDescending(auditEvent => auditEvent.OccurredAt),
            "actor" => ascending
                ? query.OrderBy(auditEvent => auditEvent.ActorUsername).ThenBy(auditEvent => auditEvent.OccurredAt)
                : query.OrderByDescending(auditEvent => auditEvent.ActorUsername).ThenByDescending(auditEvent => auditEvent.OccurredAt),
            _ => ascending
                ? query.OrderBy(auditEvent => auditEvent.OccurredAt)
                : query.OrderByDescending(auditEvent => auditEvent.OccurredAt)
        };
    }
}
