using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class ProcessStepExecution
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance? ProcessInstance { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string NodeTitle { get; set; } = string.Empty;
    public ProcessNodeType NodeType { get; set; }
    public TaskAssignmentType? AssignmentType { get; set; }
    public string TeamNameSnapshot { get; set; } = string.Empty;
    public string CommunityRoleNameSnapshot { get; set; } = string.Empty;
    public string AssignedUserNameSnapshot { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public ProcessStepStatus Status { get; set; }
    public DateTime EnteredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public WorkflowAction? Action { get; set; }
    public string Note { get; set; } = string.Empty;
    public string OutputJson { get; set; } = "{}";
}
