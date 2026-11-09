using Apex.Hrms.Infrastructure.Time;

namespace Apex.Hrms.Infrastructure.Tests;

public sealed class ClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // Myanmar time is UTC+6:30 all year (no daylight saving).
    private static readonly TimeZoneInfo Yangon = TimeZoneInfo.CreateCustomTimeZone("Test/Yangon", TimeSpan.FromMinutes(390), "Yangon", "Yangon");

    [Fact]
    public void Today_follows_the_business_time_zone_when_it_is_already_tomorrow_there()
    {
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 11, 8, 20, 0, 0, TimeSpan.Zero)), Yangon);

        Assert.Equal(new DateOnly(2026, 11, 9), clock.Today);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 20, 0, 0, TimeSpan.Zero), clock.UtcNow);
    }

    [Fact]
    public void Today_follows_the_business_time_zone_when_it_is_still_yesterday_there()
    {
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 17, 29, 59, TimeSpan.Zero)), TimeZoneInfo.Utc);
        var yangon = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 17, 29, 59, TimeSpan.Zero)), Yangon);
        var yangonMidnight = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 17, 30, 0, TimeSpan.Zero)), Yangon);

        Assert.Equal(new DateOnly(2026, 11, 9), clock.Today);
        Assert.Equal(new DateOnly(2026, 11, 9), yangon.Today);
        Assert.Equal(new DateOnly(2026, 11, 10), yangonMidnight.Today);
    }

    [Fact]
    public void The_default_time_zone_is_Asia_Yangon_and_resolves_on_this_machine()
    {
        var zone = BusinessTimeZone.Resolve(null);

        Assert.Equal(TimeSpan.FromMinutes(390), zone.BaseUtcOffset);
        Assert.Equal(zone.BaseUtcOffset, BusinessTimeZone.Resolve("  ").BaseUtcOffset);
    }

    [Fact]
    public void A_configured_time_zone_is_used_and_UTC_is_accepted() =>
        Assert.Equal(TimeSpan.Zero, BusinessTimeZone.Resolve("UTC").BaseUtcOffset);

    [Fact]
    public void An_unknown_time_zone_fails_fast_with_the_configuration_key_in_the_message()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BusinessTimeZone.Resolve("Mars/Olympus"));

        Assert.Contains("Business:TimeZone", exception.Message, StringComparison.Ordinal);
    }
}
