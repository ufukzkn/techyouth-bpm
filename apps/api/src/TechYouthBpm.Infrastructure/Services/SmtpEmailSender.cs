using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Infrastructure.Services;

public class SmtpEmailSender(IConfiguration configuration, string sectionPrefix = "Email") : IEmailSender
{
    public bool ExposesVerificationCode => false;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EnsureAllowedRecipient(message);

        var host = Required("Smtp:Host");
        var username = Required("Smtp:Username");
        var password = Required("Smtp:Password");
        var fromAddress = Required("FromAddress");
        var fromName = configuration[Key("FromName")] ?? "TechYouth BPM";
        var port = GetInt("Smtp:Port", 587);
        var enableSsl = GetBool("Smtp:EnableSsl", true);

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml
        };
        mailMessage.To.Add(message.To);

        using var smtpClient = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl
        };

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }

    private void EnsureAllowedRecipient(EmailMessage message)
    {
        var allowedRecipients = GetCsv("AllowedRecipients");
        if (allowedRecipients.Count > 0
            && !allowedRecipients.Contains(message.To.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email recipient is not allowed for SMTP delivery.");
        }

        var allowedUsernames = GetCsv("AllowedUsernames");
        if (allowedUsernames.Count > 0
            && (string.IsNullOrWhiteSpace(message.RecipientUsername)
                || !allowedUsernames.Contains(message.RecipientUsername.Trim(), StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Email recipient username is not allowed for SMTP delivery.");
        }
    }

    private string Required(string key)
    {
        var value = configuration[Key(key)];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Email configuration '{Key(key)}' is required for SMTP delivery.");
        }

        return value;
    }

    private int GetInt(string key, int fallback)
    {
        var configuredValue = configuration[Key(key)];
        return int.TryParse(configuredValue, out var value) && value > 0 ? value : fallback;
    }

    private bool GetBool(string key, bool fallback)
    {
        var configuredValue = configuration[Key(key)];
        return bool.TryParse(configuredValue, out var value) ? value : fallback;
    }

    private IReadOnlyList<string> GetCsv(string key)
    {
        var configuredValue = configuration[Key(key)];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return [];
        }

        return configuredValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private string Key(string key) => $"{sectionPrefix}:{key}";
}
