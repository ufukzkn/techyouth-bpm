using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Domain.Entities;

public class FieldValidationRule
{
    public Guid Id { get; set; }
    public Guid FormFieldDefinitionId { get; set; }
    public FormFieldDefinition? Field { get; set; }
    public ValidationRuleType RuleType { get; set; }
    public string DependsOnFieldKey { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
