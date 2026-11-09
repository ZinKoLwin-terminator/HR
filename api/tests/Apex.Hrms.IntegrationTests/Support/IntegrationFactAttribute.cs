namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>A fact that is reported as skipped, with the reason, when LocalDB or the dacpac is unavailable.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (SqlServerEnvironment.UnavailableReason is { } reason)
        {
            Skip = reason;
        }
    }
}

/// <summary>A theory that is reported as skipped, with the reason, when LocalDB or the dacpac is unavailable.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (SqlServerEnvironment.UnavailableReason is { } reason)
        {
            Skip = reason;
        }
    }
}
