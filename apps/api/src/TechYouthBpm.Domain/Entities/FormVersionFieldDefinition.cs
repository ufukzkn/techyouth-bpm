using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class FormVersionFieldDefinition
{
    public Guid Id { get; set; }
    public Guid FormPageDefinitionId { get; set; }
    public FormPageDefinition? FormPageDefinition { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public List<FormVersionFieldValidationRule> ValidationRules { get; set; } = [];
}
