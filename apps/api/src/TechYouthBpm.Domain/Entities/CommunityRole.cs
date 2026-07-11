namespace TechYouthBpm.Domain.Entities;

public class CommunityRole
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CommunityRolePermission> Permissions { get; set; } = [];
}
