using System.Text.RegularExpressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Recruitment.Views;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The recruitment use cases against the real schema: the code sequence, the unique indexes and check constraints, the
/// foreign keys, the whole hiring flow in the database and the delete protection. The clock is the fixed 2026-11-09 of
/// <see cref="DatabaseTest"/>.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed partial class RecruitmentTests : DatabaseTest
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private async Task<(EmployeeDto Manager, EmployeeDto Interviewer, DepartmentDto Department)> SeedPeopleAsync()
    {
        var org = await SeedOrganizationAsync();
        var manager = await SeedEmployeeAsync(org, "1", "manager");
        var interviewer = await SeedEmployeeAsync(org, "2", "interviewer");
        return (manager, interviewer, org.Department);
    }

    private Task<Result<JobOpeningDto>> OpenJob(JobOpeningDto job) =>
        RunCommand<OpenJobOpeningCommand, JobOpeningDto>(new OpenJobOpeningCommand(job.Id, Requests.Version(job.RowVersion)));

    private async Task<JobOpeningDto> SeedOpenJobAsync(DepartmentDto department, EmployeeDto manager, string title = "Engineer", int vacancies = 1) =>
        Succeeded(await OpenJob(Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today, title, vacancies)))));

    private async Task<CandidateDto> SeedCandidateAsync(string local = "ada") => Succeeded(await Create(Requests.Candidate(local)));

    private Task<Result<JobApplicationDto>> MoveStage(JobApplicationDto application, string stage, string? reason = null) =>
        RunCommand<MoveApplicationStageCommand, JobApplicationDto>(Requests.MoveStage(application, stage, reason));

    private async Task<JobApplicationDto> SeedApplicationAsync(int jobId, int candidateId, params string[] stages)
    {
        var application = Succeeded(await Create(Requests.JobApplication(jobId, candidateId)));
        foreach (var stage in stages)
        {
            application = Succeeded(await MoveStage(application, stage, stage == "Rejected" ? "Not a fit" : null));
        }

        return application;
    }

    private Task<Result<InterviewDto>> Outcome(InterviewDto interview, int score = 4, string recommendation = "Hire") =>
        RunCommand<RecordInterviewOutcomeCommand, InterviewDto>(Requests.Outcome(interview, score, recommendation));

    private static int Number(string code) =>
        int.Parse(code[(code.LastIndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^JOB-2026-\d{3,}$")]
    private static partial Regex CodePattern();

    // ---- Job openings ----

    [IntegrationFact]
    public async Task A_new_opening_gets_a_code_from_the_database_sequence_and_starts_as_a_draft()
    {
        var (manager, _, department) = await SeedPeopleAsync();

        var first = Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today, "A")));
        var second = Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today, "B")));

        Assert.Matches(CodePattern(), first.Code);
        Assert.Matches(CodePattern(), second.Code);
        Assert.True(Number(second.Code) > Number(first.Code));
        Assert.Equal("Draft", first.Status);
        Assert.Equal(Today, first.PostedOn);
        var stored = await Sql.ScalarAsync<string>("SELECT Status FROM rec.JobOpening WHERE Id = @id", ("@id", first.Id));
        Assert.Equal("Draft", stored);
    }

    [IntegrationFact]
    public async Task The_status_commands_are_saved_and_a_stale_row_version_is_a_conflict()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var draft = Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today)));

        var open = Succeeded(await OpenJob(draft));
        var stale = await OpenJob(draft);
        var hold = Succeeded(await RunCommand<HoldJobOpeningCommand, JobOpeningDto>(new HoldJobOpeningCommand(open.Id, Requests.Version(open.RowVersion))));

        Failed(stale, ErrorType.Conflict);
        Assert.Equal("On Hold", hold.Status);
        Assert.Equal("On Hold", await Sql.ScalarAsync<string>("SELECT Status FROM rec.JobOpening WHERE Id = @id", ("@id", draft.Id)));
    }

    [IntegrationFact]
    public async Task A_past_closing_date_blocks_publishing_until_the_opening_is_edited()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var job = Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today, closing: Today.AddDays(1))));
        Time.Advance(TimeSpan.FromDays(3));

        var late = await OpenJob(job);

        Assert.Equal("The closing date has passed. Edit the opening before reopening it.", Failed(late, ErrorType.Conflict).Message);
    }

    [IntegrationFact]
    public async Task The_department_and_hiring_manager_are_checked_against_the_database()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var request = Requests.JobOpening(department.Id, manager.Id, Today);

        var noDepartment = await Create(request with { DepartmentId = 999999 });
        var noManager = await Create(request with { HiringManagerId = 999999 });

        Assert.Equal("Choose a department.", FailedOnField(noDepartment, "departmentId"));
        Assert.Equal("Choose a current employee.", FailedOnField(noManager, "hiringManagerId"));
    }

    // ---- Candidates ----

    [IntegrationFact]
    public async Task A_candidate_email_is_stored_in_lower_case_and_is_unique_whatever_the_case()
    {
        var request = Requests.Candidate("Ada") with { Email = Requests.CandidateEmail("Ada").ToUpperInvariant() };

        var created = Succeeded(await Create(request));
        var duplicate = await Create(Requests.Candidate("other") with { Email = request.Email });

        Assert.Equal(Requests.CandidateEmail("ada"), created.Email);
        Assert.Equal("Email must be unique.", FailedOnField(duplicate, "email"));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM rec.Candidate WHERE LOWER(Email) = @email", ("@email", created.Email)));
    }

    [IntegrationFact]
    public async Task The_database_itself_refuses_a_duplicate_email_in_another_case_and_experience_outside_0_to_50()
    {
        var created = Succeeded(await Create(Requests.Candidate()));

        var duplicate = await Sql.ErrorNumberOfAsync(
            "INSERT INTO rec.Candidate (FirstName, LastName, Email, Phone, Source, ExperienceYears) VALUES (N'X', N'Y', UPPER(@email), N'123', 'Referral', 1)",
            ("@email", created.Email));
        var tooMuch = await Sql.ErrorNumberOfAsync(
            "INSERT INTO rec.Candidate (FirstName, LastName, Email, Phone, Source, ExperienceYears) VALUES (N'X', N'Y', @email, N'123', 'Referral', 51)",
            ("@email", Requests.CandidateEmail("other")));

        Assert.Equal(2627, duplicate);
        Assert.Equal(547, tooMuch);
    }

    [IntegrationFact]
    public async Task A_candidate_can_keep_its_email_on_update_and_the_experience_keeps_one_decimal()
    {
        var created = Succeeded(await Create(Requests.Candidate(years: 4.5m)));

        var updated = Succeeded(await Update(created.Id, Requests.CandidateUpdate(created, years: 6.5m)));

        Assert.Equal(6.5m, updated.ExperienceYears);
        Assert.Equal(created.Email, updated.Email);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);
    }

    // ---- The hiring flow ----

    [IntegrationFact]
    public async Task A_candidate_goes_from_application_to_selection_and_the_opening_fills_up()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var candidate = await SeedCandidateAsync();

        var application = await SeedApplicationAsync(job.Id, candidate.Id, "Screening");
        var interview = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today)));
        var advanced = Succeeded(await Get<JobApplicationDto>(application.Id));
        var completed = Succeeded(await Outcome(interview, 5, "Strong hire"));
        var selected = Succeeded(await MoveStage(advanced, "Selected"));
        var filled = Succeeded(await Get<JobOpeningDto>(job.Id));

        Assert.Equal("Interview", advanced.Stage);
        Assert.Equal(("Completed", 5, "Strong hire"), (completed.Status, completed.Score, completed.Recommendation));
        Assert.Equal(new TimeOnly(10, 0), completed.StartTime);
        Assert.Equal("Selected", selected.Stage);
        Assert.Equal("Filled", filled.Status);
        Assert.Equal("Filled", await Sql.ScalarAsync<string>("SELECT Status FROM rec.JobOpening WHERE Id = @id", ("@id", job.Id)));
        Assert.Equal(5, await Sql.ScalarAsync<byte>("SELECT Score FROM rec.Interview WHERE Id = @id", ("@id", interview.Id)));
    }

    [IntegrationFact]
    public async Task Rejecting_a_selected_candidate_reopens_the_opening_and_cancels_scheduled_interviews()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var candidate = await SeedCandidateAsync();
        var application = await SeedApplicationAsync(job.Id, candidate.Id, "Screening");
        var done = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today, startHour: 9)));
        Succeeded(await Outcome(done));
        var scheduled = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(3))));
        var selected = Succeeded(await MoveStage(Succeeded(await Get<JobApplicationDto>(application.Id)), "Selected"));
        Assert.Equal("Filled", Succeeded(await Get<JobOpeningDto>(job.Id)).Status);

        var rejected = Succeeded(await MoveStage(selected, "Rejected", "Took another offer"));

        Assert.Equal("Took another offer", rejected.RejectionReason);
        Assert.Equal("Open", Succeeded(await Get<JobOpeningDto>(job.Id)).Status);
        var cancelled = Succeeded(await Get<InterviewDto>(scheduled.Id));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Cancelled: the application was rejected.", cancelled.Feedback);
    }

    [IntegrationFact]
    public async Task A_second_application_for_the_same_opening_is_refused_by_the_rule_and_by_the_database()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var candidate = await SeedCandidateAsync();
        await SeedApplicationAsync(job.Id, candidate.Id);

        var again = await Create(Requests.JobApplication(job.Id, candidate.Id));

        Assert.Equal("Already applied for this opening.", FailedOnField(again, "candidateId"));
    }

    [IntegrationFact]
    public async Task Two_applications_saved_at_the_same_moment_become_a_validation_error_not_a_server_error()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var candidate = await SeedCandidateAsync();
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO rec.Application (JobOpeningId, CandidateId, AppliedOn) VALUES (@job, @candidate, '2026-11-09')",
            ("@job", job.Id),
            ("@candidate", candidate.Id));

        var raced = await Create(Requests.JobApplication(job.Id, candidate.Id));

        Assert.Equal("Already applied for this opening.", FailedOnField(raced, "candidateId"));
    }

    [IntegrationFact]
    public async Task An_interviewer_is_not_double_booked_and_a_cancelled_slot_is_free_again()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager, vacancies: 2);
        var application = await SeedApplicationAsync(job.Id, (await SeedCandidateAsync()).Id, "Screening");
        var first = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(2))));

        var clash = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(2), startHour: 10, minutes: 30));
        var adjacent = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(2), startHour: 11));
        Succeeded(await RunCommand<CancelInterviewCommand, InterviewDto>(new CancelInterviewCommand(first.Id, Requests.Version(first.RowVersion), "Moved")));
        var reuse = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(2), startHour: 10, minutes: 30));

        Assert.Equal("Interviewer is busy from 10:00.", FailedOnField(clash, "startTime"));
        Assert.True(adjacent.IsSuccess);
        Assert.True(reuse.IsSuccess);
    }

    [IntegrationFact]
    public async Task A_new_interview_cannot_be_in_the_past_by_the_business_clock_but_a_completed_one_can_be_recorded()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var application = await SeedApplicationAsync(job.Id, (await SeedCandidateAsync()).Id, "Screening");

        var past = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(-1)));
        var recorded = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(-1)) with
        {
            Status = "Completed",
            Score = 3,
            Recommendation = "Hire",
        });

        Assert.Equal("Choose today or a later date.", FailedOnField(past, "date"));
        Assert.Equal("Completed", Succeeded(recorded).Status);
    }

    [IntegrationFact]
    public async Task The_interviewer_must_be_a_current_employee()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var application = await SeedApplicationAsync(job.Id, (await SeedCandidateAsync()).Id, "Screening");
        var current = Succeeded(await Get<EmployeeDto>(interviewer.Id));
        Succeeded(await RunCommand<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(current.Id, Requests.Version(current.RowVersion), Today.AddDays(-1), "Resigned")));

        var result = await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(1)));

        Assert.Equal("Choose a current employee.", FailedOnField(result, "interviewerId"));
    }

    // ---- Delete protection ----

    [IntegrationFact]
    public async Task Records_that_others_point_at_cannot_be_deleted_and_say_by_what()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager);
        var candidate = await SeedCandidateAsync();
        var application = await SeedApplicationAsync(job.Id, candidate.Id, "Screening");
        Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(1))));
        var current = Succeeded(await Get<JobApplicationDto>(application.Id));

        var jobDelete = await Delete<JobOpeningDto>(job.Id, Requests.Version(job.RowVersion));
        var candidateDelete = await Delete<CandidateDto>(candidate.Id, Requests.Version(candidate.RowVersion));
        var applicationDelete = await Delete<JobApplicationDto>(application.Id, Requests.Version(current.RowVersion));
        var employeeDelete = await Delete<EmployeeDto>(interviewer.Id, Requests.Version(interviewer.RowVersion));
        var departmentDelete = await Delete<DepartmentDto>(department.Id, Requests.Version(department.RowVersion));

        Assert.EndsWith(
            "cannot be deleted because it is used by 1 application(s). Deactivate it instead.",
            Failed(jobDelete, ErrorType.Conflict).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "\"Ada Testcandidate\" cannot be deleted because it is used by 1 application(s). Deactivate it instead.",
            Failed(candidateDelete, ErrorType.Conflict).Message);
        Assert.Equal(
            "\"Ada Testcandidate for " + job.Title + "\" cannot be deleted because it is used by 1 interview(s). Deactivate it instead.",
            Failed(applicationDelete, ErrorType.Conflict).Message);
        Assert.Contains("interview(s)", Failed(employeeDelete, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("job opening(s)", Failed(departmentDelete, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task A_completed_interview_stays_but_a_scheduled_one_and_then_its_application_can_go()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager, vacancies: 2);
        var candidate = await SeedCandidateAsync();
        var application = await SeedApplicationAsync(job.Id, candidate.Id, "Screening");
        var done = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today, startHour: 9)));
        var completed = Succeeded(await Outcome(done));
        var scheduled = Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(2))));

        var refused = await Delete<InterviewDto>(completed.Id, Requests.Version(completed.RowVersion));
        var removed = await Delete<InterviewDto>(scheduled.Id, Requests.Version(scheduled.RowVersion));

        Assert.Equal("A completed interview is part of the hiring record and cannot be deleted.", Failed(refused, ErrorType.Conflict).Message);
        Assert.True(removed.IsSuccess);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM rec.Interview WHERE ApplicationId = @id", ("@id", application.Id)));
    }

    [IntegrationFact]
    public async Task Deleting_unused_records_removes_them()
    {
        var (manager, _, department) = await SeedPeopleAsync();
        var job = Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today)));
        var candidate = await SeedCandidateAsync();

        Assert.True((await Delete<JobOpeningDto>(job.Id, Requests.Version(job.RowVersion))).IsSuccess);
        Assert.True((await Delete<CandidateDto>(candidate.Id, Requests.Version(candidate.RowVersion))).IsSuccess);
        Failed(await Get<JobOpeningDto>(job.Id), ErrorType.NotFound);
    }

    // ---- Lists and views ----

    [IntegrationFact]
    public async Task The_lists_search_filter_and_sort_in_the_database()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager, "Backend");
        Succeeded(await Create(Requests.JobOpening(department.Id, manager.Id, Today, "Designer")));
        var application = await SeedApplicationAsync(job.Id, (await SeedCandidateAsync()).Id, "Screening");
        Succeeded(await Create(Requests.Interview(application.Id, interviewer.Id, Today.AddDays(1))));

        var byTitle = Succeeded(await Query<JobOpeningDto, JobOpeningFilter>(
            new PageRequest<JobOpeningFilter>(0, 10, new SortState("title", "desc"), Tag.ToLowerInvariant(), new JobOpeningFilter())));
        var open = Succeeded(await Query<JobOpeningDto, JobOpeningFilter>(
            new PageRequest<JobOpeningFilter>(0, 10, null, Tag, new JobOpeningFilter(Status: "Open"))));
        var stages = Succeeded(await Query<JobApplicationDto, JobApplicationFilter>(
            new PageRequest<JobApplicationFilter>(0, 10, null, null, new JobApplicationFilter(JobOpeningId: job.Id, Stage: "Interview"))));
        var interviews = Succeeded(await Query<InterviewDto, InterviewFilter>(
            new PageRequest<InterviewFilter>(0, 10, null, null, new InterviewFilter(InterviewerId: interviewer.Id, DateFrom: Today, DateTo: Today.AddDays(2)))));

        Assert.Equal([Requests.JobOpening(department.Id, manager.Id, Today, "Designer").Title, job.Title], byTitle.Items.Select(item => item.Title));
        Assert.Equal(job.Id, Assert.Single(open.Items).Id);
        Assert.Equal(application.Id, Assert.Single(stages.Items).Id);
        Assert.Equal("Meeting link", Assert.Single(interviews.Items).Location);
    }

    [IntegrationFact]
    public async Task The_pipeline_and_counts_are_built_from_the_database()
    {
        var (manager, interviewer, department) = await SeedPeopleAsync();
        var job = await SeedOpenJobAsync(department, manager, vacancies: 2);
        var ada = await SeedCandidateAsync("ada");
        var bob = await SeedCandidateAsync("bob");
        var screened = await SeedApplicationAsync(job.Id, ada.Id, "Screening");
        await SeedApplicationAsync(job.Id, bob.Id);
        var interview = Succeeded(await Create(Requests.Interview(screened.Id, interviewer.Id, Today.AddDays(2))));

        var pipeline = Succeeded(await InScopeAsync(async sp => await sp.GetRequiredService<IRecruitmentPipelineHandler>().HandleAsync(job.Id, default)));
        var counts = Succeeded(await InScopeAsync(async sp => await sp.GetRequiredService<IRecruitmentCountsHandler>().HandleAsync(default)));

        Assert.Equal(["Applied", "Screening", "Interview", "Selected", "Rejected"], pipeline.Columns.Select(column => column.Stage));
        var card = Assert.Single(Assert.Single(pipeline.Columns, column => column.Stage == "Interview").Cards);
        Assert.Equal(interview.Id, card.NextInterview!.Id);
        Assert.Equal(1, Assert.Single(pipeline.Columns, column => column.Stage == "Applied").Count);
        var forJob = Assert.Single(counts.JobOpenings, count => count.JobOpeningId == job.Id);
        Assert.Equal((2, 0), (forJob.Applications, forJob.Selected));
    }
}
