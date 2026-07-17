namespace TechYouthBpm.Domain.Entities;

public class SystemAuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }
    public Guid? CommunityId { get; set; }
    public Community? Community { get; set; }
    public string Category { get; set; } = "other";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
