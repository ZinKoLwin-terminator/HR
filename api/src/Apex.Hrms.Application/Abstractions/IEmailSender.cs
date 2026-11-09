namespace Apex.Hrms.Application.Abstractions;

/// <summary>A plain-text mail. The body may carry a secret (a password reset token): never log it.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Sends mail. Development writes to a local file; production uses SMTP (see Infrastructure).</summary>
public interface IEmailSender
{
    /// <summary>
    /// Fails before any lookup or send when this sender cannot deliver, so a caller can fail closed with a clear
    /// configuration error instead of silently dropping the mail.
    /// </summary>
    /// <exception cref="EmailNotConfiguredException">The sender is not configured.</exception>
    void EnsureAvailable();

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Mail cannot be sent because the server is not configured for it. The message names the setting, never a value.</summary>
public sealed class EmailNotConfiguredException : InvalidOperationException
{
    public EmailNotConfiguredException()
        : base("Email sending is not configured.")
    {
    }

    public EmailNotConfiguredException(string message)
        : base(message)
    {
    }

    public EmailNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
