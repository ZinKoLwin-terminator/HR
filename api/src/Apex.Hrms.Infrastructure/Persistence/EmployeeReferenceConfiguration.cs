using Apex.Hrms.Application.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Maps the read-only <see cref="EmployeeReference"/> slice onto hr.Employee. Replace with the real aggregate when it is ported.</summary>
internal sealed class EmployeeReferenceConfiguration : IEntityTypeConfiguration<EmployeeReference>
{
    public void Configure(EntityTypeBuilder<EmployeeReference> builder)
    {
        builder.ToTable("Employee", "hr", table => table.ExcludeFromMigrations());
        builder.HasKey(employee => employee.Id).HasName("PK_Employee");
    }
}
