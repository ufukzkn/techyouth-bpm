namespace TechYouthBpm.Domain.Entities;

public class FormDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<FormFieldDefinition> Fields { get; set; } = [];
    public List<FormDefinitionVersion> Versions { get; set; } = [];
}
