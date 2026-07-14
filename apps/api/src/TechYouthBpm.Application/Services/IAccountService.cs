using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IAccountService
{
    Task<Result<UserDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserDto>> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<EmailVerificationStartResponse>> StartEmailVerificationAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default);
    Task<Result<UserDto>> ConfirmEmailVerificationAsync(
        EmailVerificationConfirmRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
