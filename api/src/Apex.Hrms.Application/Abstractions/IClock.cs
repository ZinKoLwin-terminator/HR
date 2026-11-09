namespace Apex.Hrms.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Today's date in UTC. A per-company time zone can replace this once the requirement exists.</summary>
    DateOnly Today { get; }
}
