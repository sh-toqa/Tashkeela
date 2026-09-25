namespace Tashkeela.Application.Common;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Outbound email (EM-1, NFR-MNT-3). An interface because email is an external service: tests replace it to read
/// what was sent, and the Notifications iteration will swap in a queued SMTP sender without touching use cases.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
