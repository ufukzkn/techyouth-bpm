using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

internal sealed class AuthenticationService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender,
    AuthenticatedUserLoader authenticatedUserLoader) : AuthServiceBase(db, configuration, auditService, otpService, emailSender), IAuthenticationService
{
    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery()
            .SingleOrDefaultAsync(item => item.Username == request.Username, cancellationToken);

        if (user is null)
        {
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure("Account is temporarily locked after multiple failed login attempts.");
        }

        if (!PasswordMatches(request.Password, user.Password))
        {
            user.FailedLoginCount += 1;

            if (user.FailedLoginCount >= GetInt("Auth:MaxFailedLoginAttempts", DefaultMaxFailedLoginAttempts))
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(GetInt("Auth:LockoutMinutes", DefaultLockoutMinutes));
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user.Id,
                user.LockedUntil is null ? "Auth.LoginFailed" : "Auth.AccountLocked",
                "User",
                user.Id.ToString(),
                user.LockedUntil is null
                    ? $"Failed login attempt for '{user.Username}'."
                    : $"User '{user.Username}' was temporarily locked after failed login attempts.",
                cancellationToken);
            return Result<LoginResponse>.Failure("Username or password is incorrect.");
        }

        if (user.Status != UserStatus.Active)
        {
            return Result<LoginResponse>.Failure(user.Status == UserStatus.PendingApproval
                ? "Account is waiting for admin approval."
                : "Account is not active.");
        }

        if (!HasActiveCommunityAccess(user))
        {
            return Result<LoginResponse>.Failure("The user's community is not active.");
        }

        if (!PasswordHasher.IsHashed(user.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        var rawToken = SessionTokenHasher.CreateToken();
        var csrfToken = SessionTokenHasher.CreateToken();
        var rawRefreshToken = request.RememberMe ? SessionTokenHasher.CreateToken() : string.Empty;
        var refreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetRefreshTokenDurationMinutes());
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(rememberMe: false)),
            IpAddress = TrimOrNull(ipAddress, 128),
            UserAgent = TrimOrNull(userAgent, 512),
            RememberedDevice = request.RememberMe
        };

        db.UserSessions.Add(session);
        if (request.RememberMe)
        {
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = SessionTokenHasher.Hash(rawRefreshToken),
                UserId = user.Id,
                UserSessionId = session.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = refreshTokenExpiresAt,
                CreatedByIpAddress = TrimOrNull(ipAddress, 128),
                CreatedByUserAgent = TrimOrNull(userAgent, 512)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.LoginSucceeded",
            "Session",
            session.Id.ToString(),
            $"User '{user.Username}' signed in.",
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            rawToken,
            user.ToDto(),
            session.ExpiresAt,
            csrfToken,
            rawRefreshToken,
            request.RememberMe ? refreshTokenExpiresAt : null));
    }

    public async Task<Result<LoginResponse>> RefreshSessionAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponse>.Failure("A valid refresh token is required.");
        }

        var refreshTokenHash = SessionTokenHasher.Hash(refreshToken);
        var storedRefreshToken = await db.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(token => token.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .Include(token => token.User)
            .ThenInclude(user => user!.TeamMemberships)
            .ThenInclude(teamMembership => teamMembership.Team)
            .Include(token => token.UserSession)
            .SingleOrDefaultAsync(token => token.Token == refreshTokenHash, cancellationToken);

        if (storedRefreshToken is null)
        {
            return Result<LoginResponse>.Failure("A valid refresh token is required.");
        }

        if (storedRefreshToken.RevokedAt is not null)
        {
            await RevokeAllSessionsForUserAsync(storedRefreshToken.UserId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                storedRefreshToken.UserId,
                "Auth.RefreshReuseDetected",
                "Session",
                storedRefreshToken.UserSessionId.ToString(),
                "A revoked refresh token was reused; active sessions were revoked.",
                cancellationToken);
            return Result<LoginResponse>.Failure("Refresh session is no longer valid.");
        }

        if (storedRefreshToken.ExpiresAt <= DateTime.UtcNow
            || storedRefreshToken.User is null
            || storedRefreshToken.UserSession is null
            || storedRefreshToken.User.Status != UserStatus.Active
            || !HasActiveCommunityAccess(storedRefreshToken.User))
        {
            storedRefreshToken.RevokedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Result<LoginResponse>.Failure("Refresh session is no longer valid.");
        }

        var user = storedRefreshToken.User;
        var previousSession = storedRefreshToken.UserSession;
        previousSession.RevokedAt = DateTime.UtcNow;
        storedRefreshToken.RevokedAt = DateTime.UtcNow;

        var rawToken = SessionTokenHasher.CreateToken();
        var rawRefreshToken = SessionTokenHasher.CreateToken();
        var csrfToken = SessionTokenHasher.CreateToken();
        var refreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetRefreshTokenDurationMinutes());
        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(rememberMe: false)),
            IpAddress = TrimOrNull(ipAddress, 128),
            UserAgent = TrimOrNull(userAgent, 512),
            RememberedDevice = true
        };
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawRefreshToken),
            UserId = user.Id,
            UserSessionId = newSession.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshTokenExpiresAt,
            CreatedByIpAddress = TrimOrNull(ipAddress, 128),
            CreatedByUserAgent = TrimOrNull(userAgent, 512)
        };
        storedRefreshToken.ReplacedByRefreshTokenId = newRefreshToken.Id;
        db.UserSessions.Add(newSession);
        db.RefreshTokens.Add(newRefreshToken);

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.Refresh",
            "Session",
            newSession.Id.ToString(),
            $"Remembered session for '{user.Username}' was refreshed.",
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            rawToken,
            user.ToDto(),
            newSession.ExpiresAt,
            csrfToken,
            rawRefreshToken,
            refreshTokenExpiresAt));
    }


    public Task<UserDto?> GetUserByTokenAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        authenticatedUserLoader.LoadAsync(token, cancellationToken);
}
