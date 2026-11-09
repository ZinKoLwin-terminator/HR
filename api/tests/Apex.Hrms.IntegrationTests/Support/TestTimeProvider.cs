namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>A clock tests can set and advance, so audit stamps are predictable.</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public void Advance(TimeSpan by) => now += by;

    public override DateTimeOffset GetUtcNow() => now;
}
