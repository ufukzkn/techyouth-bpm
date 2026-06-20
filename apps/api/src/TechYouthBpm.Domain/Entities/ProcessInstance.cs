using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class ProcessInstance
{
    public Guid Id { get; set; }
    public Guid FormDefinitionId { get; set; }
    public FormDefinition? FormDefinition { get; set; }
    public Guid StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public ProcessStatus Status { get; set; }
    public string FormDataJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ProcessTask> Tasks { get; set; } = [];
    public List<AuditLog> AuditLogs { get; set; } = [];
}
