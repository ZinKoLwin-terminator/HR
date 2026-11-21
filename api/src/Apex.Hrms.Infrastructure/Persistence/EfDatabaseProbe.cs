using Apex.Hrms.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Opens (and closes) a connection through EF Core; no query touches HR data.</summary>
internal sealed class EfDatabaseProbe(HrmsDbContext context) : IDatabaseProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);
}
