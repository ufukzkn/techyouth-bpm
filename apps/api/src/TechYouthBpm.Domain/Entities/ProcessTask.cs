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
    public string NodeKey { get; set; } = string.Empty;
    public int Attempt { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskAssignmentType? AssignmentType { get; set; }
    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }
    public Guid? CandidateTeamId { get; set; }
    public Team? CandidateTeam { get; set; }
    public Guid? CandidateCommunityRoleId { get; set; }
    public CommunityRole? CandidateCommunityRole { get; set; }
    public Guid? ClaimedByUserId { get; set; }
    public User? ClaimedByUser { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public Guid ClaimVersion { get; set; } = Guid.NewGuid();
    public Guid? FormDefinitionVersionId { get; set; }
    public FormDefinitionVersion? FormDefinitionVersion { get; set; }
    public string RequiredPermission { get; set; } = "Tasks.Act";
    public ProcessTaskStatus Status { get; set; }
    public string AvailableActionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool RequiresTeamLead { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public WorkflowAction? CompletedAction { get; set; }
    public string CompletionNote { get; set; } = string.Empty;
}
