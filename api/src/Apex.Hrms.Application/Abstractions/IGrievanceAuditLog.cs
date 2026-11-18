using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The confidential access log of the grievance module (grv.AuditLog: bigint key, append-only). Entries are staged and saved with the
/// unit of work of the request, so a change and its entry are saved together. Never app.ActivityLog.
/// </summary>
public interface IGrievanceAuditLog
{
    void Add(GrievanceAuditEntry entry);

    /// <summary>Every entry, not tracked; composable with the <see cref="IReadModel"/> queries of the same request.</summary>
    IQueryable<GrievanceAuditEntry> Query();
}
