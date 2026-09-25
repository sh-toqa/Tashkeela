using Microsoft.Extensions.Logging;
using Tashkeela.Application.Common;

namespace Tashkeela.Infrastructure.Email;

/// <summary>
/// Writes emails to the log instead of sending them. Real SMTP delivery (EM-1) arrives with the Notifications
/// iteration as another <see cref="IEmailSender"/>; nothing else changes.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogEmail(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email to {To}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
