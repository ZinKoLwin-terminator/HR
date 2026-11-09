using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Maps the domain onto the existing SQL Server schema (owned by the SSDT database project).
/// EF never creates or migrates that schema.
/// </summary>
internal sealed class HrmsDbContext(DbContextOptions<HrmsDbContext> options) : DbContext(options)
{
    public const string EmployeeCodeSequenceName = "EmployeeCodeSeq";
    public const string EmployeeCodeSequenceSchema = "hr";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrmsDbContext).Assembly);

        // hr.EmployeeCodeSeq: read with NEXT VALUE FOR (see EfEmployeeCodeSequence); it is not tied to a column.
        modelBuilder.HasSequence<int>(EmployeeCodeSequenceName, EmployeeCodeSequenceSchema)
            .StartsAt(1)
            .IncrementsBy(1);
    }
}
