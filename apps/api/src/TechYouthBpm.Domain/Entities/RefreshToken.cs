namespace TechYouthBpm.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid UserSessionId { get; set; }
    public UserSession? UserSession { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByRefreshTokenId { get; set; }
    public string? CreatedByIpAddress { get; set; }
    public string? CreatedByUserAgent { get; set; }
}
