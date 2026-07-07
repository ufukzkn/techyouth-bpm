using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Infrastructure.Services;

public class DemoEmailSender : IEmailSender
{
    public bool ExposesVerificationCode => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
