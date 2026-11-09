using Apex.Hrms.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Maps the domain onto the existing SQL Server schema (owned by the SSDT database project).
/// EF never creates or migrates that schema. It is also the ASP.NET Core Identity context: the standard
/// <see cref="IdentityUser"/> stores (no roles) are mapped onto the auth schema (see <see cref="IdentityModelConfiguration"/>).
/// </summary>
internal sealed class HrmsDbContext(DbContextOptions<HrmsDbContext> options) : IdentityUserContext<IdentityUser>(options)
{
    public const string EmployeeCodeSequenceName = "EmployeeCodeSeq";
    public const string EmployeeCodeSequenceSchema = "hr";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Configure(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrmsDbContext).Assembly);

        // hr.EmployeeCodeSeq: read with NEXT VALUE FOR (see EfEmployeeCodeSequence); it is not tied to a column.
        modelBuilder.HasSequence<int>(EmployeeCodeSequenceName, EmployeeCodeSequenceSchema)
            .StartsAt(1)
            .IncrementsBy(1);
    }
}
