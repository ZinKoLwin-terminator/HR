using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Grievances;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Stages grv.AuditLog rows in the request's context (saved by the unit of work) and reads them untracked.</summary>
internal sealed class EfGrievanceAuditLog(HrmsDbContext context) : IGrievanceAuditLog
{
    public void Add(GrievanceAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        context.Set<GrievanceAuditEntry>().Add(entry);
    }

    public IQueryable<GrievanceAuditEntry> Query() => context.Set<GrievanceAuditEntry>().AsNoTracking();
}
