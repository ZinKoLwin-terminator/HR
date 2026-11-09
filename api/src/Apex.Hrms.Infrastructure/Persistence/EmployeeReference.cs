using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Read-only slice of hr.Employee: just the columns that point at organization records, so delete rules can
/// count employees before the Employee module is ported. Replace with the real aggregate then.
/// </summary>
internal sealed class EmployeeReference
{
    public int Id { get; private set; }

    public int CompanyId { get; private set; }

    public int BranchId { get; private set; }

    public int DepartmentId { get; private set; }

    public int? SectionId { get; private set; }

    public int PositionId { get; private set; }

    public int JobGradeId { get; private set; }
}

internal sealed class EmployeeReferenceConfiguration : IEntityTypeConfiguration<EmployeeReference>
{
    public void Configure(EntityTypeBuilder<EmployeeReference> builder)
    {
        builder.ToTable("Employee", "hr", table => table.ExcludeFromMigrations());
        builder.HasKey(employee => employee.Id).HasName("PK_Employee");
    }
}
