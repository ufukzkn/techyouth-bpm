using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IUserAdministrationService
{
    Task<Result<AdminPasswordResetResponse>> ResetPasswordByAdminAsync(
        Guid userId,
        AdminPasswordResetRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> CreateUserAsync(
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserAdminDto>>> ListUsersAsync(
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> UpdateUserAccessAsync(
        Guid userId,
        UpdateUserAccessRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<CommunityTransferPreviewDto>> PreviewCommunityTransferAsync(
        Guid userId,
        CommunityTransferPreviewRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> TransferCommunityAsync(
        Guid userId,
        CommunityTransferRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserAdminDto>>> ListCommunityUsersAsync(
        Guid communityId,
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> CreateCommunityUserAsync(
        Guid communityId,
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> UpdateCommunityMembershipAsync(
        Guid communityId,
        Guid userId,
        UpdateUserMembershipRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
