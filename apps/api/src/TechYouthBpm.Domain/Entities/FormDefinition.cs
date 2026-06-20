namespace TechYouthBpm.Domain.Entities;

public class FormDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FormFieldDefinition> Fields { get; set; } = [];
}
