using Apex.Hrms.Domain.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps app.ImportBatch. The CHECK constraints (strategy, mode, status, counts) stay in the SSDT project.</summary>
internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatch", "app");
        builder.HasKey(batch => batch.Id).HasName("PK_ImportBatch");
        builder.Property(batch => batch.Id).ValueGeneratedNever();

        builder.Property(batch => batch.ModuleCode).HasMaxLength(ImportBatch.ModuleCodeMaxLength).IsUnicode(false);
        builder.Property(batch => batch.ModuleLabel).HasMaxLength(ImportBatch.ModuleLabelMaxLength);
        builder.Property(batch => batch.FileName).HasMaxLength(ImportBatch.FileNameMaxLength);
        builder.Property(batch => batch.Strategy).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(batch => batch.Mode).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(batch => batch.UploadedByUserId);
        builder.Property(batch => batch.UploadedByName).HasMaxLength(ImportBatch.UserNameMaxLength);
        builder.Property(batch => batch.UploadedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(batch => batch.StartedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(batch => batch.CompletedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(batch => batch.ErrorSummary).HasMaxLength(ImportBatch.ErrorSummaryMaxLength);
        builder.HasMany(batch => batch.Issues).WithOne().HasForeignKey(issue => issue.ImportBatchId)
            .HasConstraintName("FK_ImportIssue_ImportBatch").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(batch => batch.Issues).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(batch => batch.UploadedAtUtc).IsDescending().HasDatabaseName("IX_ImportBatch_UploadedAtUtc");
    }
}

/// <summary>Maps app.ImportIssue.</summary>
internal sealed class ImportIssueConfiguration : IEntityTypeConfiguration<ImportIssue>
{
    public void Configure(EntityTypeBuilder<ImportIssue> builder)
    {
        builder.ToTable("ImportIssue", "app");
        builder.HasKey(issue => issue.Id).HasName("PK_ImportIssue");
        builder.Property(issue => issue.Id).HasColumnType("bigint").UseIdentityColumn();
        builder.Property(issue => issue.Field).HasMaxLength(ImportIssue.FieldMaxLength);
        builder.Property(issue => issue.Value).HasMaxLength(ImportIssue.ValueMaxLength);
        builder.Property(issue => issue.Message).HasMaxLength(ImportIssue.MessageMaxLength);
        builder.Property(issue => issue.Suggestion).HasMaxLength(ImportIssue.SuggestionMaxLength);
        builder.Property(issue => issue.Severity).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(issue => new { issue.ImportBatchId, issue.RowNumber }).HasDatabaseName("IX_ImportIssue_ImportBatchId_RowNumber");
    }
}
