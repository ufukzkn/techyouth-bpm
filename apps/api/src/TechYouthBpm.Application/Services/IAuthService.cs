using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IAuthService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserDto>> ChangePasswordAsync(
        ChangePasswordRequest request,
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
    Task<Result> DeleteUserAsync(
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserSessionDto>>> ListSessionsAsync(
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserSessionDto>>> ListUserSessionsAsync(
        Guid userId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string token, CancellationToken cancellationToken = default);
    Task<Result> RevokeSessionAsync(
        Guid sessionId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default);
    Task<Result> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<EmailVerificationStartResponse>> StartEmailVerificationAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserDto>> ConfirmEmailVerificationAsync(
        EmailVerificationConfirmRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
