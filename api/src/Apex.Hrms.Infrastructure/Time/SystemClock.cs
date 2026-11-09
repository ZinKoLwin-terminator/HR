using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Time;

/// <summary>The system clock; "today" is the calendar date in the business time zone, not in UTC.</summary>
internal sealed class SystemClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, businessTimeZone).DateTime);
}
