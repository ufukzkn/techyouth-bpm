using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IRegistrationService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<EmailVerificationStartResponse>> StartPublicEmailVerificationAsync(
        PublicEmailVerificationStartRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<RegisterResponse>> ConfirmPublicEmailVerificationAsync(
        PublicEmailVerificationConfirmRequest request,
        CancellationToken cancellationToken = default);
}
