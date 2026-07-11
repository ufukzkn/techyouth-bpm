using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class ProcessTask
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance? ProcessInstance { get; set; }
    // Compatibility-only database field. Authorization and public task DTOs use
    // AssignedCommunityRoleId plus RequiredPermission instead.
    public Role AssignedRole { get; set; }
    public Guid? AssignedCommunityRoleId { get; set; }
    public CommunityRole? AssignedCommunityRole { get; set; }
    public string RequiredPermission { get; set; } = "Tasks.Act";
    public ProcessTaskStatus Status { get; set; }
    public string AvailableActionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
}
