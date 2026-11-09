using System.Globalization;
using System.Text;
using Apex.Hrms.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Infrastructure.Email;

/// <summary>
/// Development only: writes each message to a file in a local folder (the API's <c>App_Data/dev-mail</c>) and sends
/// nothing, so a developer can read the reset token without any mail server and nothing can reach a real address.
/// The file holds the recipient and the token: the folder is git-ignored and must never be shared. Only the fact that a file was
/// written is logged: not the recipient, the subject or the path.
/// </summary>
public sealed partial class DevelopmentMailSink(string folder, ILogger<DevelopmentMailSink> logger) : IEmailSender
{
    public void EnsureAvailable()
    {
        // Always available: it only writes a file.
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        Directory.CreateDirectory(folder);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.eml";
        var content = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"To: {message.To}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Subject: {message.Subject}\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n\r\n")
            .Append(message.Body)
            .ToString();
        await File.WriteAllTextAsync(Path.Combine(folder, fileName), content, new UTF8Encoding(false), cancellationToken);
        LogWritten();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "A development mail was written to the dev-mail folder. Nothing was sent.")]
    private partial void LogWritten();
}
