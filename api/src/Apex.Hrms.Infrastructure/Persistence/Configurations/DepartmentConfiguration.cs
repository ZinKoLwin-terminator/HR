using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration() : CodedRecordConfiguration<Department>("Department")
{
    protected override void ConfigureColumns(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(100);
        builder.Property(d => d.Description).HasMaxLength(300);
        builder.Property(d => d.CostCenter).HasMaxLength(20);
    }

    protected override void ConfigureRelationships(EntityTypeBuilder<Department> builder)
    {
        // Target of the composite foreign key that keeps Employee inside the department's branch.
        builder.HasAlternateKey(d => new { d.Id, d.BranchId }).HasName("UQ_Department_Id_BranchId");

        // The branch must belong to the same company.
        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(d => new { d.BranchId, d.CompanyId })
            .HasPrincipalKey(b => new { b.Id, b.CompanyId })
            .HasConstraintName("FK_Department_Branch")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EmployeeReference>().WithMany()
            .HasForeignKey(d => d.HeadEmployeeId)
            .HasConstraintName("FK_Department_Employee_HeadEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
