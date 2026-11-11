namespace Apex.Hrms.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Today's date in the configured business time zone (key <c>Business:TimeZone</c>, default Asia/Yangon),
    /// not in UTC, so "cannot be in the future" rules follow the calendar the users live in.
    /// </summary>
    DateOnly Today { get; }

    /// <summary>
    /// The time of day now in the business time zone. Attendance punches are stamped with it (never with a time the
    /// client sends); <see cref="Today"/> is the matching date.
    /// </summary>
    TimeOnly TimeNow { get; }
}
