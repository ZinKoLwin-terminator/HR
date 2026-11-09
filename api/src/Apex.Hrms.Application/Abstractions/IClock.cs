namespace Apex.Hrms.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Today's date in the configured business time zone (key <c>Business:TimeZone</c>, default Asia/Yangon),
    /// not in UTC, so "cannot be in the future" rules follow the calendar the users live in.
    /// </summary>
    DateOnly Today { get; }
}
