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
        if (ShouldUseSandbox(message.To) && IsSandboxConfigured())
        {
            await sandboxSender.SendAsync(message, cancellationToken);
            return;
        }

        try
        {
            await primarySender.SendAsync(message, cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsAllowlistFailure(exception) && IsSandboxConfigured())
        {
            await sandboxSender.SendAsync(message, cancellationToken);
        }
    }

    private bool ShouldUseSandbox(string recipient)
    {
        var sandboxDomains = GetCsv("Email:SandboxDomains");
        if (sandboxDomains.Count == 0)
        {
            sandboxDomains = ["@techyouth.local"];
        }

        return sandboxDomains.Any(domain =>
            recipient.Trim().EndsWith(domain.Trim(), StringComparison.OrdinalIgnoreCase));
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

    private IReadOnlyList<string> GetCsv(string key)
    {
        var configuredValue = configuration[key];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return [];
        }

        return configuredValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
