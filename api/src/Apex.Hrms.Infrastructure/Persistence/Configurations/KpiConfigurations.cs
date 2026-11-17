using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

// Maps the kpi schema. CHECK constraints and defaults stay in the SSDT project; the application always supplies those
// values and the database refuses anything inconsistent (SqlConflicts turns the refusal into a validation error). The
// tables Template, EvaluationPeriod, Scorecard and ScorecardItem are system-versioned (temporal): SQL Server keeps their
// *History tables and the ValidFromUtc/ValidToUtc period columns itself, so neither is mapped (as for lve.LeaveApplication).
// Composite foreign keys (the scorecard's period, branch, department and position) are not modeled, as for the
// organization tables: the database enforces them and the scorecard rules explain violations first.

/// <summary>Maps kpi.Category.</summary>
internal sealed class KpiCategoryConfiguration : IEntityTypeConfiguration<KpiCategory>
{
    public void Configure(EntityTypeBuilder<KpiCategory> builder)
    {
        builder.ToTable("Category", "kpi");
        builder.HasKey(category => category.Id).HasName("PK_Category");
        builder.Property(category => category.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(category => category.Code).HasMaxLength(20);
        builder.HasIndex(category => category.Code).IsUnique().HasDatabaseName("UQ_Category_Code");
        builder.Property(category => category.Name).HasMaxLength(100);
        builder.HasIndex(category => category.Name).IsUnique().HasDatabaseName("UQ_Category_Name");
        builder.Property(category => category.Description).HasMaxLength(300);
        builder.Property(category => category.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
    }
}

/// <summary>Maps kpi.Template (temporal).</summary>
internal sealed class KpiTemplateConfiguration : IEntityTypeConfiguration<KpiTemplate>
{
    public void Configure(EntityTypeBuilder<KpiTemplate> builder)
    {
        builder.ToTable("Template", "kpi");
        builder.HasKey(template => template.Id).HasName("PK_Template");
        builder.Property(template => template.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(template => template.Code).HasMaxLength(20);
        builder.HasIndex(template => template.Code).IsUnique().HasDatabaseName("UQ_Template_Code");
        builder.Property(template => template.Name).HasMaxLength(150);
        builder.Property(template => template.Description).HasMaxLength(500);
        builder.Property(template => template.MeasurementType).HasTextConversion(KpiMeasurementTypeText.Map);
        builder.Property(template => template.Unit).HasMaxLength(20);
        builder.Property(template => template.Direction).HasTextConversion(KpiDirectionText.Map);
        builder.Property(template => template.ScoringMethod).HasTextConversion(KpiScoringMethodText.Map);
        builder.Property(template => template.DefaultTarget).HasColumnType("decimal(18,4)");
        builder.Property(template => template.RangeMin).HasColumnType("decimal(18,4)");
        builder.Property(template => template.RangeMax).HasColumnType("decimal(18,4)");
        builder.Property(template => template.MaxRating).HasColumnType("decimal(18,4)");
        builder.Property(template => template.DefaultWeight).HasColumnType("decimal(5,2)");
        builder.Property(template => template.MaxAchievementPct).HasColumnType("decimal(8,2)");
        builder.Property(template => template.Frequency).HasTextConversion(KpiFrequencyText.Map);
        builder.Property(template => template.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Ignore(template => template.Rule);

        builder.HasOne<KpiCategory>().WithMany()
            .HasForeignKey(template => template.CategoryId)
            .HasConstraintName("FK_Template_Category")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(template => template.DepartmentId)
            .HasConstraintName("FK_Template_Department")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(template => template.CategoryId).HasDatabaseName("IX_Template_CategoryId");
        builder.HasIndex(template => template.DepartmentId).HasDatabaseName("IX_Template_DepartmentId");
    }
}

/// <summary>Maps kpi.EvaluationPeriod (temporal).</summary>
internal sealed class EvaluationPeriodConfiguration : IEntityTypeConfiguration<EvaluationPeriod>
{
    public void Configure(EntityTypeBuilder<EvaluationPeriod> builder)
    {
        builder.ToTable("EvaluationPeriod", "kpi");
        builder.HasKey(period => period.Id).HasName("PK_EvaluationPeriod");
        builder.Property(period => period.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(period => period.Name).HasMaxLength(100);
        builder.Property(period => period.PeriodType).HasTextConversion(KpiFrequencyText.Map);
        builder.Property(period => period.StartDate).HasColumnType("date");
        builder.Property(period => period.EndDate).HasColumnType("date");
        builder.Property(period => period.SubmissionDueDate).HasColumnType("date");
        builder.Property(period => period.Status).HasTextConversion(EvaluationPeriodStatusText.Map);

        // The database also has UQ_EvaluationPeriod_Id_CompanyId, the target of the scorecard's composite foreign key.
        builder.HasIndex(period => new { period.CompanyId, period.Name }).IsUnique().HasDatabaseName("UQ_EvaluationPeriod_CompanyId_Name");
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(period => period.CompanyId)
            .HasConstraintName("FK_EvaluationPeriod_Company")
            .OnDelete(DeleteBehavior.NoAction);

        // Serves the overlap check and the period pickers (it also includes EndDate and Status in the database).
        builder.HasIndex(period => new { period.CompanyId, period.PeriodType, period.StartDate })
            .HasDatabaseName("IX_EvaluationPeriod_CompanyId_PeriodType_StartDate");
    }
}

/// <summary>Maps kpi.RatingBand.</summary>
internal sealed class RatingBandConfiguration : IEntityTypeConfiguration<RatingBand>
{
    public void Configure(EntityTypeBuilder<RatingBand> builder)
    {
        builder.ToTable("RatingBand", "kpi");
        builder.HasKey(band => band.Id).HasName("PK_RatingBand");
        builder.Property(band => band.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(band => band.Label).HasMaxLength(60);
        builder.HasIndex(band => band.Label).IsUnique().HasDatabaseName("UQ_RatingBand_Label");
        builder.Property(band => band.MinScore).HasColumnType("decimal(8,2)");
        builder.HasIndex(band => band.MinScore).IsUnique().HasDatabaseName("UQ_RatingBand_MinScore");
        builder.Property(band => band.Description).HasMaxLength(300);
        builder.Property(band => band.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
    }
}

/// <summary>Maps kpi.Scorecard (temporal). The workflow that changes it comes with the scorecard use cases.</summary>
internal sealed class ScorecardConfiguration : IEntityTypeConfiguration<Scorecard>
{
    public void Configure(EntityTypeBuilder<Scorecard> builder)
    {
        builder.ToTable("Scorecard", "kpi");
        builder.HasKey(scorecard => scorecard.Id).HasName("PK_Scorecard");
        builder.Property(scorecard => scorecard.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(scorecard => scorecard.Status).HasTextConversion(ScorecardStatusText.Map);
        builder.Property(scorecard => scorecard.EmployeeComment).HasMaxLength(1000);
        builder.Property(scorecard => scorecard.ManagerRating).HasColumnType("decimal(4,2)");
        builder.Property(scorecard => scorecard.ManagerComment).HasMaxLength(1000);
        builder.Property(scorecard => scorecard.HrComment).HasMaxLength(1000);
        builder.Property(scorecard => scorecard.OverallScore).HasColumnType("decimal(8,2)");
        builder.Property(scorecard => scorecard.RatingLabel).HasMaxLength(60);
        builder.Property(scorecard => scorecard.CalculationVersion).HasColumnType("smallint");
        builder.Property(scorecard => scorecard.PlanFinalizedOn).HasColumnType("date");
        builder.Property(scorecard => scorecard.SubmittedOn).HasColumnType("date");
        builder.Property(scorecard => scorecard.ReviewedOn).HasColumnType("date");
        builder.Property(scorecard => scorecard.FinalizedOn).HasColumnType("date");

        // One scorecard per employee and period.
        builder.HasIndex(scorecard => new { scorecard.EmployeeId, scorecard.PeriodId })
            .IsUnique()
            .HasDatabaseName("UQ_Scorecard_EmployeeId_PeriodId");
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(scorecard => scorecard.EmployeeId)
            .HasConstraintName("FK_Scorecard_Employee")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(scorecard => scorecard.ReviewerEmployeeId)
            .HasConstraintName("FK_Scorecard_Employee_ReviewerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(scorecard => new { scorecard.PeriodId, scorecard.Status }).HasDatabaseName("IX_Scorecard_PeriodId_Status");
        builder.HasIndex(scorecard => new { scorecard.ReviewerEmployeeId, scorecard.Status }).HasDatabaseName("IX_Scorecard_ReviewerEmployeeId_Status");
        builder.HasIndex(scorecard => new { scorecard.DepartmentId, scorecard.PeriodId }).HasDatabaseName("IX_Scorecard_DepartmentId_PeriodId");
        builder.HasIndex(scorecard => new { scorecard.BranchId, scorecard.CompanyId }).HasDatabaseName("IX_Scorecard_BranchId_CompanyId");
        builder.HasIndex(scorecard => new { scorecard.PositionId, scorecard.DepartmentId }).HasDatabaseName("IX_Scorecard_PositionId_DepartmentId");
        builder.HasIndex(scorecard => new { scorecard.PeriodId, scorecard.CompanyId }).HasDatabaseName("IX_Scorecard_PeriodId_CompanyId");
    }
}

/// <summary>Maps kpi.ScorecardItem (temporal). The items go with a scorecard that is deleted while it is still being planned.</summary>
internal sealed class ScorecardItemConfiguration : IEntityTypeConfiguration<ScorecardItem>
{
    public void Configure(EntityTypeBuilder<ScorecardItem> builder)
    {
        builder.ToTable("ScorecardItem", "kpi");
        builder.HasKey(item => item.Id).HasName("PK_ScorecardItem");
        builder.Property(item => item.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(item => item.KpiCode).HasMaxLength(20);
        builder.Property(item => item.KpiName).HasMaxLength(150);
        builder.Property(item => item.MeasurementType).HasTextConversion(KpiMeasurementTypeText.Map);
        builder.Property(item => item.Unit).HasMaxLength(20);
        builder.Property(item => item.Direction).HasTextConversion(KpiDirectionText.Map);
        builder.Property(item => item.ScoringMethod).HasTextConversion(KpiScoringMethodText.Map);
        builder.Property(item => item.Target).HasColumnType("decimal(18,4)");
        builder.Property(item => item.RangeMin).HasColumnType("decimal(18,4)");
        builder.Property(item => item.RangeMax).HasColumnType("decimal(18,4)");
        builder.Property(item => item.MaxRating).HasColumnType("decimal(18,4)");
        builder.Property(item => item.Weight).HasColumnType("decimal(5,2)");
        builder.Property(item => item.MaxAchievementPct).HasColumnType("decimal(8,2)");
        builder.Property(item => item.StartDate).HasColumnType("date");
        builder.Property(item => item.EndDate).HasColumnType("date");
        builder.Property(item => item.SortOrder).HasColumnType("smallint");
        builder.Property(item => item.AssignedOn).HasColumnType("date");
        builder.Property(item => item.ActualValue).HasColumnType("decimal(18,4)");
        builder.Property(item => item.EmployeeComment).HasMaxLength(1000);
        builder.Property(item => item.AchievementPct).HasColumnType("decimal(8,2)");
        builder.Property(item => item.WeightedScore).HasColumnType("decimal(8,2)");
        builder.Property(item => item.ManagerRating).HasColumnType("decimal(4,2)");
        builder.Property(item => item.ManagerComment).HasMaxLength(1000);
        builder.Ignore(item => item.Rule);

        // A template is assigned once per scorecard.
        builder.HasIndex(item => new { item.ScorecardId, item.TemplateId })
            .IsUnique()
            .HasDatabaseName("UQ_ScorecardItem_ScorecardId_TemplateId");
        builder.HasOne<Scorecard>().WithMany()
            .HasForeignKey(item => item.ScorecardId)
            .HasConstraintName("FK_ScorecardItem_Scorecard")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KpiTemplate>().WithMany()
            .HasForeignKey(item => item.TemplateId)
            .HasConstraintName("FK_ScorecardItem_Template")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<KpiCategory>().WithMany()
            .HasForeignKey(item => item.CategoryId)
            .HasConstraintName("FK_ScorecardItem_Category")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(item => item.TemplateId).HasDatabaseName("IX_ScorecardItem_TemplateId");
        builder.HasIndex(item => item.CategoryId).HasDatabaseName("IX_ScorecardItem_CategoryId");
    }
}

/// <summary>
/// Maps kpi.ReviewAction. Append-only (the database user has no UPDATE or DELETE on it) and without audit columns or a row
/// version: <c>OccurredAtUtc</c> is the only timestamp.
/// </summary>
internal sealed class ReviewActionConfiguration : IEntityTypeConfiguration<ReviewAction>
{
    public void Configure(EntityTypeBuilder<ReviewAction> builder)
    {
        builder.ToTable("ReviewAction", "kpi");
        builder.HasKey(action => action.Id).HasName("PK_ReviewAction");
        builder.Property(action => action.Id).UseIdentityColumn();

        builder.Property(action => action.Step).HasTextConversion(ReviewStepText.Map);
        builder.Property(action => action.Action).HasTextConversion(ReviewActionTypeText.Map);
        builder.Property(action => action.FromStatus).HasTextConversion(ScorecardStatusText.Map);
        builder.Property(action => action.ToStatus).HasTextConversion(ScorecardStatusText.Map);
        builder.Property(action => action.ActorName).HasMaxLength(ReviewAction.ActorNameMaxLength);
        builder.Property(action => action.Comment).HasMaxLength(ReviewAction.CommentMaxLength);
        builder.Property(action => action.OverallScore).HasColumnType("decimal(8,2)");
        builder.Property(action => action.OccurredAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<Scorecard>().WithMany()
            .HasForeignKey(action => action.ScorecardId)
            .HasConstraintName("FK_ReviewAction_Scorecard")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(action => action.ActorEmployeeId)
            .HasConstraintName("FK_ReviewAction_Employee_ActorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(action => new { action.ScorecardId, action.OccurredAtUtc }).HasDatabaseName("IX_ReviewAction_ScorecardId_OccurredAtUtc");
        builder.HasIndex(action => action.ActorEmployeeId).HasDatabaseName("IX_ReviewAction_ActorEmployeeId");
    }
}

/// <summary>Maps kpi.Attachment: file metadata only, the file itself lives in the API's file storage under the storage key.</summary>
internal sealed class KpiAttachmentConfiguration : IEntityTypeConfiguration<KpiAttachment>
{
    public void Configure(EntityTypeBuilder<KpiAttachment> builder)
    {
        builder.ToTable("Attachment", "kpi");
        builder.HasKey(attachment => attachment.Id).HasName("PK_Attachment");
        builder.Property(attachment => attachment.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(attachment => attachment.FileName).HasMaxLength(KpiAttachment.FileNameMaxLength);
        builder.Property(attachment => attachment.ContentType).HasMaxLength(KpiAttachment.ContentTypeMaxLength).IsUnicode(false);
        builder.Property(attachment => attachment.SizeBytes).HasColumnType("bigint");
        builder.Property(attachment => attachment.StorageKey).HasColumnType("char(32)").IsUnicode(false);
        builder.HasIndex(attachment => attachment.StorageKey).IsUnique().HasDatabaseName("UQ_Attachment_StorageKey");
        builder.Property(attachment => attachment.UploadedOn).HasColumnType("date");

        builder.HasOne<ScorecardItem>().WithMany()
            .HasForeignKey(attachment => attachment.ScorecardItemId)
            .HasConstraintName("FK_Attachment_ScorecardItem")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attachment => attachment.ScorecardItemId).HasDatabaseName("IX_Attachment_ScorecardItemId");
    }
}
