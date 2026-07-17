namespace TechYouthBpm.Application.Audit;

public record SystemAuditLogDto(
    Guid Id,
    Guid? ActorUserId,
    string ActorDisplayName,
    string ActorUsername,
    string Action,
    string EntityType,
    string? EntityId,
    string Description,
    DateTime CreatedAt,
    string? EntityDisplayName = null,
    string? EntityUsername = null,
    Guid? CommunityId = null,
    string Category = SystemAuditCategories.Other,
    string? MetadataJson = null);

public sealed record SystemAuditContext(
    Guid? CommunityId = null,
    object? Metadata = null,
    string? Category = null);

public record SystemAuditSearchRequest(
    string? Query = null,
    string? Category = null,
    int Page = 1,
    int PageSize = 10,
    string SortBy = "createdAt",
    string SortDirection = "desc");

public record SystemAuditCategoryCountsDto(
    int All,
    int Identity,
    int Access,
    int Forms,
    int Processes,
    int Tasks);
