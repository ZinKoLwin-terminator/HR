using Apex.Hrms.Application.Organization;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class SectionConfiguration() : CodedRecordConfiguration<Section>("Section")
{
    protected override void ConfigureColumns(EntityTypeBuilder<Section> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(100);
        builder.Property(s => s.Description).HasMaxLength(300);
    }

    protected override void ConfigureRelationships(EntityTypeBuilder<Section> builder)
    {
        // UQ_Section_Id_DepartmentId and the composite foreign keys from Position and Employee are enforced by the
        // database only (see BranchConfiguration); SectionRules explains violations first.
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(s => s.DepartmentId)
            .HasConstraintName("FK_Section_Department")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EmployeeReference>().WithMany()
            .HasForeignKey(s => s.SupervisorEmployeeId)
            .HasConstraintName("FK_Section_Employee_SupervisorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
