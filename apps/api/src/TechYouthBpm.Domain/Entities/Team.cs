namespace TechYouthBpm.Domain.Entities;

public class Team
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<TeamMembership> Memberships { get; set; } = [];
}
