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
        // UQ_Position_Id_DepartmentId and the composite foreign key from Employee are enforced by the database only
        // (see BranchConfiguration); PositionRules explains violations first.
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .HasConstraintName("FK_Position_Department")
            .OnDelete(DeleteBehavior.NoAction);

        // In the database FK_Position_Section is composite (SectionId, DepartmentId): the section must belong to the
        // same department, and it is skipped when SectionId is NULL. Only SectionId is modeled.
        builder.HasOne<Section>().WithMany()
            .HasForeignKey(p => p.SectionId)
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
