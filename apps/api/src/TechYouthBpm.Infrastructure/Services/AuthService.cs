using System.Security.Cryptography;
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

public class AuthService(
    AppDbContext db,
    IConfiguration configuration,
    ISystemAuditService auditService) : IAuthService
{
    private const int FallbackSessionDurationMinutes = 1;
    private const int DefaultMaxFailedLoginAttempts = 5;
    private const int DefaultLockoutMinutes = 10;
    private const int DefaultEmailVerificationMinutes = 10;

    public AuthService(AppDbContext db, IConfiguration configuration)
        : this(db, configuration, new SystemAuditService(db))
    {
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }

        if (errors.Count > 0)
        {
            return Result<RegisterResponse>.Failure(errors);
        }

        var exists = await db.Users.AnyAsync(
            user => user.Username == username || user.Email == email,
            cancellationToken);
        if (exists)
        {
            return Result<RegisterResponse>.Failure("Username or email is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Email = email,
            Password = PasswordHasher.Hash(request.Password),
            Role = Role.User,
            Status = UserStatus.PendingApproval,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.RegisterRequested",
            "User",
            user.Id.ToString(),
            $"User '{user.Username}' registered and is waiting for admin approval.",
            cancellationToken);

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Username, user.Email, user.Status));
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
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

        if (!PasswordHasher.IsHashed(user.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        var rawToken = SessionTokenHasher.CreateToken();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = SessionTokenHasher.Hash(rawToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetSessionDurationMinutes(request.RememberMe))
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user.Id,
            "Auth.LoginSucceeded",
            "Session",
            session.Id.ToString(),
            $"User '{user.Username}' signed in.",
            cancellationToken);

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
            .SingleOrDefaultAsync(
                item => item.Token == tokenHash && item.ExpiresAt > DateTime.UtcNow && item.RevokedAt == null,
                cancellationToken);

        if (session?.User is null || session.User.Status != UserStatus.Active)
        {
            return null;
        }

        session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return session.User.ToDto();
    }

    public async Task<Result<IReadOnlyList<UserAdminDto>>> ListUsersAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != Role.Admin)
        {
            return Result<IReadOnlyList<UserAdminDto>>.Failure("Only Admin users can list users.");
        }

        var users = await db.Users
            .OrderBy(user => user.Status)
            .ThenBy(user => user.Username)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserAdminDto>>.Success(users.Select(user => user.ToAdminDto()).ToArray());
    }

    public async Task<Result<UserAdminDto>> UpdateUserAccessAsync(
        Guid userId,
        UpdateUserAccessRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != Role.Admin)
        {
            return Result<UserAdminDto>.Failure("Only Admin users can update access.");
        }

        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure("User not found.");
        }

        var oldStatus = user.Status;
        var oldRole = user.Role;
        user.Role = request.Role;
        user.Status = request.Status;

        if (request.Status != UserStatus.Active)
        {
            await RevokeAllSessionsForUserAsync(user.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "User.AccessUpdated",
            "User",
            user.Id.ToString(),
            $"User '{user.Username}' access changed from {oldRole}/{oldStatus} to {user.Role}/{user.Status}.",
            cancellationToken);
        return Result<UserAdminDto>.Success(user.ToAdminDto());
    }

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
                session.Token == currentTokenHash))
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

    public async Task<Result<EmailVerificationStartResponse>> StartEmailVerificationAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<EmailVerificationStartResponse>.Failure("User not found.");
        }

        if (user.IsEmailVerified)
        {
            return Result<EmailVerificationStartResponse>.Failure("Email is already verified.");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(GetInt("Auth:EmailVerificationMinutes", DefaultEmailVerificationMinutes));

        user.EmailVerificationCode = PasswordHasher.Hash(code);
        user.EmailVerificationCodeExpiresAt = expiresAt;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.EmailVerificationRequested",
            "User",
            user.Id.ToString(),
            $"Email verification requested for '{user.Email}'.",
            cancellationToken);

        return Result<EmailVerificationStartResponse>.Success(new EmailVerificationStartResponse(
            "Verification code generated for local demo. A production flow should send this code by email.",
            code,
            expiresAt));
    }

    public async Task<Result<UserDto>> ConfirmEmailVerificationAsync(
        EmailVerificationConfirmRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == currentUser.Id, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        if (user.IsEmailVerified)
        {
            return Result<UserDto>.Success(user.ToDto());
        }

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt <= DateTime.UtcNow)
        {
            return Result<UserDto>.Failure("Verification code expired.");
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode)
            || !PasswordHasher.Verify(request.Code.Trim(), user.EmailVerificationCode))
        {
            return Result<UserDto>.Failure("Verification code is incorrect.");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "Auth.EmailVerified",
            "User",
            user.Id.ToString(),
            $"Email '{user.Email}' was verified.",
            cancellationToken);

        return Result<UserDto>.Success(user.ToDto());
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

    private int GetInt(string key, int fallback)
    {
        var configuredValue = configuration[key];
        return int.TryParse(configuredValue, out var value) && value > 0 ? value : fallback;
    }

    private async Task RevokeAllSessionsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAt = DateTime.UtcNow;
        }
    }
}
