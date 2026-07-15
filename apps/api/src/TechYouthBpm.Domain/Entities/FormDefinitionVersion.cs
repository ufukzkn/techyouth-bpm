using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class FormDefinitionVersion
{
    public Guid Id { get; set; }
    public Guid FormDefinitionId { get; set; }
    public FormDefinition? FormDefinition { get; set; }
    public int VersionNumber { get; set; }
    public DefinitionVersionStatus Status { get; set; } = DefinitionVersionStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public User? PublishedByUser { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<FormPageDefinition> Pages { get; set; } = [];
}
