using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Recruitment;

/// <summary>Candidates (unique lower-case email, experience, delete protection) and applications (creating, stage moves, selecting, deleting).</summary>
public sealed class CandidateAndApplicationHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly Today => RecruitmentFixtures.Today;

    private Task<Result<CandidateDto>> UpdateCandidate(CandidateDto current, string? email = null, decimal? years = null) =>
        host.Get<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>()
            .HandleAsync(current.Id, RecruitmentFixtures.CandidateUpdateOf(current, email, years), default);

    // ---- Candidates ----

    [Fact]
    public async Task The_email_is_stored_trimmed_and_in_lower_case()
    {
        var candidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("  Ada.Test@Example.TEST "));

        Assert.Equal("ada.test@example.test", candidate.Email);
        Assert.Equal("Referral", candidate.Source);
    }

    [Fact]
    public async Task The_email_is_unique_regardless_of_case()
    {
        await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("ada.test@example.test"));

        var duplicate = await host.CreateCandidate(RecruitmentFixtures.NewCandidate("ADA.Test@example.test", first: "Other"));

        ResultAssert.FailedOnField(duplicate, "email", "Email \"ada.test@example.test\" already exists.");
    }

    [Fact]
    public async Task A_candidate_can_keep_its_own_email_but_not_take_another_candidates()
    {
        var ada = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("ada.test@example.test"));
        var bob = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("bob.test@example.test", first: "Bob"));

        var same = await UpdateCandidate(ada, years: 6m);
        var taken = await UpdateCandidate(bob, email: "Ada.Test@example.test");

        Assert.Equal(6m, ResultAssert.Succeeded(same).ExperienceYears);
        ResultAssert.FailedOnField(taken, "email");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(50.5)]
    public async Task Experience_must_be_between_0_and_50_years(double years)
    {
        var result = await host.CreateCandidate(RecruitmentFixtures.NewCandidate(years: (decimal)years));

        ResultAssert.FailedOnField(result, "experienceYears");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public async Task The_experience_bounds_are_accepted(double years) =>
        Assert.True((await host.CreateCandidate(RecruitmentFixtures.NewCandidate(years: (decimal)years))).IsSuccess);

    [Fact]
    public async Task An_unknown_source_a_bad_email_and_missing_names_are_refused()
    {
        var request = RecruitmentFixtures.NewCandidate();

        var source = await host.CreateCandidate(request with { Source = "Billboard" });
        var email = await host.CreateCandidate(request with { Email = "not-an-email" });
        var names = await host.CreateCandidate(request with { FirstName = " ", Phone = "" });

        ResultAssert.FailedOnField(source, "source");
        ResultAssert.FailedOnField(email, "email");
        ResultAssert.FailedOnField(names, "firstName");
        ResultAssert.FailedOnField(names, "phone");
    }

    [Fact]
    public async Task A_candidate_with_applications_cannot_be_deleted()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var used = await host.CreateCandidateAsync();
        var free = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("free@example.test", first: "Free"));
        await host.ApplyAsync(job.Id, used.Id);
        var delete = host.Get<IDeleteHandler<CandidateDto>>();

        var refused = await delete.HandleAsync(used.Id, Requests.Version(used.RowVersion), default);
        var removed = await delete.HandleAsync(free.Id, Requests.Version(free.RowVersion), default);

        ResultAssert.Failed(
            refused,
            ErrorType.Conflict,
            "\"Ada Test\" cannot be deleted because it is used by 1 application(s). Deactivate it instead.");
        Assert.True(removed.IsSuccess);
    }

    // ---- Applying ----

    [Fact]
    public async Task A_new_application_is_applied_today_and_starts_at_applied()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        host.Clock.Today = Today.AddDays(1);

        var application = ResultAssert.Succeeded(await host.Apply(job.Id, candidate.Id, "  Referred.  "));

        Assert.Equal("Applied", application.Stage);
        Assert.Equal(Today.AddDays(1), application.AppliedOn);
        Assert.Null(application.RejectionReason);
        Assert.Equal("Referred.", application.Notes);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Closed")]
    public async Task An_application_can_only_open_for_an_open_opening(string status)
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        if (status == "Closed")
        {
            job = ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        }

        var candidate = await host.CreateCandidateAsync();

        var result = await host.Apply(job.Id, candidate.Id);

        ResultAssert.FailedOnField(
            result,
            "jobOpeningId",
            $"\"Backend Engineer\" is {status.ToLowerInvariant()} and not accepting applications.");
    }

    [Fact]
    public async Task An_opening_on_hold_does_not_accept_applications()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        ResultAssert.Succeeded(await host.Command<HoldJobOpeningCommand, JobOpeningDto>(new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        var candidate = await host.CreateCandidateAsync();

        var result = await host.Apply(job.Id, candidate.Id);

        ResultAssert.FailedOnField(result, "jobOpeningId", "\"Backend Engineer\" is on hold and not accepting applications.");
    }

    [Fact]
    public async Task A_candidate_applies_only_once_per_opening_but_can_apply_to_another()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var other = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        var candidate = await host.CreateCandidateAsync();
        await host.ApplyAsync(job.Id, candidate.Id);

        var again = await host.Apply(job.Id, candidate.Id);
        var elsewhere = await host.Apply(other.Id, candidate.Id);

        ResultAssert.FailedOnField(again, "candidateId", "This candidate has already applied for this job.");
        Assert.True(elsewhere.IsSuccess);
    }

    [Fact]
    public async Task The_opening_and_the_candidate_must_exist()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();

        ResultAssert.FailedOnField(await host.Apply(999, candidate.Id), "jobOpeningId", "Choose a job opening.");
        ResultAssert.FailedOnField(await host.Apply(job.Id, 999), "candidateId", "Choose a candidate.");
    }

    [Fact]
    public async Task Only_the_notes_of_an_application_change()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var other = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);
        var update = host.Get<IUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>>();
        var version = Requests.Version(application.RowVersion);

        var moved = await update.HandleAsync(application.Id, new JobApplicationUpdateRequest(other.Id, candidate.Id, "x", version), default);
        var changed = await update.HandleAsync(application.Id, new JobApplicationUpdateRequest(job.Id, candidate.Id, " New note ", version), default);

        ResultAssert.Failed(moved, ErrorType.Validation, "The job opening and candidate cannot be changed.");
        Assert.Equal("New note", ResultAssert.Succeeded(changed).Notes);
    }

    // ---- Stage moves ----

    [Fact]
    public async Task An_application_walks_the_pipeline_and_a_skipped_stage_is_a_conflict()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);

        var skip = await host.MoveStage(application, "Interview");
        var screening = ResultAssert.Succeeded(await host.MoveStage(application, "Screening"));
        var backwards = await host.MoveStage(screening, "Applied");
        var unknown = await host.MoveStage(screening, "Hired");

        ResultAssert.Failed(skip, ErrorType.Conflict, "An application at \"Applied\" cannot move to \"Interview\".");
        Assert.Equal("Screening", screening.Stage);
        ResultAssert.Failed(backwards, ErrorType.Conflict, "An application at \"Screening\" cannot move to \"Applied\".");
        ResultAssert.FailedOnField(unknown, "stage");
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_which_is_kept_and_a_rejected_application_goes_nowhere()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);

        var blank = await host.MoveStage(application, "Rejected", "   ");
        var tooLong = await host.MoveStage(application, "Rejected", new string('x', 201));
        var rejected = ResultAssert.Succeeded(await host.MoveStage(application, "Rejected", "  Salary expectations  "));
        var after = await host.MoveStage(rejected, "Screening");

        ResultAssert.FailedOnField(blank, "reason", "Enter a reason for rejecting this application.");
        ResultAssert.FailedOnField(tooLong, "reason");
        Assert.Equal("Rejected", rejected.Stage);
        Assert.Equal("Salary expectations", rejected.RejectionReason);
        ResultAssert.Failed(after, ErrorType.Conflict, "An application at \"Rejected\" cannot move to \"Screening\".");
    }

    [Fact]
    public async Task Moving_forward_needs_an_open_or_on_hold_opening_but_rejecting_always_works()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);
        ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));

        var forward = await host.MoveStage(application, "Screening");
        var reject = await host.MoveStage(application, "Rejected", "Position closed");

        ResultAssert.Failed(forward, ErrorType.Conflict, "\"Backend Engineer\" is closed, so applications cannot move forward.");
        Assert.True(reject.IsSuccess);
    }

    [Fact]
    public async Task An_opening_on_hold_lets_applications_move_forward()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);
        ResultAssert.Succeeded(await host.Command<HoldJobOpeningCommand, JobOpeningDto>(new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));

        Assert.True((await host.MoveStage(application, "Screening")).IsSuccess);
    }

    // ---- Selecting ----

    [Fact]
    public async Task Selecting_needs_a_completed_interview_with_a_positive_recommendation()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));
        const string Needs = "Selecting a candidate needs a completed interview with a \"Hire\" recommendation.";

        var scheduledOnly = await host.MoveStage(application, "Selected");
        interview = ResultAssert.Succeeded(await host.Outcome(interview, 2, "No hire"));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));
        var noHire = await host.MoveStage(application, "Selected");
        interview = ResultAssert.Succeeded(await host.Outcome(interview, 5, "Strong hire"));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));
        var selected = await host.MoveStage(application, "Selected");

        ResultAssert.Failed(scheduledOnly, ErrorType.Conflict, Needs);
        ResultAssert.Failed(noHire, ErrorType.Conflict, Needs);
        Assert.Equal("Selected", ResultAssert.Succeeded(selected).Stage);
    }

    [Fact]
    public async Task Selecting_the_last_vacancy_fills_the_opening_and_rejecting_the_selection_reopens_it()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (job, _, application, _) = await host.ReadyToSelectAsync(seed, vacancies: 1);

        var selected = ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));
        var filled = ResultAssert.Succeeded(await host.GetJob(job.Id)).Status;
        var dropped = ResultAssert.Succeeded(await host.MoveStage(selected, "Rejected", "Accepted another offer"));
        var reopened = ResultAssert.Succeeded(await host.GetJob(job.Id)).Status;

        Assert.Equal("Filled", filled);
        Assert.Equal("Rejected", dropped.Stage);
        Assert.Equal("Open", reopened);
    }

    [Fact]
    public async Task The_opening_stays_open_until_the_selected_count_reaches_the_vacancies()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (job, _, first, _) = await host.ReadyToSelectAsync(seed, vacancies: 2, email: "first@example.test");
        ResultAssert.Succeeded(await host.MoveStage(first, "Selected"));
        Assert.Equal("Open", ResultAssert.Succeeded(await host.GetJob(job.Id)).Status);

        var candidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("second@example.test", first: "Second"));
        var second = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(second.Id, seed.Interviewer.Id, Today, startHour: 13));
        ResultAssert.Succeeded(await host.Outcome(interview));
        ResultAssert.Succeeded(await host.MoveStage(ResultAssert.Succeeded(await host.GetJobApplication(second.Id)), "Selected"));

        Assert.Equal("Filled", ResultAssert.Succeeded(await host.GetJob(job.Id)).Status);
    }

    [Fact]
    public async Task A_filled_opening_neither_accepts_applications_nor_lets_others_move_forward_nor_reopens_by_hand()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (job, _, application, _) = await host.ReadyToSelectAsync(seed, vacancies: 1);
        var other = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("other@example.test", first: "Other"));
        var waiting = await host.ApplyAsync(job.Id, other.Id);
        ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));
        var filled = ResultAssert.Succeeded(await host.GetJob(job.Id));
        var third = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("third@example.test", first: "Third"));

        var apply = await host.Apply(job.Id, third.Id);
        var move = await host.MoveStage(waiting, "Screening");
        var reopen = await host.Command<ReopenJobOpeningCommand, JobOpeningDto>(new ReopenJobOpeningCommand(job.Id, Requests.Version(filled.RowVersion)));

        ResultAssert.FailedOnField(apply, "jobOpeningId", "\"Backend Engineer\" is filled and not accepting applications.");
        ResultAssert.Failed(move, ErrorType.Conflict, "\"Backend Engineer\" is filled, so applications cannot move forward.");
        ResultAssert.Failed(reopen, ErrorType.Conflict, "A filled job opening cannot be set to Open.");
    }

    // ---- Rejection side effects ----

    [Fact]
    public async Task Rejecting_cancels_the_scheduled_interviews_of_the_application()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));

        ResultAssert.Succeeded(await host.MoveStage(application, "Rejected", "Withdrew"));

        var cancelled = ResultAssert.Succeeded(await host.GetInterview(interview.Id));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Cancelled: the application was rejected.", cancelled.Feedback);
    }

    // ---- Delete ----

    [Fact]
    public async Task A_selected_application_cannot_be_deleted_and_one_with_interviews_is_protected()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (_, _, application, _) = await host.ReadyToSelectAsync(seed);
        var delete = host.Get<IDeleteHandler<JobApplicationDto>>();
        var selected = ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));

        var refusedSelected = await delete.HandleAsync(selected.Id, Requests.Version(selected.RowVersion), default);
        var rejected = ResultAssert.Succeeded(await host.MoveStage(selected, "Rejected", "Declined"));
        var refusedInterview = await delete.HandleAsync(rejected.Id, Requests.Version(rejected.RowVersion), default);

        ResultAssert.Failed(refusedSelected, ErrorType.Conflict, "A selected application cannot be deleted. Reject it first.");
        ResultAssert.Failed(
            refusedInterview,
            ErrorType.Conflict,
            "\"Ada Test for Backend Engineer\" cannot be deleted because it is used by 1 interview(s). Deactivate it instead.");
    }

    [Fact]
    public async Task An_application_without_interviews_can_be_deleted()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);

        var removed = await host.Get<IDeleteHandler<JobApplicationDto>>()
            .HandleAsync(application.Id, Requests.Version(application.RowVersion), default);

        Assert.True(removed.IsSuccess);
        ResultAssert.Failed(await host.GetJobApplication(application.Id), ErrorType.NotFound, "Application #1 was not found.");
    }

    // ---- List ----

    [Fact]
    public async Task Applications_filter_by_opening_candidate_and_stage()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var other = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        var ada = await host.CreateCandidateAsync();
        var bob = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("bob@example.test", first: "Bob"));
        var adaApplication = await host.ApplyAsync(job.Id, ada.Id);
        await host.ApplyAsync(job.Id, bob.Id);
        await host.ApplyAsync(other.Id, ada.Id);
        ResultAssert.Succeeded(await host.MoveStage(adaApplication, "Screening"));
        var query = host.Get<IQueryHandler<JobApplicationDto, JobApplicationFilter>>();

        Task<Result<PagedResult<JobApplicationDto>>> Run(JobApplicationFilter filter) =>
            query.HandleAsync(new PageRequest<JobApplicationFilter>(0, 20, null, null, filter), default);

        Assert.Equal(2, ResultAssert.Succeeded(await Run(new JobApplicationFilter(JobOpeningId: job.Id))).Total);
        Assert.Equal(2, ResultAssert.Succeeded(await Run(new JobApplicationFilter(CandidateId: ada.Id))).Total);
        Assert.Equal(1, ResultAssert.Succeeded(await Run(new JobApplicationFilter(Stage: "Screening"))).Total);
        Assert.Equal(0, ResultAssert.Succeeded(await Run(new JobApplicationFilter(Stage: "Nonsense"))).Total);
        Assert.Equal(3, ResultAssert.Succeeded(await Run(new JobApplicationFilter(Stage: "all"))).Total);
    }

    [Fact]
    public async Task The_interview_dto_carries_the_recommendation_text()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (_, _, _, interview) = await host.ReadyToSelectAsync(seed);

        Assert.Equal("Hire", interview.Recommendation);
        Assert.Equal(4, interview.Score);
        Assert.Equal("Completed", interview.Status);
    }
}
