namespace TechYouthBpm.Domain.Entities;

public class TeamMembership
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public bool IsLead { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
