using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class OtpServiceTests
{
    [Fact]
    public void IssueEmailVerificationCode_Stores_Hashed_Code()
    {
        var user = new User { Email = "user@test.local" };
        var service = new OtpService();

        var result = service.IssueEmailVerificationCode(user, 10);

        Assert.Equal(6, result.DemoCode.Length);
        Assert.NotEqual(result.DemoCode, user.EmailVerificationCode);
        Assert.StartsWith("pbkdf2:v1:", user.EmailVerificationCode, StringComparison.Ordinal);
        Assert.True(user.EmailVerificationCodeExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void VerifyEmailVerificationCode_Accepts_Issued_Code()
    {
        var user = new User { Email = "user@test.local" };
        var service = new OtpService();
        var result = service.IssueEmailVerificationCode(user, 10);

        var verification = service.VerifyEmailVerificationCode(user, result.DemoCode);

        Assert.True(verification.IsSuccess);
    }

    [Fact]
    public void VerifyEmailVerificationCode_Rejects_Wrong_Code()
    {
        var user = new User { Email = "user@test.local" };
        var service = new OtpService();
        service.IssueEmailVerificationCode(user, 10);

        var verification = service.VerifyEmailVerificationCode(user, "000000");

        Assert.False(verification.IsSuccess);
        Assert.Contains("incorrect", verification.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyEmailVerificationCode_Rejects_Expired_Code()
    {
        var user = new User { Email = "user@test.local" };
        var service = new OtpService();
        service.IssueEmailVerificationCode(user, 10);
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var verification = service.VerifyEmailVerificationCode(user, "000000");

        Assert.False(verification.IsSuccess);
        Assert.Contains("expired", verification.Errors[0], StringComparison.OrdinalIgnoreCase);
    }
}
