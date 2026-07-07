namespace TechYouthBpm.Application.Services;

public record EmailMessage(string To, string Subject, string Body, string? RecipientUsername = null, bool IsHtml = false);

public interface IEmailSender
{
    bool ExposesVerificationCode { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
