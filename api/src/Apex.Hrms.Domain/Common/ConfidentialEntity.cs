namespace Apex.Hrms.Domain.Common;

/// <summary>
/// A record that a confidential submitter writes (a grievance case and its shared files): the creation and modification
/// times and the optimistic-concurrency token, but deliberately NO user ids. <see cref="AuditedEntity"/> stamps
/// <c>CreatedByUserId</c>/<c>ModifiedByUserId</c>, which a system-versioned history would keep forever and which would
/// identify the submitter of an anonymous case. The times are stamped by the persistence layer when changes are saved, and
/// a write still needs a signed-in user (or the system identity); the user is just not recorded.
/// </summary>
public abstract class ConfidentialEntity : Entity
{
    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ModifiedAtUtc { get; private set; }

    /// <summary>SQL Server rowversion; changes on every update.</summary>
    public byte[] RowVersion { get; private set; } = [];
}
