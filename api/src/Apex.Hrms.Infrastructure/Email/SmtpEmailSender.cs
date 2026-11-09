using System.Globalization;
using System.Net;
using System.Net.Mail;
using Apex.Hrms.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Apex.Hrms.Infrastructure.Email;

/// <summary>
/// The SMTP settings (<c>Email:Smtp:*</c>). The password and user name belong in environment variables or user
/// secrets only, never in an appsettings file or the repository.
/// </summary>
public sealed record SmtpSettings(
    string? Host,
    int Port,
    string? UserName,
    string? Password,
    string? FromAddress,
    string? FromName,
    bool EnableSsl)
{
    public const string Section = "Email:Smtp";

    public const int DefaultPort = 587;

    /// <summary>Host and sender are required; a user name needs a password (and the other way round).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress)
        && string.IsNullOrWhiteSpace(UserName) == string.IsNullOrWhiteSpace(Password);

    public static SmtpSettings From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(Section);
        var port = int.TryParse(section["Port"], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : DefaultPort;
        var enableSsl = !bool.TryParse(section["EnableSsl"], out var ssl) || ssl;
        return new SmtpSettings(
            section["Host"], port, section["UserName"], section["Password"], section["FromAddress"], section["FromName"], enableSsl);
    }
}

/// <summary>
/// Production sender. Refuses with <see cref="EmailNotConfiguredException"/> when the SMTP settings are missing, so
/// the caller fails closed instead of silently dropping mail. The exception names the settings, never their values.
/// </summary>
public sealed class SmtpEmailSender(SmtpSettings settings) : IEmailSender
{
    public void EnsureAvailable()
    {
        if (!settings.IsConfigured)
        {
            throw new EmailNotConfiguredException(
                $"Email is not configured. Set {SmtpSettings.Section}:Host and {SmtpSettings.Section}:FromAddress " +
                $"(and {SmtpSettings.Section}:UserName with {SmtpSettings.Section}:Password) through environment variables or user secrets.");
        }
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureAvailable();

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress!, settings.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };
        mail.To.Add(new MailAddress(message.To));

        using var client = new SmtpClient(settings.Host!, settings.Port) { EnableSsl = settings.EnableSsl };
        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}
