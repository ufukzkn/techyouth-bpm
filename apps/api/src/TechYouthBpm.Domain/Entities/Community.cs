namespace TechYouthBpm.Domain.Entities;

public class Community
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CommunityRole> Roles { get; set; } = [];
}
