using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Time;

internal sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
