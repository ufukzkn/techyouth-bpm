using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Infrastructure.Services;

public class RoutingEmailSender(IConfiguration configuration) : IEmailSender
{
    private readonly SmtpEmailSender primarySender = new(configuration);
    private readonly SmtpEmailSender sandboxSender = new(configuration, "Email:Sandbox");

    public bool ExposesVerificationCode => false;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await primarySender.SendAsync(message, cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsAllowlistFailure(exception) && IsSandboxConfigured())
        {
            await sandboxSender.SendAsync(message, cancellationToken);
        }
    }

    private static bool IsAllowlistFailure(InvalidOperationException exception)
    {
        return exception.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSandboxConfigured()
    {
        return !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:Smtp:Host"])
            && !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:Smtp:Username"])
            && !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:Smtp:Password"])
            && !string.IsNullOrWhiteSpace(configuration["Email:Sandbox:FromAddress"]);
    }
}
