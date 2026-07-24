namespace TechYouthBpm.Domain.Entities;

public class CommunityDeletionArchive
{
    public Guid Id { get; set; }
    public Guid OriginalCommunityId { get; set; }
    public string CommunityName { get; set; } = string.Empty;
    public Guid DeletedByUserId { get; set; }
    public string DeletedByUsername { get; set; } = string.Empty;
    public string DeletedByDisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
    public int UserCount { get; set; }
    public int PreservedUserCount { get; set; }
    public int CommunityRoleCount { get; set; }
    public int TeamCount { get; set; }
    public int FormCount { get; set; }
    public int WorkflowCount { get; set; }
    public int ProcessCount { get; set; }
    public int TaskCount { get; set; }
    public int NotificationCount { get; set; }
    public int SystemAuditCount { get; set; }
    public int ProcessStepCount { get; set; }
    public List<ArchivedAuditEvent> Events { get; set; } = [];
}
