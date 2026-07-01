using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

public class AuthService(AppDbContext db, IConfiguration configuration) : IAuthService
{
    private const int FallbackSessionDurationMinutes = 1;

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Username == request.Username, cancellationToken);

        if (user is null || !PasswordMatches(request.Password, user.Password))
        {
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        if (!PasswordHasher.IsHashed(user.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
        }

        var rawToken = SessionTokenHasher.CreateToken();
        var session = new UserSession
        {
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(request.RememberMe))
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(rawToken, user.ToDto(), session.ExpiresAt));
    }

    public async Task<UserDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = SessionTokenHasher.Hash(token);
        var session = await db.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Token == tokenHash && item.ExpiresAt > DateTime.UtcNow, cancellationToken);

        return session?.User?.ToDto();
    }

    private static bool PasswordMatches(string password, string storedPassword) =>
        PasswordHasher.IsHashed(storedPassword)
            ? PasswordHasher.Verify(password, storedPassword)
            : string.Equals(password, storedPassword, StringComparison.Ordinal);

    private int GetSessionDurationMinutes(bool rememberMe)
    {
        var configuredDuration = rememberMe
            ? configuration["Auth:RememberMeDurationMinutes"]
            : configuration["Auth:SessionDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : FallbackSessionDurationMinutes;
    }
}
