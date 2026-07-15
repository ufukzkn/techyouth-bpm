using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class ProcessInstance
{
    public Guid Id { get; set; }
    public Guid FormDefinitionId { get; set; }
    public FormDefinition? FormDefinition { get; set; }
    public Guid? FormDefinitionVersionId { get; set; }
    public FormDefinitionVersion? FormDefinitionVersion { get; set; }
    public Guid? ProcessDefinitionVersionId { get; set; }
    public ProcessDefinitionVersion? ProcessDefinitionVersion { get; set; }
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public Guid StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public ProcessStatus Status { get; set; }
    public string FormDataJson { get; set; } = "{}";
    public string VariablesJson { get; set; } = "{}";
    public string CurrentNodeKey { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ProcessTask> Tasks { get; set; } = [];
    public List<AuditLog> AuditLogs { get; set; } = [];
    public List<ProcessStepExecution> StepExecutions { get; set; } = [];
}
