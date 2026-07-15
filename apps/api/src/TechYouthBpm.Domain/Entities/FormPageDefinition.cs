namespace TechYouthBpm.Domain.Entities;

public class FormPageDefinition
{
    public Guid Id { get; set; }
    public Guid FormDefinitionVersionId { get; set; }
    public FormDefinitionVersion? FormDefinitionVersion { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<FormVersionFieldDefinition> Fields { get; set; } = [];
}
