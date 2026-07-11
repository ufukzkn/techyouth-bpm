namespace TechYouthBpm.Domain.Entities;

public class CommunityRolePermission
{
    public Guid Id { get; set; }
    public Guid CommunityRoleId { get; set; }
    public CommunityRole? CommunityRole { get; set; }
    public string Permission { get; set; } = string.Empty;
}
