using Apex.Hrms.Domain.Recruitment;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the recruitment part of the EF model against the rec tables of the SSDT project. No connection is opened.</summary>
public sealed class RecruitmentSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(JobOpening), "JobOpening")]
    [InlineData(typeof(Candidate), "Candidate")]
    [InlineData(typeof(JobApplication), "Application")]
    [InlineData(typeof(Interview), "Interview")]
    public void Tables_map_to_the_rec_schema(Type entity, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal("rec", type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void Every_recruitment_table_has_a_rowversion_token_and_an_identity_key()
    {
        foreach (var type in new[] { Entity<JobOpening>(), Entity<Candidate>(), Entity<JobApplication>(), Entity<Interview>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
        }
    }

    [Fact]
    public void Columns_use_the_ssdt_types_and_lengths()
    {
        var job = Entity<JobOpening>();
        Assert.Equal(20, job.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(80, job.FindProperty("Title")!.GetMaxLength());
        Assert.Equal(500, job.FindProperty("Description")!.GetMaxLength());
        Assert.Equal("smallint", job.FindProperty("Vacancies")!.GetColumnType());
        Assert.Equal("date", job.FindProperty("PostedOn")!.GetColumnType());
        Assert.Equal("date", job.FindProperty("ClosingDate")!.GetColumnType());
        Assert.Equal("varchar(30)", job.FindProperty("Status")!.GetColumnType());
        Assert.Equal("varchar(30)", job.FindProperty("EmploymentType")!.GetColumnType());

        var candidate = Entity<Candidate>();
        Assert.Equal("decimal(4,1)", candidate.FindProperty("ExperienceYears")!.GetColumnType());
        Assert.Equal("varchar(30)", candidate.FindProperty("Source")!.GetColumnType());
        Assert.Equal(100, candidate.FindProperty("Email")!.GetMaxLength());
        Assert.Equal(25, candidate.FindProperty("Phone")!.GetMaxLength());
        Assert.Equal(260, candidate.FindProperty("ResumeName")!.GetMaxLength());

        var application = Entity<JobApplication>();
        Assert.Equal("varchar(30)", application.FindProperty("Stage")!.GetColumnType());
        Assert.Equal("date", application.FindProperty("AppliedOn")!.GetColumnType());
        Assert.Equal(200, application.FindProperty("RejectionReason")!.GetMaxLength());
        Assert.Equal(300, application.FindProperty("Notes")!.GetMaxLength());

        var interview = Entity<Interview>();
        Assert.Equal("date", interview.FindProperty("Date")!.GetColumnType());
        Assert.Equal("time(0)", interview.FindProperty("StartTime")!.GetColumnType());
        Assert.Equal("smallint", interview.FindProperty("DurationMinutes")!.GetColumnType());
        Assert.Equal("tinyint", interview.FindProperty("Score")!.GetColumnType());
        Assert.Equal("varchar(30)", interview.FindProperty("Recommendation")!.GetColumnType());
        Assert.Equal(120, interview.FindProperty("Location")!.GetMaxLength());
        Assert.Equal(500, interview.FindProperty("Feedback")!.GetMaxLength());
        Assert.Equal("InterviewerEmployeeId", interview.FindProperty("InterviewerEmployeeId")!.GetColumnName());
    }

    [Fact]
    public void The_unique_indexes_of_the_database_are_mapped()
    {
        Assert.Contains(Entity<JobOpening>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_JobOpening_Code");
        Assert.Contains(Entity<Candidate>().GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "UQ_Candidate_Email");
        Assert.Contains(
            Entity<JobApplication>().GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["JobOpeningId", "CandidateId"]));
    }

    [Fact]
    public void Foreign_keys_never_cascade()
    {
        foreach (var type in new[] { Entity<JobOpening>(), Entity<JobApplication>(), Entity<Interview>() })
        {
            Assert.NotEmpty(type.GetForeignKeys());
            Assert.All(type.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
        }
    }

    [Fact]
    public void Status_texts_round_trip_the_database_values()
    {
        Assert.Equal("On Hold", JobOpeningStatusText.ToText(JobOpeningStatus.OnHold));
        Assert.Equal(RecruitmentEmploymentType.Internship, RecruitmentEmploymentTypeText.Parse("Internship"));
        Assert.Equal("Career site", CandidateSourceText.ToText(CandidateSource.CareerSite));
        Assert.Equal("Phone screen", InterviewRoundText.ToText(InterviewRound.PhoneScreen));
        Assert.Equal("Strong hire", InterviewRecommendationText.ToText(InterviewRecommendation.StrongHire));
        Assert.False(JobOpeningStatusText.TryParse("OnHold", out _));
    }
}
