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
    DateTime CreatedAt);
