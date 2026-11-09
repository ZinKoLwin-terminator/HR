namespace Apex.Hrms.Domain.Common;

/// <summary>
/// Adds the audit columns and the optimistic-concurrency token every table carries. The audit values are
/// stamped by the persistence layer when changes are saved, not by domain code.
/// </summary>
public abstract class AuditedEntity : Entity
{
    public DateTime CreatedAtUtc { get; private set; }

    public int? CreatedByUserId { get; private set; }

    public DateTime ModifiedAtUtc { get; private set; }

    public int? ModifiedByUserId { get; private set; }

    /// <summary>SQL Server rowversion; changes on every update.</summary>
    public byte[] RowVersion { get; private set; } = [];
}
