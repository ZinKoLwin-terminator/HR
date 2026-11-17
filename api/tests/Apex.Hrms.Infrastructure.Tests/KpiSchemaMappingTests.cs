using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the KPI part of the EF model against the kpi tables of the SSDT project. No connection is opened.</summary>
public sealed class KpiSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    private static string Type(IEntityType entity, string property) => entity.FindProperty(property)!.GetColumnType();

    [Theory]
    [InlineData(typeof(KpiCategory), "Category")]
    [InlineData(typeof(KpiTemplate), "Template")]
    [InlineData(typeof(EvaluationPeriod), "EvaluationPeriod")]
    [InlineData(typeof(RatingBand), "RatingBand")]
    [InlineData(typeof(Scorecard), "Scorecard")]
    [InlineData(typeof(ScorecardItem), "ScorecardItem")]
    [InlineData(typeof(ReviewAction), "ReviewAction")]
    [InlineData(typeof(KpiAttachment), "Attachment")]
    public void Tables_map_to_the_kpi_schema_and_name(Type entity, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal("kpi", type.GetSchema());
        Assert.Equal(table, type.GetTableName());
        Assert.Equal($"PK_{table}", type.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
    }

    [Fact]
    public void Every_audited_kpi_table_has_a_rowversion_concurrency_token_and_the_review_trail_has_none()
    {
        foreach (var type in new[]
        {
            Entity<KpiCategory>(), Entity<KpiTemplate>(), Entity<EvaluationPeriod>(), Entity<RatingBand>(),
            Entity<Scorecard>(), Entity<ScorecardItem>(), Entity<KpiAttachment>(),
        })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal("datetime2(3)", Type(type, "CreatedAtUtc"));
            Assert.Equal("datetime2(3)", Type(type, "ModifiedAtUtc"));
        }

        var review = Entity<ReviewAction>();
        Assert.Null(review.FindProperty("RowVersion"));
        Assert.Null(review.FindProperty("CreatedAtUtc"));
        Assert.Equal("datetime2(3)", Type(review, "OccurredAtUtc"));
    }

    [Fact]
    public void Template_and_scorecard_item_columns_use_the_ssdt_types_and_lengths()
    {
        foreach (var (entity, target, weight) in new[] { (Entity<KpiTemplate>(), "DefaultTarget", "DefaultWeight"), (Entity<ScorecardItem>(), "Target", "Weight") })
        {
            Assert.Equal("decimal(18,4)", Type(entity, target));
            Assert.Equal("decimal(18,4)", Type(entity, "RangeMin"));
            Assert.Equal("decimal(18,4)", Type(entity, "RangeMax"));
            Assert.Equal("decimal(18,4)", Type(entity, "MaxRating"));
            Assert.Equal("decimal(5,2)", Type(entity, weight));
            Assert.Equal("decimal(8,2)", Type(entity, "MaxAchievementPct"));
            Assert.Equal("varchar(30)", Type(entity, "MeasurementType"));
            Assert.Equal("varchar(30)", Type(entity, "Direction"));
            Assert.Equal("varchar(30)", Type(entity, "ScoringMethod"));
            Assert.Equal(20, entity.FindProperty("Unit")!.GetMaxLength());
        }

        var template = Entity<KpiTemplate>();
        Assert.Equal(20, template.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(150, template.FindProperty("Name")!.GetMaxLength());
        Assert.Equal(500, template.FindProperty("Description")!.GetMaxLength());
        Assert.Equal("varchar(30)", Type(template, "Frequency"));
        Assert.Equal("varchar(30)", Type(template, "Status"));

        var item = Entity<ScorecardItem>();
        Assert.Equal("decimal(18,4)", Type(item, "ActualValue"));
        Assert.Equal("decimal(8,2)", Type(item, "AchievementPct"));
        Assert.Equal("decimal(8,2)", Type(item, "WeightedScore"));
        Assert.Equal("decimal(4,2)", Type(item, "ManagerRating"));
        Assert.Equal("smallint", Type(item, "SortOrder"));
        Assert.Equal("date", Type(item, "StartDate"));
        Assert.Equal("date", Type(item, "EndDate"));
        Assert.Equal("date", Type(item, "AssignedOn"));
        Assert.Equal(20, item.FindProperty("KpiCode")!.GetMaxLength());
        Assert.Equal(150, item.FindProperty("KpiName")!.GetMaxLength());
        Assert.Equal(1000, item.FindProperty("EmployeeComment")!.GetMaxLength());
        Assert.Equal(1000, item.FindProperty("ManagerComment")!.GetMaxLength());
    }

    [Fact]
    public void Category_period_band_and_scorecard_columns_use_the_ssdt_types_and_lengths()
    {
        var category = Entity<KpiCategory>();
        Assert.Equal(20, category.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(100, category.FindProperty("Name")!.GetMaxLength());
        Assert.Equal(300, category.FindProperty("Description")!.GetMaxLength());
        Assert.Equal("varchar(30)", Type(category, "Status"));

        var period = Entity<EvaluationPeriod>();
        Assert.Equal(100, period.FindProperty("Name")!.GetMaxLength());
        Assert.Equal("varchar(30)", Type(period, "PeriodType"));
        Assert.Equal("varchar(30)", Type(period, "Status"));
        Assert.Equal("date", Type(period, "StartDate"));
        Assert.Equal("date", Type(period, "EndDate"));
        Assert.Equal("date", Type(period, "SubmissionDueDate"));
        Assert.Equal("bit", Type(period, "RequiresHrReview"));

        var band = Entity<RatingBand>();
        Assert.Equal(60, band.FindProperty("Label")!.GetMaxLength());
        Assert.Equal("decimal(8,2)", Type(band, "MinScore"));
        Assert.Equal(300, band.FindProperty("Description")!.GetMaxLength());
        Assert.Equal("varchar(30)", Type(band, "Status"));

        var scorecard = Entity<Scorecard>();
        Assert.Equal("varchar(30)", Type(scorecard, "Status"));
        Assert.Equal("decimal(4,2)", Type(scorecard, "ManagerRating"));
        Assert.Equal("decimal(8,2)", Type(scorecard, "OverallScore"));
        Assert.Equal("smallint", Type(scorecard, "CalculationVersion"));
        Assert.Equal(60, scorecard.FindProperty("RatingLabel")!.GetMaxLength());
        Assert.Equal(1000, scorecard.FindProperty("EmployeeComment")!.GetMaxLength());
        Assert.Equal(1000, scorecard.FindProperty("ManagerComment")!.GetMaxLength());
        Assert.Equal(1000, scorecard.FindProperty("HrComment")!.GetMaxLength());
        foreach (var column in new[] { "PlanFinalizedOn", "SubmittedOn", "ReviewedOn", "FinalizedOn" })
        {
            Assert.Equal("date", Type(scorecard, column));
        }

        var review = Entity<ReviewAction>();
        Assert.Equal("varchar(30)", Type(review, "Step"));
        Assert.Equal("varchar(30)", Type(review, "Action"));
        Assert.Equal("varchar(30)", Type(review, "FromStatus"));
        Assert.Equal("varchar(30)", Type(review, "ToStatus"));
        Assert.Equal(130, review.FindProperty("ActorName")!.GetMaxLength());
        Assert.Equal(1000, review.FindProperty("Comment")!.GetMaxLength());
        Assert.Equal("decimal(8,2)", Type(review, "OverallScore"));

        var attachment = Entity<KpiAttachment>();
        Assert.Equal(260, attachment.FindProperty("FileName")!.GetMaxLength());
        Assert.Equal("varchar(100)", Type(attachment, "ContentType"));
        Assert.Equal("bigint", Type(attachment, "SizeBytes"));
        Assert.Equal("char(32)", Type(attachment, "StorageKey"));
        Assert.Equal("date", Type(attachment, "UploadedOn"));
    }

    [Fact]
    public void Unique_indexes_and_foreign_keys_match_ssdt_names()
    {
        static IEnumerable<string> Unique(IEntityType entity) =>
            entity.GetIndexes().Where(index => index.IsUnique).Select(index => index.GetDatabaseName()!).Order(StringComparer.Ordinal);

        Assert.Equal(["UQ_Category_Code", "UQ_Category_Name"], Unique(Entity<KpiCategory>()));
        Assert.Equal(["UQ_Template_Code"], Unique(Entity<KpiTemplate>()));
        Assert.Equal(["UQ_EvaluationPeriod_CompanyId_Name"], Unique(Entity<EvaluationPeriod>()));
        Assert.Equal(["UQ_RatingBand_Label", "UQ_RatingBand_MinScore"], Unique(Entity<RatingBand>()));
        Assert.Equal(["UQ_Scorecard_EmployeeId_PeriodId"], Unique(Entity<Scorecard>()));
        Assert.Equal(["UQ_ScorecardItem_ScorecardId_TemplateId"], Unique(Entity<ScorecardItem>()));
        Assert.Equal(["UQ_Attachment_StorageKey"], Unique(Entity<KpiAttachment>()));

        static IEnumerable<string> Keys(IEntityType entity) =>
            entity.GetForeignKeys().Select(key => key.GetConstraintName()!).Order(StringComparer.Ordinal);

        Assert.Equal(["FK_Template_Category", "FK_Template_Department"], Keys(Entity<KpiTemplate>()));
        Assert.Equal(["FK_EvaluationPeriod_Company"], Keys(Entity<EvaluationPeriod>()));
        Assert.Equal(["FK_Scorecard_Employee", "FK_Scorecard_Employee_ReviewerEmployeeId"], Keys(Entity<Scorecard>()));
        Assert.Equal(["FK_ScorecardItem_Category", "FK_ScorecardItem_Scorecard", "FK_ScorecardItem_Template"], Keys(Entity<ScorecardItem>()));
        Assert.Equal(["FK_ReviewAction_Employee_ActorEmployeeId", "FK_ReviewAction_Scorecard"], Keys(Entity<ReviewAction>()));
        Assert.Equal(["FK_Attachment_ScorecardItem"], Keys(Entity<KpiAttachment>()));
    }

    [Fact]
    public void The_history_tables_and_period_columns_of_the_temporal_tables_are_not_mapped()
    {
        foreach (var type in new[] { Entity<KpiTemplate>(), Entity<EvaluationPeriod>(), Entity<Scorecard>(), Entity<ScorecardItem>() })
        {
            var names = type.GetProperties().Select(property => property.Name).ToList();

            Assert.DoesNotContain("ValidFromUtc", names);
            Assert.DoesNotContain("ValidToUtc", names);
        }

        Assert.DoesNotContain(context.Model.GetEntityTypes(), type => type.GetTableName() is { } table && table.EndsWith("History", StringComparison.Ordinal) && type.GetSchema() == "kpi");
    }

    [Fact]
    public void Texts_are_stored_as_the_exact_values_the_database_checks()
    {
        var measurement = Entity<KpiTemplate>().FindProperty("MeasurementType")!.GetValueConverter()!;
        var direction = Entity<KpiTemplate>().FindProperty("Direction")!.GetValueConverter()!;
        var scoring = Entity<ScorecardItem>().FindProperty("ScoringMethod")!.GetValueConverter()!;
        var frequency = Entity<KpiTemplate>().FindProperty("Frequency")!.GetValueConverter()!;
        var periodType = Entity<EvaluationPeriod>().FindProperty("PeriodType")!.GetValueConverter()!;
        var periodStatus = Entity<EvaluationPeriod>().FindProperty("Status")!.GetValueConverter()!;
        var scorecardStatus = Entity<Scorecard>().FindProperty("Status")!.GetValueConverter()!;
        var step = Entity<ReviewAction>().FindProperty("Step")!.GetValueConverter()!;
        var action = Entity<ReviewAction>().FindProperty("Action")!.GetValueConverter()!;

        Assert.Equal("Percentage", measurement.ConvertToProvider(KpiMeasurementType.Percentage));
        Assert.Equal("Higher Is Better", direction.ConvertToProvider(KpiDirection.HigherIsBetter));
        Assert.Equal("Capped Percentage", scoring.ConvertToProvider(KpiScoringMethod.CappedPercentage));
        Assert.Equal("Half-Yearly", frequency.ConvertToProvider(KpiFrequency.HalfYearly));
        Assert.Equal("Half-Yearly", periodType.ConvertToProvider(KpiFrequency.HalfYearly));
        Assert.Equal("Draft", periodStatus.ConvertToProvider(EvaluationPeriodStatus.Draft));
        Assert.Equal("Under Review", scorecardStatus.ConvertToProvider(ScorecardStatus.UnderReview));
        Assert.Equal("HR", step.ConvertToProvider(ReviewStep.HR));
        Assert.Equal("Finalize Plan", action.ConvertToProvider(ReviewActionType.FinalizePlan));
        Assert.Equal(EvaluationPeriodStatus.Closed, periodStatus.ConvertFromProvider("Closed"));
        Assert.Equal(KpiDirection.RangeBased, direction.ConvertFromProvider("Range Based"));
    }
}
