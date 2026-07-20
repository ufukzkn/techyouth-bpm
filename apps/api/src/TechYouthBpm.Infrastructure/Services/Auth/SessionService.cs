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

internal sealed class SessionService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService,
    IOtpService otpService,
    IEmailSender emailSender) : AuthServiceBase(db, configuration, auditService, otpService, emailSender), ISessionService
{
    public async Task<Result<IReadOnlyList<UserSessionDto>>> ListSessionsAsync(
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        var currentTokenHash = SessionTokenHasher.Hash(currentToken);
        var sessions = await db.UserSessions
            .Where(session => session.UserId == currentUser.Id && session.RevokedAt == null && session.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new UserSessionDto(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastSeenAt,
                session.Token == currentTokenHash,
                session.IpAddress,
                session.UserAgent,
                session.RememberedDevice))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserSessionDto>>.Success(sessions);
    }

    public async Task<Result<IReadOnlyList<UserSessionDto>>> ListUserSessionsAsync(
        Guid userId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, null))
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("Community management permission is required to view user sessions.");
        }

        var managedUser = await UserQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (managedUser is null)
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("User not found.");
        }

        if (!CanManageUsers(currentUser, managedUser.ToDto().CommunityId))
        {
            return Result<IReadOnlyList<UserSessionDto>>.Failure("Current user cannot view sessions in this community.");
        }

        var currentTokenHash = SessionTokenHasher.Hash(currentToken);
        var sessions = await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(session => session.LastSeenAt ?? session.CreatedAt)
            .Select(session => new UserSessionDto(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastSeenAt,
                session.Token == currentTokenHash,
                session.IpAddress,
                session.UserAgent,
                session.RememberedDevice))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserSessionDto>>.Success(sessions);
    }

    public async Task<Result> LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure("A valid session token is required.");
        }

        var tokenHash = SessionTokenHasher.Hash(token);
        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Token == tokenHash && item.RevokedAt == null,
            cancellationToken);

        if (session is not null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                session.UserId,
                "Auth.Logout",
                "Session",
                session.Id.ToString(),
                "User session was revoked by logout.",
                cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeSessionAsync(
        Guid sessionId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == currentUser.Id && item.RevokedAt == null,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure("Session not found.");
        }

        session.RevokedAt = DateTime.UtcNow;
        await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.SessionRevoked",
            "Session",
            session.Id.ToString(),
            "User revoked an active session.",
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageUsers(currentUser, null))
        {
            return Result.Failure("Community management permission is required to revoke user sessions.");
        }

        var managedUser = await UserQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (managedUser is null)
        {
            return Result.Failure("User not found.");
        }

        if (!CanManageUsers(currentUser, managedUser.ToDto().CommunityId))
        {
            return Result.Failure("Current user cannot revoke sessions in this community.");
        }

        var session = await db.UserSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId && item.RevokedAt == null,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure("Session not found.");
        }

        session.RevokedAt = DateTime.UtcNow;
        await RevokeRefreshTokensForSessionAsync(session.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.AdminSessionRevoked",
            "Session",
            session.Id.ToString(),
            $"Admin '{currentUser.Username}' revoked a session for user '{userId}'.",
            cancellationToken);
        return Result.Success();
    }

}
