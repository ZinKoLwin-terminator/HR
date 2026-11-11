using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Time;

/// <summary>The system clock; "today" is the calendar date in the business time zone, not in UTC.</summary>
internal sealed class SystemClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(Local.DateTime);

    public TimeOnly TimeNow => TimeOnly.FromDateTime(Local.DateTime);

    /// <summary>One conversion for both, so a call near midnight cannot pair a date with the wrong time.</summary>
    private DateTimeOffset Local => TimeZoneInfo.ConvertTime(UtcNow, businessTimeZone);
}
