using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class AuthService(AppDbContext db) : IAuthService
{
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
            ExpiresAt = DateTime.UtcNow.AddHours(8)
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
}
