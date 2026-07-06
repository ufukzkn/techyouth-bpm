using System.Security.Cryptography;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

public class OtpService : IOtpService
{
    public OtpIssueResult IssueEmailVerificationCode(User user, int validMinutes)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(validMinutes);

        user.EmailVerificationCode = PasswordHasher.Hash(code);
        user.EmailVerificationCodeExpiresAt = expiresAt;

        return new OtpIssueResult(code, expiresAt);
    }

    public Result VerifyEmailVerificationCode(User user, string code)
    {
        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt <= DateTime.UtcNow)
        {
            return Result.Failure("Verification code expired.");
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode)
            || !PasswordHasher.Verify(code.Trim(), user.EmailVerificationCode))
        {
            return Result.Failure("Verification code is incorrect.");
        }

        return Result.Success();
    }
}
