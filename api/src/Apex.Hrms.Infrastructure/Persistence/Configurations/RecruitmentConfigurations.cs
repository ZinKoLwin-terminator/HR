using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps rec.JobOpening. CHECK constraints and defaults stay in the SSDT project.</summary>
internal sealed class JobOpeningConfiguration : IEntityTypeConfiguration<JobOpening>
{
    public void Configure(EntityTypeBuilder<JobOpening> builder)
    {
        builder.ToTable("JobOpening", "rec");
        builder.HasKey(opening => opening.Id).HasName("PK_JobOpening");
        builder.Property(opening => opening.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(opening => opening.Code).HasMaxLength(20);
        builder.HasIndex(opening => opening.Code).IsUnique().HasDatabaseName("UQ_JobOpening_Code");
        builder.Property(opening => opening.Title).HasMaxLength(80);
        builder.Property(opening => opening.EmploymentType).HasTextConversion(RecruitmentEmploymentTypeText.Map);
        builder.Property(opening => opening.Vacancies).HasColumnType("smallint");
        builder.Property(opening => opening.PostedOn).HasColumnType("date");
        builder.Property(opening => opening.ClosingDate).HasColumnType("date");
        builder.Property(opening => opening.Description).HasMaxLength(500);
        builder.Property(opening => opening.Status).HasTextConversion(JobOpeningStatusText.Map);

        builder.HasOne<Department>().WithMany()
            .HasForeignKey(opening => opening.DepartmentId)
            .HasConstraintName("FK_JobOpening_Department")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(opening => opening.HiringManagerEmployeeId)
            .HasConstraintName("FK_JobOpening_Employee_HiringManagerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(opening => opening.DepartmentId).HasDatabaseName("IX_JobOpening_DepartmentId");
        builder.HasIndex(opening => opening.HiringManagerEmployeeId).HasDatabaseName("IX_JobOpening_HiringManagerEmployeeId");
        builder.HasIndex(opening => opening.Status).HasDatabaseName("IX_JobOpening_Status");
    }
}

/// <summary>Maps rec.Candidate.</summary>
internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("Candidate", "rec");
        builder.HasKey(candidate => candidate.Id).HasName("PK_Candidate");
        builder.Property(candidate => candidate.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(candidate => candidate.FirstName).HasMaxLength(40);
        builder.Property(candidate => candidate.LastName).HasMaxLength(40);
        builder.Property(candidate => candidate.Email).HasMaxLength(100);
        builder.HasIndex(candidate => candidate.Email).IsUnique().HasDatabaseName("UQ_Candidate_Email");
        builder.Property(candidate => candidate.Phone).HasMaxLength(25);
        builder.Property(candidate => candidate.Source).HasTextConversion(CandidateSourceText.Map);
        builder.Property(candidate => candidate.CurrentEmployer).HasMaxLength(80);
        builder.Property(candidate => candidate.ExperienceYears).HasColumnType("decimal(4,1)");
        builder.Property(candidate => candidate.ResumeName).HasMaxLength(260);
        builder.Property(candidate => candidate.Notes).HasMaxLength(300);
        builder.Property(candidate => candidate.NationalId).HasMaxLength(40);
    }
}

/// <summary>Maps rec.Application (the entity is JobApplication, because "Application" is a layer of this solution).</summary>
internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Application", "rec");
        builder.HasKey(application => application.Id).HasName("PK_Application");
        builder.Property(application => application.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(application => application.Stage).HasTextConversion(ApplicationStageText.Map);
        builder.Property(application => application.AppliedOn).HasColumnType("date");
        builder.Property(application => application.RejectionReason).HasMaxLength(200);
        builder.Property(application => application.Notes).HasMaxLength(300);
        builder.Property(application => application.BlacklistOverrideReason).HasMaxLength(300);

        builder.HasOne<JobOpening>().WithMany()
            .HasForeignKey(application => application.JobOpeningId)
            .HasConstraintName("FK_Application_JobOpening")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Candidate>().WithMany()
            .HasForeignKey(application => application.CandidateId)
            .HasConstraintName("FK_Application_Candidate")
            .OnDelete(DeleteBehavior.NoAction);

        // One application per candidate per opening; the same index serves the foreign key of the opening.
        builder.HasIndex(application => new { application.JobOpeningId, application.CandidateId })
            .IsUnique()
            .HasDatabaseName("UQ_Application_JobOpeningId_CandidateId");
        builder.HasIndex(application => application.CandidateId).HasDatabaseName("IX_Application_CandidateId");
        builder.HasIndex(application => new { application.JobOpeningId, application.Stage }).HasDatabaseName("IX_Application_JobOpeningId_Stage");
    }
}

/// <summary>Maps rec.Interview.</summary>
internal sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable("Interview", "rec");
        builder.HasKey(interview => interview.Id).HasName("PK_Interview");
        builder.Property(interview => interview.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(interview => interview.Round).HasTextConversion(InterviewRoundText.Map);
        builder.Property(interview => interview.Date).HasColumnType("date");
        builder.Property(interview => interview.StartTime).HasColumnType("time(0)");
        builder.Property(interview => interview.DurationMinutes).HasColumnType("smallint");
        builder.Property(interview => interview.Mode).HasTextConversion(InterviewModeText.Map);
        builder.Property(interview => interview.Location).HasMaxLength(120);
        builder.Property(interview => interview.Status).HasTextConversion(InterviewStatusText.Map);

        // tinyint: converted explicitly, because the entity keeps the score as an int.
        // A nullable property: EF skips the converter for null, so the converters are the plain ones.
        builder.Property(interview => interview.Score)
            .HasConversion(new ValueConverter<int, byte>(score => (byte)score, stored => stored))
            .HasColumnType("tinyint");
        builder.Property(interview => interview.Recommendation)
            .HasConversion(new ValueConverter<InterviewRecommendation, string>(
                recommendation => InterviewRecommendationText.ToText(recommendation),
                text => InterviewRecommendationText.Parse(text)))
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(interview => interview.Feedback).HasMaxLength(500);

        builder.HasOne<JobApplication>().WithMany()
            .HasForeignKey(interview => interview.ApplicationId)
            .HasConstraintName("FK_Interview_Application")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(interview => interview.InterviewerEmployeeId)
            .HasConstraintName("FK_Interview_Employee_InterviewerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(interview => interview.ApplicationId).HasDatabaseName("IX_Interview_ApplicationId");
        builder.HasIndex(interview => new { interview.InterviewerEmployeeId, interview.Date })
            .HasDatabaseName("IX_Interview_InterviewerEmployeeId_Date");
    }
}
