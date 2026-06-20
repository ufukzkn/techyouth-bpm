using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public ProcessInstance? ProcessInstance { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public WorkflowAction Action { get; set; }
    public ProcessStatus FromStatus { get; set; }
    public ProcessStatus ToStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Note { get; set; } = string.Empty;
}
