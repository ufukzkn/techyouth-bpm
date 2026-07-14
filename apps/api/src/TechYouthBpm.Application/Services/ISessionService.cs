using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface ISessionService
{
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
}
