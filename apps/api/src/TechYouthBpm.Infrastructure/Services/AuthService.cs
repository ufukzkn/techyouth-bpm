using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class AuthService(AppDbContext db, IConfiguration configuration) : IAuthService
{
    private const int FallbackSessionDurationMinutes = 1;

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Username == request.Username, cancellationToken);

        if (user is null || user.Password != request.Password)
        {
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        var session = new UserSession
        {
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", string.Empty).Replace("+", string.Empty),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes())
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(session.Token, user.ToDto(), session.ExpiresAt));
    }

    public async Task<UserDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await db.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Token == token && item.ExpiresAt > DateTime.UtcNow, cancellationToken);

        return session?.User?.ToDto();
    }

    private int GetSessionDurationMinutes()
    {
        var configuredDuration = configuration["Auth:SessionDurationMinutes"];
        return int.TryParse(configuredDuration, out var minutes) && minutes > 0
            ? minutes
            : FallbackSessionDurationMinutes;
    }
}
