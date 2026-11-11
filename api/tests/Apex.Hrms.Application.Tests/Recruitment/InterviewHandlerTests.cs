using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.Views;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Recruitment;

/// <summary>Interviews: scheduling rules, the business clock, double booking, the outcome, cancelling and deleting; plus the read views.</summary>
public sealed class InterviewHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly Today => RecruitmentFixtures.Today;

    /// <summary>An open opening with an application in Screening.</summary>
    private async Task<(RecruitmentSeed Seed, JobApplicationDto Application)> SeedAsync(string email = "ada.test@example.test")
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, vacancies: 3));
        var candidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate(email));
        var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        return (seed, application);
    }

    private Task<Result<InterviewDto>> Update(InterviewDto current, InterviewUpdateRequest request) =>
        host.Get<IUpdateHandler<InterviewUpdateRequest, InterviewDto>>().HandleAsync(current.Id, request, default);

    // ---- Scheduling ----

    [Fact]
    public async Task Scheduling_creates_a_scheduled_interview_and_moves_a_screened_application_to_interview()
    {
        var (seed, application) = await SeedAsync();

        var interview = ResultAssert.Succeeded(await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id)));

        Assert.Equal("Scheduled", interview.Status);
        Assert.Equal("Technical", interview.Round);
        Assert.Equal(new TimeOnly(10, 0), interview.StartTime);
        Assert.Null(interview.Score);
        Assert.Null(interview.Recommendation);
        Assert.Equal("Interview", ResultAssert.Succeeded(await host.GetJobApplication(application.Id)).Stage);
    }

    [Theory]
    [InlineData("Applied")]
    [InlineData("Selected")]
    [InlineData("Rejected")]
    public async Task A_scheduled_interview_needs_an_application_in_screening_or_interview(string stage)
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await host.CreateCandidateAsync();
        var application = await host.ApplyAsync(job.Id, candidate.Id);
        if (stage == "Selected")
        {
            (_, _, application, _) = await host.ReadyToSelectAsync(seed, vacancies: 2, email: "ready@example.test");
            application = ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));
        }
        else if (stage == "Rejected")
        {
            application = await host.MoveThroughAsync(application, "Rejected");
        }

        var result = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));

        ResultAssert.FailedOnField(
            result,
            "applicationId",
            $"Interviews can be scheduled for candidates in Screening or Interview (this one is {stage}).");
    }

    [Fact]
    public async Task A_new_interview_cannot_be_scheduled_in_the_past_by_the_business_clock()
    {
        var (seed, application) = await SeedAsync();

        var yesterday = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(-1)));
        var today = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));
        host.Clock.Today = Today.AddDays(3);
        var late = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(2), startHour: 15));

        ResultAssert.FailedOnField(yesterday, "date", "A scheduled interview cannot be in the past.");
        Assert.True(today.IsSuccess);
        ResultAssert.FailedOnField(late, "date", "A scheduled interview cannot be in the past.");
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public async Task The_duration_must_be_between_15_and_240_minutes(int minutes)
    {
        var (seed, application) = await SeedAsync();

        var result = await host.CreateInterview(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, minutes: minutes));

        ResultAssert.FailedOnField(result, "durationMinutes", "The duration must be between 15 and 240 minutes.");
    }

    [Fact]
    public async Task The_application_and_the_interviewer_must_exist_and_the_interviewer_must_be_a_current_employee()
    {
        var (seed, application) = await SeedAsync();
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id);

        var noApplication = await host.CreateInterview(request with { ApplicationId = 999 });
        var noInterviewer = await host.CreateInterview(request with { InterviewerId = 999 });
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(seed.Interviewer.Id, Requests.Version(seed.Interviewer.RowVersion), new DateOnly(2026, 11, 1), "Terminated")));
        var left = await host.CreateInterview(request);

        ResultAssert.FailedOnField(noApplication, "applicationId", "Choose an application.");
        ResultAssert.FailedOnField(noInterviewer, "interviewerId", "Choose a current employee as interviewer.");
        ResultAssert.FailedOnField(left, "interviewerId", "Choose a current employee as interviewer.");
    }

    [Fact]
    public async Task An_unknown_round_or_mode_or_status_is_refused()
    {
        var (seed, application) = await SeedAsync();
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id);

        ResultAssert.FailedOnField(await host.CreateInterview(request with { Round = "Coffee" }), "round");
        ResultAssert.FailedOnField(await host.CreateInterview(request with { Mode = "Carrier pigeon" }), "mode");
        ResultAssert.FailedOnField(await host.CreateInterview(request with { Status = "Cancelled" }), "status");
    }

    // ---- Recording a past interview ----

    [Fact]
    public async Task A_completed_interview_can_be_recorded_for_a_past_date()
    {
        var (seed, application) = await SeedAsync();
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(-2)) with
        {
            Status = "Completed",
            Score = 5,
            Recommendation = "Strong hire",
            Feedback = " Excellent. ",
        };

        var interview = ResultAssert.Succeeded(await host.CreateInterview(request));

        Assert.Equal("Completed", interview.Status);
        Assert.Equal(5, interview.Score);
        Assert.Equal("Strong hire", interview.Recommendation);
        Assert.Equal("Excellent.", interview.Feedback);
    }

    [Fact]
    public async Task A_completed_interview_needs_a_score_and_a_recommendation_and_cannot_be_in_the_future()
    {
        var (seed, application) = await SeedAsync();
        var past = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(-1)) with { Status = "Completed" };

        var noScore = await host.CreateInterview(past with { Recommendation = "Hire" });
        var badScore = await host.CreateInterview(past with { Score = 6, Recommendation = "Hire" });
        var noRecommendation = await host.CreateInterview(past with { Score = 3 });
        var future = await host.CreateInterview(past with { Date = Today.AddDays(1), Score = 3, Recommendation = "Hire" });

        ResultAssert.FailedOnField(noScore, "score", "A completed interview needs a score from 1 to 5.");
        ResultAssert.FailedOnField(badScore, "score");
        ResultAssert.FailedOnField(noRecommendation, "recommendation", "A completed interview needs a recommendation.");
        ResultAssert.FailedOnField(future, "status", "A future interview cannot be marked completed.");
    }

    [Fact]
    public async Task A_scheduled_interview_cannot_carry_a_score_or_recommendation()
    {
        var (seed, application) = await SeedAsync();
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id);

        var score = await host.CreateInterview(request with { Score = 3 });
        var recommendation = await host.CreateInterview(request with { Recommendation = "Hire" });

        ResultAssert.FailedOnField(score, "score", "Only completed interviews can have a score or recommendation.");
        ResultAssert.FailedOnField(recommendation, "score");
    }

    // ---- Double booking ----

    [Fact]
    public async Task An_interviewer_cannot_be_double_booked()
    {
        var (seed, application) = await SeedAsync();
        await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, startHour: 10, minutes: 30);

        var overlap = await host.CreateInterview(request);
        var inside = await host.CreateInterview(request with { StartTime = new TimeOnly(10, 30) });
        var before = await host.CreateInterview(request with { StartTime = new TimeOnly(9, 45), DurationMinutes = 30 });

        const string Message = "The interviewer already has an interview at 10:00 on that day.";
        ResultAssert.FailedOnField(overlap, "startTime", Message);
        ResultAssert.FailedOnField(inside, "startTime", Message);
        ResultAssert.FailedOnField(before, "startTime", Message);
    }

    [Fact]
    public async Task Back_to_back_interviews_other_days_other_interviewers_and_cancelled_ones_do_not_clash()
    {
        var (seed, application) = await SeedAsync();
        var org = seed.Org;
        var colleague = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "colleague@example.test"));
        var first = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var request = RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id);

        var after = await host.CreateInterview(request with { StartTime = new TimeOnly(11, 0) });
        var otherDay = await host.CreateInterview(request with { Date = Today.AddDays(3) });
        var otherPerson = await host.CreateInterview(request with { InterviewerId = colleague.Id });
        ResultAssert.Succeeded(await host.CancelInterview(first));
        var reuse = await host.CreateInterview(request with { StartTime = new TimeOnly(10, 30), DurationMinutes = 15 });

        Assert.True(after.IsSuccess);
        Assert.True(otherDay.IsSuccess);
        Assert.True(otherPerson.IsSuccess);
        Assert.True(reuse.IsSuccess);
    }

    // ---- Update ----

    [Fact]
    public async Task A_scheduled_interview_can_be_rescheduled_but_not_into_a_clash_or_the_past()
    {
        var (seed, application) = await SeedAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var other = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, startHour: 14));

        var sameSlot = await Update(interview, RecruitmentFixtures.InterviewUpdateOf(interview) with { Location = "Room 9" });
        var clash = await Update(other, RecruitmentFixtures.InterviewUpdateOf(other, startHour: 10));
        var past = await Update(other, RecruitmentFixtures.InterviewUpdateOf(other, date: Today.AddDays(-1)));
        var moved = await Update(other, RecruitmentFixtures.InterviewUpdateOf(other, date: Today.AddDays(5)));

        Assert.Equal("Room 9", ResultAssert.Succeeded(sameSlot).Location);
        ResultAssert.FailedOnField(clash, "startTime");
        ResultAssert.FailedOnField(past, "date", "A scheduled interview cannot be in the past.");
        Assert.Equal(Today.AddDays(5), ResultAssert.Succeeded(moved).Date);
    }

    [Fact]
    public async Task A_cancelled_interview_is_locked()
    {
        var (seed, application) = await SeedAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var cancelled = ResultAssert.Succeeded(await host.CancelInterview(interview, "Candidate withdrew"));
        const string Locked = "A cancelled interview cannot be changed. Schedule a new one instead.";

        ResultAssert.Failed(await Update(cancelled, RecruitmentFixtures.InterviewUpdateOf(cancelled)), ErrorType.Conflict, Locked);
        ResultAssert.Failed(await host.Outcome(cancelled), ErrorType.Conflict, Locked);
        ResultAssert.Failed(await host.CancelInterview(cancelled), ErrorType.Conflict, Locked);
        Assert.Equal("Candidate withdrew", cancelled.Feedback);
    }

    [Fact]
    public async Task The_application_of_an_interview_cannot_be_changed()
    {
        var (seed, application) = await SeedAsync();
        var other = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("other@example.test", first: "Other"));
        var otherApplication = await host.MoveThroughAsync(await host.ApplyAsync(application.JobOpeningId, other.Id), "Screening");
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));

        var result = await Update(interview, RecruitmentFixtures.InterviewUpdateOf(interview) with { ApplicationId = otherApplication.Id });

        ResultAssert.FailedOnField(result, "applicationId", "The application of an interview cannot be changed.");
    }

    [Fact]
    public async Task An_update_needs_a_current_interviewer_and_a_row_version()
    {
        var (seed, application) = await SeedAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));

        var unknown = await Update(interview, RecruitmentFixtures.InterviewUpdateOf(interview, interviewerId: 999));
        var noVersion = await Update(interview, RecruitmentFixtures.InterviewUpdateOf(interview) with { RowVersion = null });

        ResultAssert.FailedOnField(unknown, "interviewerId");
        ResultAssert.FailedOnField(noVersion, "rowVersion");
    }

    // ---- Outcome ----

    [Fact]
    public async Task Recording_the_outcome_completes_the_interview()
    {
        var (seed, application) = await SeedAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));

        var completed = ResultAssert.Succeeded(await host.Outcome(interview, 3, "Hire", "  Solid. "));

        Assert.Equal("Completed", completed.Status);
        Assert.Equal((3, "Hire", "Solid."), (completed.Score, completed.Recommendation, completed.Feedback));
        Assert.NotEqual(interview.RowVersion, completed.RowVersion);
    }

    [Fact]
    public async Task The_outcome_needs_a_valid_score_a_recommendation_and_a_date_that_has_come()
    {
        var (seed, application) = await SeedAsync();
        var future = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id));
        var today = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today, startHour: 15));

        ResultAssert.FailedOnField(await host.Outcome(future), "status", "A future interview cannot be marked completed.");
        ResultAssert.FailedOnField(await host.Outcome(today, 0), "score");
        ResultAssert.FailedOnField(await host.Outcome(today, 6), "score");
        ResultAssert.FailedOnField(await host.Outcome(today, null), "score");
        ResultAssert.FailedOnField(await host.Outcome(today, 3, null), "recommendation");
        ResultAssert.FailedOnField(await host.Outcome(today, 3, "Maybe"), "recommendation");
        ResultAssert.FailedOnField(await host.Outcome(today, 3, "Hire", new string('x', 501)), "feedback");
    }

    [Fact]
    public async Task A_selected_candidate_keeps_the_recommendation_that_supports_the_selection()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (_, _, application, interview) = await host.ReadyToSelectAsync(seed, vacancies: 2);
        ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));

        var downgrade = await host.Outcome(interview, 2, "No hire");
        var keep = await host.Outcome(interview, 5, "Strong hire");

        ResultAssert.Failed(
            downgrade,
            ErrorType.Conflict,
            "This interview supports the selection of the candidate. Reject the application first, or keep a \"Hire\" recommendation.");
        Assert.Equal("Strong hire", ResultAssert.Succeeded(keep).Recommendation);
    }

    // ---- Cancel and delete ----

    [Fact]
    public async Task Only_a_scheduled_interview_can_be_cancelled()
    {
        var (seed, application) = await SeedAsync();
        var interview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));
        var completed = ResultAssert.Succeeded(await host.Outcome(interview));

        var refused = await host.CancelInterview(completed);

        ResultAssert.Failed(refused, ErrorType.Conflict, "A completed interview cannot be cancelled.");
    }

    [Fact]
    public async Task A_completed_interview_cannot_be_deleted_but_scheduled_and_cancelled_ones_can()
    {
        var (seed, application) = await SeedAsync();
        var done = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today));
        var completed = ResultAssert.Succeeded(await host.Outcome(done));
        var scheduled = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, startHour: 14));
        var toCancel = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, startHour: 16));
        var cancelled = ResultAssert.Succeeded(await host.CancelInterview(toCancel));
        var delete = host.Get<IDeleteHandler<InterviewDto>>();

        var refused = await delete.HandleAsync(completed.Id, Requests.Version(completed.RowVersion), default);
        var removedScheduled = await delete.HandleAsync(scheduled.Id, Requests.Version(scheduled.RowVersion), default);
        var removedCancelled = await delete.HandleAsync(cancelled.Id, Requests.Version(cancelled.RowVersion), default);

        ResultAssert.Failed(refused, ErrorType.Conflict, "A completed interview is part of the hiring record and cannot be deleted.");
        Assert.True(removedScheduled.IsSuccess);
        Assert.True(removedCancelled.IsSuccess);
    }

    // ---- List ----

    [Fact]
    public async Task Interviews_filter_by_status_interviewer_application_and_date_range()
    {
        var (seed, application) = await SeedAsync();
        var first = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(1)));
        await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(application.Id, seed.Interviewer.Id, Today.AddDays(5)));
        ResultAssert.Succeeded(await host.CancelInterview(first));
        var query = host.Get<IQueryHandler<InterviewDto, InterviewFilter>>();

        Task<Result<PagedResult<InterviewDto>>> Run(InterviewFilter filter, string? search = null) =>
            query.HandleAsync(new PageRequest<InterviewFilter>(0, 20, null, search, filter), default);

        Assert.Equal(1, ResultAssert.Succeeded(await Run(new InterviewFilter(Status: "Cancelled"))).Total);
        Assert.Equal(2, ResultAssert.Succeeded(await Run(new InterviewFilter(InterviewerId: seed.Interviewer.Id))).Total);
        Assert.Equal(0, ResultAssert.Succeeded(await Run(new InterviewFilter(InterviewerId: seed.Manager.Id))).Total);
        Assert.Equal(2, ResultAssert.Succeeded(await Run(new InterviewFilter(ApplicationId: application.Id))).Total);
        Assert.Equal(1, ResultAssert.Succeeded(await Run(new InterviewFilter(DateFrom: Today.AddDays(3), DateTo: Today.AddDays(6)))).Total);
        Assert.Equal(2, ResultAssert.Succeeded(await Run(new InterviewFilter(), search: "Meeting")).Total);
    }

    // ---- Views ----

    [Fact]
    public async Task The_pipeline_groups_applications_by_stage_with_interview_details()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, vacancies: 2));
        var other = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Designer"));
        var zed = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("zed@example.test", first: "Zed", years: 7m));
        var ada = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("ada@example.test", first: "Ada", years: 3m));
        var bob = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("bob@example.test", first: "Bob"));
        await host.ApplyAsync(job.Id, zed.Id);
        var adaApplication = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, ada.Id), "Screening");
        await host.ApplyAsync(other.Id, bob.Id);
        var next = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(adaApplication.Id, seed.Interviewer.Id, Today.AddDays(2), startHour: 11, round: "HR"));
        var done = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(adaApplication.Id, seed.Interviewer.Id, Today, startHour: 9));
        ResultAssert.Succeeded(await host.Outcome(done));
        var handler = host.Get<IRecruitmentPipelineHandler>();

        var forJob = ResultAssert.Succeeded(await handler.HandleAsync(job.Id, default));
        var everything = ResultAssert.Succeeded(await handler.HandleAsync(null, default));

        Assert.Equal(["Applied", "Screening", "Interview", "Selected", "Rejected"], forJob.Columns.Select(column => column.Stage));
        var applied = Assert.Single(forJob.Columns, column => column.Stage == "Applied");
        Assert.Equal(1, applied.Count);
        Assert.Equal("Zed Test", Assert.Single(applied.Cards).CandidateName);
        Assert.Equal(7m, applied.Cards[0].ExperienceYears);
        Assert.Equal("Backend Engineer", applied.Cards[0].JobTitle);
        var card = Assert.Single(Assert.Single(forJob.Columns, column => column.Stage == "Interview").Cards);
        Assert.Equal("Ada Test", card.CandidateName);
        Assert.Equal(1, card.CompletedInterviews);
        Assert.Equal(next.Id, card.NextInterview!.Id);
        Assert.Equal("HR", card.NextInterview.Round);
        Assert.Equal(Today.AddDays(2), card.NextInterview.Date);
        Assert.Equal(3, everything.Columns.Sum(column => column.Count));
        Assert.Null(everything.JobOpeningId);
        Assert.Equal(job.Id, forJob.JobOpeningId);
    }

    [Fact]
    public async Task The_pipeline_of_an_unknown_opening_is_not_found_and_applied_cards_are_ordered_by_name()
    {
        var seed = await host.SeedRecruitmentAsync();
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        foreach (var (email, first) in new[] { ("z@example.test", "Zoe"), ("a@example.test", "Amy") })
        {
            var candidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate(email, first));
            await host.ApplyAsync(job.Id, candidate.Id);
        }

        var handler = host.Get<IRecruitmentPipelineHandler>();
        var unknown = await handler.HandleAsync(999, default);
        var pipeline = ResultAssert.Succeeded(await handler.HandleAsync(job.Id, default));

        ResultAssert.Failed(unknown, ErrorType.NotFound, "Job opening #999 was not found.");
        Assert.Equal(["Amy Test", "Zoe Test"], pipeline.Columns[0].Cards.Select(card => card.CandidateName));
    }

    [Fact]
    public async Task The_counts_list_applications_and_selected_candidates_per_opening_and_applications_per_candidate()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (job, ada, application, _) = await host.ReadyToSelectAsync(seed, vacancies: 2);
        ResultAssert.Succeeded(await host.MoveStage(application, "Selected"));
        var bob = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("bob@example.test", first: "Bob"));
        await host.ApplyAsync(job.Id, bob.Id);
        await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("idle@example.test", first: "Idle"));

        var counts = ResultAssert.Succeeded(await host.Get<IRecruitmentCountsHandler>().HandleAsync(default));

        var opening = Assert.Single(counts.JobOpenings);
        Assert.Equal((job.Id, 2, 1), (opening.JobOpeningId, opening.Applications, opening.Selected));
        Assert.Equal(2, counts.Candidates.Count);
        Assert.Equal(1, counts.Candidates.Single(candidate => candidate.CandidateId == ada.Id).Applications);
    }
}
