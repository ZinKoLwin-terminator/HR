using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Recruitment;

/// <summary>Job openings: the generated code, references, the status commands, editing rules, delete protection and the list.</summary>
public sealed class JobOpeningHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly Today => RecruitmentFixtures.Today;

    private Task<Result<PagedResult<JobOpeningDto>>> Query(JobOpeningFilter? filter = null, string? search = null, SortState? sort = null) =>
        host.Get<IQueryHandler<JobOpeningDto, JobOpeningFilter>>()
            .HandleAsync(new PageRequest<JobOpeningFilter>(0, 50, sort, search, filter ?? new JobOpeningFilter()), default);

    // ---- Create and the code ----

    [Fact]
    public async Task A_new_opening_is_a_draft_with_a_generated_code_and_todays_posting_date()
    {
        var seed = await host.SeedRecruitmentAsync();

        var first = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        var second = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));

        Assert.Equal("JOB-2026-001", first.Code);
        Assert.Equal("JOB-2026-002", second.Code);
        Assert.Equal("Draft", first.Status);
        Assert.Equal(Today, first.PostedOn);
        Assert.Equal("Full-time", first.EmploymentType);
        Assert.Equal(8, first.RowVersion.Length);
    }

    [Fact]
    public async Task The_code_uses_the_year_of_the_posting_date()
    {
        var seed = await host.SeedRecruitmentAsync();

        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, posted: new DateOnly(2025, 12, 1), closing: Today));

        Assert.Equal("JOB-2025-001", job.Code);
        Assert.Equal(new DateOnly(2025, 12, 1), job.PostedOn);
    }

    [Fact]
    public async Task Codes_already_in_use_are_skipped()
    {
        var seed = await host.SeedRecruitmentAsync();
        await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        host.JobCodes.Reset();

        var third = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, "Analyst"));

        Assert.Equal("JOB-2026-003", third.Code);
    }

    [Fact]
    public async Task The_department_must_exist_and_the_hiring_manager_must_be_a_current_employee()
    {
        var seed = await host.SeedRecruitmentAsync();
        var request = RecruitmentFixtures.NewJob(seed);

        var noDepartment = await host.CreateJob(request with { DepartmentId = 999 });
        var noManager = await host.CreateJob(request with { HiringManagerId = 999 });
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(seed.Manager.Id, Requests.Version(seed.Manager.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));
        var left = await host.CreateJob(request);

        ResultAssert.FailedOnField(noDepartment, "departmentId", "Choose a department.");
        ResultAssert.FailedOnField(noManager, "hiringManagerId", "Choose a current employee as hiring manager.");
        ResultAssert.FailedOnField(left, "hiringManagerId", "Choose a current employee as hiring manager.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Vacancies_must_be_between_1_and_50(int vacancies)
    {
        var seed = await host.SeedRecruitmentAsync();

        var result = await host.CreateJob(RecruitmentFixtures.NewJob(seed, vacancies: vacancies));

        ResultAssert.FailedOnField(result, "vacancies", "Vacancies must be between 1 and 50.");
    }

    [Fact]
    public async Task The_closing_date_cannot_be_before_the_posting_date_and_a_bad_employment_type_is_refused()
    {
        var seed = await host.SeedRecruitmentAsync();

        var early = await host.CreateJob(RecruitmentFixtures.NewJob(seed, posted: Today, closing: Today.AddDays(-1)));
        var bad = await host.CreateJob(RecruitmentFixtures.NewJob(seed) with { EmploymentType = "Intern" });

        ResultAssert.FailedOnField(early, "closingDate", "The closing date cannot be before the posting date.");
        ResultAssert.FailedOnField(bad, "employmentType");
    }

    // ---- Status commands ----

    [Fact]
    public async Task Open_hold_close_and_reopen_follow_the_transition_table()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));

        job = ResultAssert.Succeeded(await host.OpenJob(job));
        Assert.Equal("Open", job.Status);
        job = ResultAssert.Succeeded(await host.Command<HoldJobOpeningCommand, JobOpeningDto>(new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal("On Hold", job.Status);
        job = ResultAssert.Succeeded(await host.OpenJob(job));
        job = ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal("Closed", job.Status);
        job = ResultAssert.Succeeded(await host.Command<ReopenJobOpeningCommand, JobOpeningDto>(new ReopenJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal("Open", job.Status);
    }

    [Fact]
    public async Task A_forbidden_move_is_a_conflict_and_leaves_the_opening_alone()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));

        var hold = await host.Command<HoldJobOpeningCommand, JobOpeningDto>(new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion)));

        ResultAssert.Failed(hold, ErrorType.Conflict, "A draft job opening cannot be set to On Hold.");
        Assert.Equal("Draft", ResultAssert.Succeeded(await host.GetJob(job.Id)).Status);
    }

    [Fact]
    public async Task Publishing_is_refused_once_the_closing_date_has_passed_and_works_on_the_closing_day()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, closing: Today.AddDays(2)));

        host.Clock.Today = Today.AddDays(3);
        var late = await host.OpenJob(job);
        host.Clock.Today = Today.AddDays(2);
        var onTheDay = await host.OpenJob(job);

        ResultAssert.Failed(late, ErrorType.Conflict, "The closing date has passed. Edit the opening before reopening it.");
        Assert.Equal("Open", ResultAssert.Succeeded(onTheDay).Status);
    }

    [Fact]
    public async Task A_closed_opening_reopens_only_before_its_closing_date_passes()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, closing: Today.AddDays(2)));
        var closed = ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));

        host.Clock.Today = Today.AddDays(5);
        var refused = await host.Command<ReopenJobOpeningCommand, JobOpeningDto>(new ReopenJobOpeningCommand(closed.Id, Requests.Version(closed.RowVersion)));

        ResultAssert.Failed(refused, ErrorType.Conflict, "The closing date has passed. Edit the opening before reopening it.");
    }

    [Fact]
    public async Task A_stale_or_missing_row_version_and_an_unknown_id_are_refused()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        ResultAssert.Succeeded(await host.OpenJob(job));

        var stale = await host.OpenJob(job);
        var missing = await host.Command<OpenJobOpeningCommand, JobOpeningDto>(new OpenJobOpeningCommand(job.Id, null));
        var unknown = await host.Command<OpenJobOpeningCommand, JobOpeningDto>(new OpenJobOpeningCommand(999, Requests.Version(job.RowVersion)));

        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.FailedOnField(missing, "rowVersion");
        ResultAssert.Failed(unknown, ErrorType.NotFound, "Job opening #999 was not found.");
    }

    // ---- Update ----

    [Fact]
    public async Task An_open_opening_can_be_edited_and_keeps_its_code_and_status()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>>()
            .HandleAsync(job.Id, RecruitmentFixtures.JobUpdateOf(job, "Senior Backend Engineer", 3), default));

        Assert.Equal("Senior Backend Engineer", updated.Title);
        Assert.Equal(3, updated.Vacancies);
        Assert.Equal(job.Code, updated.Code);
        Assert.Equal("Open", updated.Status);
        Assert.NotEqual(job.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task A_closed_opening_cannot_be_edited()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        var closed = ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));

        var result = await host.Get<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>>()
            .HandleAsync(job.Id, RecruitmentFixtures.JobUpdateOf(closed, "New title"), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "A closed job opening cannot be edited.");
    }

    [Fact]
    public async Task Vacancies_cannot_be_set_below_the_selected_candidates()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, vacancies: 3));
        foreach (var email in new[] { "a@example.test", "b@example.test" })
        {
            var candidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate(email));
            var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
            var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, RecruitmentFixtures.Today, startHour: email == "a@example.test" ? 9 : 11));
            ResultAssert.Succeeded(await host.Outcome(interview));
            ResultAssert.Succeeded(await host.MoveStage(ResultAssert.Succeeded(await host.GetJobApplication(application.Id)), "Selected"));
        }

        var current = ResultAssert.Succeeded(await host.GetJob(job.Id));
        var update = host.Get<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>>();
        var tooFew = await update.HandleAsync(job.Id, RecruitmentFixtures.JobUpdateOf(current, vacancies: 1), default);
        var enough = await update.HandleAsync(job.Id, RecruitmentFixtures.JobUpdateOf(current, vacancies: 2), default);

        ResultAssert.FailedOnField(tooFew, "vacancies", "2 candidate(s) are already selected for this job.");
        Assert.Equal(2, ResultAssert.Succeeded(enough).Vacancies);
    }

    // ---- Delete protection ----

    [Fact]
    public async Task An_opening_with_applications_cannot_be_deleted_and_one_without_can()
    {
        var seed = await host.SeedRecruitmentAsync();
        var used = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var free = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        var candidate = await host.CreateCandidateAsync();
        await host.ApplyAsync(used.Id, candidate.Id);
        var delete = host.Get<IDeleteHandler<JobOpeningDto>>();

        var refused = await delete.HandleAsync(used.Id, Requests.Version(used.RowVersion), default);
        var removed = await delete.HandleAsync(free.Id, Requests.Version(free.RowVersion), default);

        ResultAssert.Failed(
            refused,
            ErrorType.Conflict,
            "\"JOB-2026-001 Backend Engineer\" cannot be deleted because it is used by 1 application(s). Deactivate it instead.");
        Assert.True(removed.IsSuccess);
        ResultAssert.Failed(await host.GetJob(free.Id), ErrorType.NotFound);
    }

    // ---- List ----

    [Fact]
    public async Task The_list_filters_by_status_searches_the_code_and_title_and_refuses_unknown_sorts()
    {
        var seed = await host.SeedRecruitmentAsync();
        await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Backend Engineer"));
        await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));

        var open = ResultAssert.Succeeded(await Query(new JobOpeningFilter(Status: "Open")));
        var all = ResultAssert.Succeeded(await Query(new JobOpeningFilter(Status: "all")));
        var unknownStatus = ResultAssert.Succeeded(await Query(new JobOpeningFilter(Status: "Bogus")));
        var byTitle = ResultAssert.Succeeded(await Query(search: "Design"));
        var byCode = ResultAssert.Succeeded(await Query(search: "JOB-2026-001"));
        var sorted = ResultAssert.Succeeded(await Query(sort: new SortState("title", "desc")));
        var badSort = await Query(sort: new SortState("salary", "asc"));

        Assert.Equal("Backend Engineer", Assert.Single(open.Items).Title);
        Assert.Equal(2, all.Total);
        Assert.Empty(unknownStatus.Items);
        Assert.Equal("Designer", Assert.Single(byTitle.Items).Title);
        Assert.Equal("Backend Engineer", Assert.Single(byCode.Items).Title);
        Assert.Equal(["Designer", "Backend Engineer"], sorted.Items.Select(job => job.Title));
        ResultAssert.FailedOnField(badSort, "sort");
    }
}
