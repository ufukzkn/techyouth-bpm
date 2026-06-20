namespace TechYouthBpm.Domain.Entities;

public class UserSession
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
