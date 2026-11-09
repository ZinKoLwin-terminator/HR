namespace Apex.Hrms.Infrastructure.Time;

/// <summary>Resolves the business time zone from configuration (key <c>Business:TimeZone</c>).</summary>
internal static class BusinessTimeZone
{
    public const string ConfigurationKey = "Business:TimeZone";
    public const string DefaultId = "Asia/Yangon";

    // Windows without ICU (invariant globalization) only knows its own ids, so IANA ids fall back to these.
    private static readonly Dictionary<string, string> WindowsIds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Asia/Yangon"] = "Myanmar Standard Time",
        ["Asia/Rangoon"] = "Myanmar Standard Time",
        ["UTC"] = "UTC",
    };

    /// <exception cref="InvalidOperationException">The configured id is not a known time zone (fail fast at startup).</exception>
    public static TimeZoneInfo Resolve(string? configuredId)
    {
        var id = string.IsNullOrWhiteSpace(configuredId) ? DefaultId : configuredId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            if (WindowsIds.TryGetValue(id, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch (Exception fallback) when (fallback is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    // Fall through to the error below.
                }
            }

            throw new InvalidOperationException(
                $"The time zone '{id}' in configuration key '{ConfigurationKey}' is not known on this machine.",
                exception);
        }
    }
}
