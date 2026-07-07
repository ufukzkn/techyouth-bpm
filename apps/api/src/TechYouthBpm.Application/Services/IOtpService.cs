using TechYouthBpm.Application.Common;
using TechYouthBpm.Domain.Entities;

namespace TechYouthBpm.Application.Services;

public record OtpIssueResult(string DemoCode, DateTime ExpiresAt);

public interface IOtpService
{
    OtpIssueResult IssueEmailVerificationCode(User user, int validMinutes);
    Result VerifyEmailVerificationCode(User user, string code);
}
