using Apex.Hrms.Application.Organization;
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
        // In the database FK_Department_Branch is composite (BranchId, CompanyId) so the branch must belong to the
        // same company, and UQ_Department_Id_BranchId backs the Employee foreign key. Neither composite is modeled
        // (see BranchConfiguration): the database enforces them, DepartmentRules explains violations first.
        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(d => d.BranchId)
            .HasConstraintName("FK_Department_Branch")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EmployeeReference>().WithMany()
            .HasForeignKey(d => d.HeadEmployeeId)
            .HasConstraintName("FK_Department_Employee_HeadEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
