using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration() : CodedRecordConfiguration<Position>("Position")
{
    protected override void ConfigureColumns(EntityTypeBuilder<Position> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(300);
        builder.Property(p => p.Headcount).HasColumnType("smallint");
    }

    protected override void ConfigureRelationships(EntityTypeBuilder<Position> builder)
    {
        // Target of the composite foreign key that keeps Employee.PositionId inside the employee's department.
        builder.HasAlternateKey(p => new { p.Id, p.DepartmentId }).HasName("UQ_Position_Id_DepartmentId");

        builder.HasOne<Department>().WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .HasConstraintName("FK_Position_Department")
            .OnDelete(DeleteBehavior.NoAction);

        // The section must belong to the same department; the database skips the check when SectionId is NULL.
        builder.HasOne<Section>().WithMany()
            .HasForeignKey(p => new { p.SectionId, p.DepartmentId })
            .HasPrincipalKey(s => new { s.Id, s.DepartmentId })
            .HasConstraintName("FK_Position_Section")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<JobGrade>().WithMany()
            .HasForeignKey(p => p.JobGradeId)
            .HasConstraintName("FK_Position_JobGrade")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Position>().WithMany()
            .HasForeignKey(p => p.ReportsToPositionId)
            .HasConstraintName("FK_Position_Position_ReportsToPositionId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
