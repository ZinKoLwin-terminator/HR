using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Application.Security;

/// <summary>Builds the password reset mail. The body contains the reset token, a secret.</summary>
public static class PasswordResetEmail
{
    public const string Subject = "Reset your APEX HRMS password";

    /// <param name="to">The address of the account.</param>
    /// <param name="token">The token from Identity's password reset token generator.</param>
    /// <param name="resetPageUrl">Optional absolute URL of the reset page in the frontend (server configuration).</param>
    public static EmailMessage Build(string to, string token, string? resetPageUrl)
    {
        var minutes = (int)SecurityPolicy.PasswordResetTokenLifetime.TotalMinutes;
        var lines = new List<string>
        {
            "A password reset was requested for your APEX HRMS account.",
            string.Empty,
        };

        if (Uri.TryCreate(resetPageUrl, UriKind.Absolute, out var page))
        {
            var separator = string.IsNullOrEmpty(page.Query) ? "?" : "&";
            lines.Add("Open this link to choose a new password:");
            lines.Add($"{page}{separator}email={Uri.EscapeDataString(to)}&token={Uri.EscapeDataString(token)}");
        }
        else
        {
            lines.Add("Use this reset token on the reset page:");
            lines.Add(token);
        }

        lines.Add(string.Empty);
        lines.Add($"The token works once and expires in {minutes} minutes.");
        lines.Add("If you did not ask for this, ignore this mail: your password has not changed.");
        return new EmailMessage(to, Subject, string.Join(Environment.NewLine, lines));
    }
}
