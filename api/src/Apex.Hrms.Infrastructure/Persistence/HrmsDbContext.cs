using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Maps the domain onto the existing SQL Server schema (owned by the SSDT database project).
/// EF never creates or migrates that schema.
/// </summary>
internal sealed class HrmsDbContext(DbContextOptions<HrmsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrmsDbContext).Assembly);
}
