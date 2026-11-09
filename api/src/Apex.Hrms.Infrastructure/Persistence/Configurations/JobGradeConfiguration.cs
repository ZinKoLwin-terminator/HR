using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class JobGradeConfiguration() : CodedRecordConfiguration<JobGrade>("JobGrade")
{
    protected override void ConfigureColumns(EntityTypeBuilder<JobGrade> builder)
    {
        builder.Property(g => g.Name).HasMaxLength(60);
        builder.Property(g => g.Level).HasColumnType("smallint");
        builder.Property(g => g.MinSalary).HasColumnType("decimal(18,2)");
        builder.Property(g => g.MaxSalary).HasColumnType("decimal(18,2)");
        builder.Property(g => g.Description).HasMaxLength(300);
    }
}
