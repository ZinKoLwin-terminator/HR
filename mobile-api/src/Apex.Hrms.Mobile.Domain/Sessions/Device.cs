namespace Apex.Hrms.Mobile.Domain.Sessions;

/// <summary>The device a session belongs to: its id names the session, its name is only kept for display.</summary>
public static class Device
{
    /// <summary>The longest device id a session accepts (the session store keys on it).</summary>
    public const int IdMaxLength = 100;

    /// <summary>A longer device name is cut to this length.</summary>
    public const int NameMaxLength = 100;

    public const string MissingIdMessage = "The device ID is missing.";

    public static readonly string TooLongIdMessage = $"The device ID must be at most {IdMaxLength} characters.";

    /// <summary>Why <paramref name="deviceId"/> cannot name a session, or null when it can.</summary>
    public static string? IdProblem(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? MissingIdMessage
        : deviceId.Trim().Length > IdMaxLength ? TooLongIdMessage
        : null;

    /// <summary>The trimmed device name, cut to <see cref="NameMaxLength"/>; null when there is none.</summary>
    public static string? NormalizeName(string? deviceName)
    {
        var trimmed = deviceName?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null
            : trimmed.Length <= NameMaxLength ? trimmed
            : trimmed[..NameMaxLength];
    }
}
