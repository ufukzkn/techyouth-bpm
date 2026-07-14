using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IAuthenticationService
{
    Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task<Result<LoginResponse>> RefreshSessionAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default);
}
