using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Auth;

public record LoginRequest(string Username, string Password, bool RememberMe = false);

public record RegisterRequest(string Username, string DisplayName, string Email, string Password);

public record UpdateProfileRequest(string DisplayName, string Email);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CreateUserRequest(
    string Username,
    string DisplayName,
    string Email,
    Role Role,
    UserStatus Status,
    string TemporaryPassword);

public record UserDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    Role Role,
    UserStatus Status,
    bool IsEmailVerified,
    bool MustChangePassword = false)
{
    public UserDto(Guid id, string username, string displayName, Role role)
        : this(id, username, displayName, string.Empty, role, UserStatus.Active, true)
    {
    }
}

public record LoginResponse(string Token, UserDto User, DateTime ExpiresAt);

public record RegisterResponse(Guid Id, string Username, string Email, UserStatus Status);

public record UserAdminDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    Role Role,
    UserStatus Status,
    bool IsEmailVerified,
    int FailedLoginCount,
    DateTime? LockedUntil,
    DateTime CreatedAt,
    bool MustChangePassword = false);

public record UpdateUserAccessRequest(Role Role, UserStatus Status);

public record UserSessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    bool IsCurrent,
    string? IpAddress = null,
    string? UserAgent = null);

public record EmailVerificationStartResponse(string Message, string DemoCode, DateTime ExpiresAt);

public record EmailVerificationConfirmRequest(string Code);
