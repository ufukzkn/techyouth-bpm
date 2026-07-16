using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class ProcessDefinitionVersion
{
    public Guid Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public ProcessDefinition? ProcessDefinition { get; set; }
    public int VersionNumber { get; set; }
    public DefinitionVersionStatus Status { get; set; } = DefinitionVersionStatus.Draft;
    public Guid FormDefinitionVersionId { get; set; }
    public FormDefinitionVersion? FormDefinitionVersion { get; set; }
    public string GraphJson { get; set; } = "{}";
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public User? PublishedByUser { get; set; }
    public DateTime? PublishedAt { get; set; }
}
