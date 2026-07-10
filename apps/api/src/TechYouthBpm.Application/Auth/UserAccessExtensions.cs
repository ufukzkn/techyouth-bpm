using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Auth;

public static class UserAccessExtensions
{
    public static bool IsSuperAdmin(this UserDto user) => user.Role == Role.SuperAdmin;

    public static bool HasPermission(this UserDto user, string permission) =>
        user.IsSuperAdmin()
        || (user.Permissions ?? []).Contains(permission, StringComparer.OrdinalIgnoreCase);

    public static bool SharesCommunityWith(this UserDto user, Guid? communityId) =>
        user.IsSuperAdmin()
        || communityId is null
        || (user.CommunityId is not null && user.CommunityId == communityId);
}
