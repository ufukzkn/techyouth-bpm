namespace TechYouthBpm.Domain.Entities;

public class ArchivedAuditEvent
{
    public Guid Id { get; set; }
    public Guid CommunityDeletionArchiveId { get; set; }
    public CommunityDeletionArchive? CommunityDeletionArchive { get; set; }
    public Guid? OriginalEventId { get; set; }
    public string Source { get; set; } = "system";
    public string Category { get; set; } = "other";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = "System";
    public string ActorUsername { get; set; } = "system";
    public string? EntityDisplayName { get; set; }
    public string? EntityUsername { get; set; }
    public string Description { get; set; } = string.Empty;
    public string NodeTitle { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string CommunityRoleName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
