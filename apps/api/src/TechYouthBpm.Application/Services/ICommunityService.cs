using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface ICommunityService
{
    Task<Result<IReadOnlyList<CommunityDto>>> ListAsync(UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<CommunityDto>> CreateAsync(CreateCommunityRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<CommunityDto>> UpdateAsync(Guid communityId, UpdateCommunityRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RoleTemplateDto>>> ListRoleTemplatesAsync(UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CommunityRoleDto>>> ListRolesAsync(Guid communityId, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<CommunityRoleDto>> CreateRoleAsync(Guid communityId, CreateCommunityRoleRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<CommunityRoleDto>> UpdateRoleAsync(Guid communityId, Guid roleId, UpdateCommunityRoleRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserAdminDto>>> ListUsersAsync(Guid communityId, UserDto currentUser, UserSearchRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> CreateUserAsync(Guid communityId, CreateUserRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> UpdateMembershipAsync(Guid communityId, Guid userId, UpdateUserMembershipRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
}
