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
        // Target of the composite foreign keys that keep Position and Employee inside the section's department.
        builder.HasAlternateKey(s => new { s.Id, s.DepartmentId }).HasName("UQ_Section_Id_DepartmentId");

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
