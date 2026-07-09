namespace TechYouthBpm.Application.Auth;

public record CommunityDto(
    Guid Id,
    string Name,
    string Description,
    bool IsActive,
    DateTime CreatedAt);

public record CommunityRoleDto(
    Guid Id,
    Guid CommunityId,
    string Name,
    string Description,
    string TemplateKey,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions);

public record CreateCommunityRequest(string Name, string Description, bool IsActive = true);

public record UpdateCommunityRequest(string Name, string Description, bool IsActive = true);

public record CreateCommunityRoleRequest(
    string Name,
    string Description,
    string TemplateKey,
    IReadOnlyList<string> Permissions);

public record UpdateCommunityRoleRequest(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);

public record UpdateUserMembershipRequest(Guid CommunityId, Guid CommunityRoleId, bool IsActive = true);

public record RoleTemplateDto(string Key, string Name, string Description, IReadOnlyList<string> Permissions);
