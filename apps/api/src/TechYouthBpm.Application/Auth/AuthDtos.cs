using System.Text.Json.Serialization;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Auth;

public record LoginRequest(string Username, string Password, bool RememberMe = false);

public record RegisterRequest(string Username, string DisplayName, string Email, string Password);

public record UpdateProfileRequest(string DisplayName, string Email);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string UsernameOrEmail);

public record ForgotPasswordResponse(string Message, string DemoToken = "", DateTime? ExpiresAt = null);

public record ResetPasswordRequest(string UsernameOrEmail, string Token, string NewPassword);

public record PublicEmailVerificationStartRequest(string UsernameOrEmail);

public record PublicEmailVerificationConfirmRequest(string UsernameOrEmail, string Code);

public record CreateUserRequest(
    string Username,
    string DisplayName,
    string Email,
    Role Role,
    UserStatus Status,
    string TemporaryPassword);

public record UserSearchRequest(
    string? Query = null,
    UserStatus? Status = null,
    int Page = 1,
    int PageSize = 10);

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

public record LoginResponse(
    string Token,
    UserDto User,
    DateTime ExpiresAt,
    string CsrfToken = "",
    [property: JsonIgnore] string RefreshToken = "",
    [property: JsonIgnore] DateTime? RefreshTokenExpiresAt = null);

public record RefreshSessionRequest(string CsrfToken = "");

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
    string? UserAgent = null,
    bool RememberedDevice = false);

public record EmailVerificationStartResponse(string Message, string DemoCode, DateTime ExpiresAt);

public record EmailVerificationConfirmRequest(string Code);
