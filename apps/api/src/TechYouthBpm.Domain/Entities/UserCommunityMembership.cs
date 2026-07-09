namespace TechYouthBpm.Domain.Entities;

public class UserCommunityMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public Guid CommunityRoleId { get; set; }
    public CommunityRole? CommunityRole { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
