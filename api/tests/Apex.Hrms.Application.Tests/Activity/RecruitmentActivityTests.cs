using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Activity;

/// <summary>
/// What the recruitment write paths record in the activity feed: the category (recruitment, not the generic system) and
/// the wording of the frontend mock services, and that a refused change leaves no entry. Wording uses the name of the
/// candidate and the code or title of the opening, as the mock does, and no email, phone or salary.
/// </summary>
public sealed class RecruitmentActivityTests : IDisposable
{
    private const string Label = "Ada Test for Backend Engineer";

    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly Today => RecruitmentFixtures.Today;

    private (string Category, string Message) Last()
    {
        var entry = host.ActivityLog.Entries[^1];
        return (entry.Category, entry.Message);
    }

    private void AssertAllRecruitment() =>
        Assert.All(host.ActivityLog.Entries, entry => Assert.Equal(ActivityCategories.Recruitment, entry.Category));

    /// <summary>An open opening (one vacancy) with an application in Screening.</summary>
    private async Task<(RecruitmentSeed Seed, JobOpeningDto Job, JobApplicationDto Application)> ScreeningAsync()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        return (seed, job, application);
    }

    // ---- Candidates (regression: they were logged under "system") ----

    [Fact]
    public async Task Candidate_create_update_and_delete_are_logged_under_recruitment_with_the_name_only()
    {
        var candidate = await host.CreateCandidateAsync();
        Assert.Equal((ActivityCategories.Recruitment, "Candidate \"Ada Test\" created"), Last());

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>()
            .HandleAsync(candidate.Id, RecruitmentFixtures.CandidateUpdateOf(candidate, years: 6m), default));
        Assert.Equal((ActivityCategories.Recruitment, "Candidate \"Ada Test\" updated"), Last());

        var removed = await host.Get<IDeleteHandler<CandidateDto>>()
            .HandleAsync(updated.Id, Requests.Version(updated.RowVersion), default);
        Assert.True(removed.IsSuccess);
        Assert.Equal((ActivityCategories.Recruitment, "Candidate \"Ada Test\" deleted"), Last());

        Assert.Equal(3, host.ActivityLog.Entries.Count);
        AssertAllRecruitment();
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("example.test") || message.Contains("555"));
    }

    // ---- Job openings ----

    [Fact]
    public async Task Job_opening_create_update_and_delete_use_the_mock_wording()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{job.Code} Backend Engineer\" created"), Last());

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>>()
            .HandleAsync(job.Id, RecruitmentFixtures.JobUpdateOf(job, title: "Platform Engineer"), default));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{job.Code} Platform Engineer\" updated"), Last());

        var removed = await host.Get<IDeleteHandler<JobOpeningDto>>()
            .HandleAsync(updated.Id, Requests.Version(updated.RowVersion), default);
        Assert.True(removed.IsSuccess);
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{job.Code} Platform Engineer\" deleted"), Last());
    }

    [Fact]
    public async Task Job_opening_status_commands_log_set_to_the_new_status()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        var subject = $"{job.Code} Backend Engineer";
        var baseline = host.ActivityLog.Entries.Count;

        job = ResultAssert.Succeeded(await host.OpenJob(job));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{subject}\" set to Open"), Last());

        job = ResultAssert.Succeeded(await host.Command<HoldJobOpeningCommand, JobOpeningDto>(
            new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{subject}\" set to On Hold"), Last());

        job = ResultAssert.Succeeded(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(
            new CloseJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{subject}\" set to Closed"), Last());

        ResultAssert.Succeeded(await host.Command<ReopenJobOpeningCommand, JobOpeningDto>(
            new ReopenJobOpeningCommand(job.Id, Requests.Version(job.RowVersion))));
        Assert.Equal((ActivityCategories.Recruitment, $"Job opening \"{subject}\" set to Open"), Last());
        Assert.Equal(baseline + 4, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task A_refused_job_opening_status_move_logs_nothing()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.CreateJobAsync(RecruitmentFixtures.NewJob(seed));
        var before = host.ActivityLog.Entries.Count;

        var result = await host.Command<HoldJobOpeningCommand, JobOpeningDto>(
            new HoldJobOpeningCommand(job.Id, Requests.Version(job.RowVersion)));

        Assert.True(result.IsFailure);
        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }

    // ---- Applications ----

    [Fact]
    public async Task Application_create_update_and_delete_use_the_mock_wording()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();

        var application = await host.ApplyAsync(job.Id, candidate.Id);
        Assert.Equal((ActivityCategories.Recruitment, $"Application \"{Label}\" created"), Last());

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>>()
            .HandleAsync(
                application.Id,
                new JobApplicationUpdateRequest(job.Id, candidate.Id, "Strong CV", Requests.Version(application.RowVersion)),
                default));
        Assert.Equal((ActivityCategories.Recruitment, $"Application \"{Label}\" updated"), Last());

        var removed = await host.Get<IDeleteHandler<JobApplicationDto>>()
            .HandleAsync(updated.Id, Requests.Version(updated.RowVersion), default);
        Assert.True(removed.IsSuccess);
        Assert.Equal((ActivityCategories.Recruitment, $"Application \"{Label}\" deleted"), Last());
    }

    [Fact]
    public async Task Moving_an_application_logs_the_stage_with_the_candidate_and_job()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);

        ResultAssert.Succeeded(await host.MoveStage(application, "Screening"));

        Assert.Equal((ActivityCategories.Recruitment, $"{Label} moved to Screening"), Last());
    }

    [Fact]
    public async Task A_refused_stage_move_logs_nothing()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);
        var before = host.ActivityLog.Entries.Count;

        var invalidTransition = await host.MoveStage(application, "Selected");
        var noReason = await host.MoveStage(application, "Rejected");

        Assert.True(invalidTransition.IsFailure);
        Assert.True(noReason.IsFailure);
        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task Selecting_the_last_candidate_logs_the_opening_as_filled_then_the_move_and_rejecting_reopens_it()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (job, _, application, _) = await host.ReadyToSelectAsync(seed);

        var selected = ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));

        var tail = host.ActivityLog.Messages.TakeLast(2).ToArray();
        Assert.Equal([$"Job opening \"{job.Code} Backend Engineer\" filled", $"{Label} moved to Selected"], tail);

        ResultAssert.Succeeded(await host.MoveStage(selected, "Rejected", "Withdrew"));

        tail = [.. host.ActivityLog.Messages.TakeLast(2)];
        Assert.Equal([$"Job opening \"{job.Code} Backend Engineer\" reopened", $"{Label} moved to Rejected"], tail);
        Assert.All(host.ActivityLog.Entries.TakeLast(2), entry => Assert.Equal(ActivityCategories.Recruitment, entry.Category));
    }

    [Fact]
    public async Task Rejecting_an_application_logs_only_the_move_not_the_interviews_it_cancels()
    {
        var (seed, _, application) = await ScreeningAsync();
        await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));
        var before = host.ActivityLog.Entries.Count;

        ResultAssert.Succeeded(await host.MoveStage(application, "Rejected", "Not a fit"));

        Assert.Equal(before + 1, host.ActivityLog.Entries.Count);
        Assert.Equal($"{Label} moved to Rejected", Last().Message);
    }

    // ---- Interviews ----

    [Fact]
    public async Task Interview_create_update_outcome_cancel_and_delete_use_the_mock_wording()
    {
        var (seed, _, application) = await ScreeningAsync();
        var subject = $"Technical · {Label}";

        var first = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));
        Assert.Equal((ActivityCategories.Recruitment, $"Interview \"{subject}\" created"), Last());

        var moved = ResultAssert.Succeeded(await host.Get<IUpdateHandler<InterviewUpdateRequest, InterviewDto>>()
            .HandleAsync(first.Id, RecruitmentFixtures.InterviewUpdateOf(first, startHour: 11), default));
        Assert.Equal((ActivityCategories.Recruitment, $"Interview \"{subject}\" updated"), Last());

        ResultAssert.Succeeded(await host.Outcome(moved));
        Assert.Equal((ActivityCategories.Recruitment, $"Interview \"{subject}\" updated"), Last());

        var second = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, startHour: 15));
        var cancelled = ResultAssert.Succeeded(await host.CancelInterview(second, "Candidate unavailable"));
        Assert.Equal((ActivityCategories.Recruitment, $"Interview \"{subject}\" updated"), Last());

        var removed = await host.Get<IDeleteHandler<InterviewDto>>()
            .HandleAsync(cancelled.Id, Requests.Version(cancelled.RowVersion), default);
        Assert.True(removed.IsSuccess);
        Assert.Equal((ActivityCategories.Recruitment, $"Interview \"{subject}\" deleted"), Last());
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("unavailable"));
    }

    [Fact]
    public async Task A_refused_interview_change_logs_nothing()
    {
        var (seed, _, application) = await ScreeningAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var cancelled = ResultAssert.Succeeded(await host.CancelInterview(interview));
        var before = host.ActivityLog.Entries.Count;

        var outcome = await host.Outcome(cancelled);
        var cancelAgain = await host.CancelInterview(cancelled);
        var inThePast = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(-1)));

        Assert.True(outcome.IsFailure);
        Assert.True(cancelAgain.IsFailure);
        Assert.True(inThePast.IsFailure);
        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }
}
