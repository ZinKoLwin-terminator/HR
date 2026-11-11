using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Domain.Tests.Recruitment;

public sealed class InterviewTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static InterviewDetails Details(
        DateOnly? date = null,
        int minutes = 60,
        int start = 10,
        int application = 1,
        string? location = " Room 4 ") => new(
        application,
        InterviewRound.Technical,
        date ?? Today.AddDays(2),
        new TimeOnly(start, 0),
        minutes,
        InterviewMode.Video,
        InterviewerId: 7,
        location);

    private static InterviewOutcome Hire(int score = 4) => new(score, InterviewRecommendation.Hire, "  Strong on design. ");

    private static Interview Scheduled(DateOnly? date = null) => Interview.Schedule(Details(date), Today).Value;

    private static Interview Completed() =>
        Interview.RecordCompleted(Details(Today.AddDays(-1)), Hire(), Today).Value;

    // ---- Duration ----

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    [InlineData(0)]
    public void A_duration_outside_15_to_240_minutes_is_refused(int minutes)
    {
        var result = Interview.Schedule(Details(minutes: minutes), Today);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("The duration must be between 15 and 240 minutes.", result.Error.Message);
        Assert.Equal("Enter 15 to 240 minutes.", result.Error.FieldErrors!["durationMinutes"][0]);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(240)]
    public void The_duration_bounds_are_accepted(int minutes) =>
        Assert.True(Interview.Schedule(Details(minutes: minutes), Today).IsSuccess);

    // ---- Past dates ----

    [Fact]
    public void A_new_interview_cannot_be_scheduled_in_the_past_but_today_is_fine()
    {
        var past = Interview.Schedule(Details(Today.AddDays(-1)), Today);

        Assert.Equal("A scheduled interview cannot be in the past.", past.Error.Message);
        Assert.Equal("Choose today or a later date.", past.Error.FieldErrors!["date"][0]);
        Assert.True(Interview.Schedule(Details(Today), Today).IsSuccess);
    }

    [Fact]
    public void A_scheduled_interview_may_only_be_moved_to_a_past_slot_if_the_slot_did_not_change()
    {
        var interview = Scheduled(Today);

        Assert.True(interview.Update(Details(Today, minutes: 30), Today.AddDays(1)).IsSuccess);
        var moved = interview.Update(Details(Today.AddDays(-3)), Today);

        Assert.Equal("A scheduled interview cannot be in the past.", moved.Error.Message);
    }

    [Fact]
    public void A_completed_interview_can_be_recorded_for_a_past_date_but_not_a_future_one()
    {
        var past = Interview.RecordCompleted(Details(Today.AddDays(-5)), Hire(), Today);
        var future = Interview.RecordCompleted(Details(Today.AddDays(1)), Hire(), Today);

        Assert.Equal(InterviewStatus.Completed, past.Value.Status);
        Assert.Equal("A future interview cannot be marked completed.", future.Error.Message);
        Assert.Equal("The interview date is in the future.", future.Error.FieldErrors!["status"][0]);
    }

    // ---- Outcome ----

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void A_score_outside_1_to_5_is_refused(int score)
    {
        var interview = Scheduled(Today);

        var result = interview.Complete(Hire(score), Today);

        Assert.Equal("A completed interview needs a score from 1 to 5.", result.Error.Message);
        Assert.Equal("Enter a score from 1 to 5.", result.Error.FieldErrors!["score"][0]);
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void The_score_bounds_are_accepted(int score) =>
        Assert.True(Scheduled(Today).Complete(Hire(score), Today).IsSuccess);

    [Fact]
    public void Completing_needs_a_score_and_a_recommendation()
    {
        var noScore = Scheduled(Today).Complete(new InterviewOutcome(null, InterviewRecommendation.Hire, null), Today);
        var noRecommendation = Scheduled(Today).Complete(new InterviewOutcome(3, null, null), Today);

        Assert.Equal("A completed interview needs a score from 1 to 5.", noScore.Error.Message);
        Assert.Equal("A completed interview needs a recommendation.", noRecommendation.Error.Message);
        Assert.Equal("Choose a recommendation.", noRecommendation.Error.FieldErrors!["recommendation"][0]);
    }

    [Fact]
    public void A_future_interview_cannot_be_completed()
    {
        var interview = Scheduled(Today.AddDays(2));

        var result = interview.Complete(Hire(), Today);

        Assert.Equal("A future interview cannot be marked completed.", result.Error.Message);
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    [Fact]
    public void Completing_keeps_the_trimmed_feedback_and_a_completed_interview_can_be_corrected()
    {
        var interview = Scheduled(Today);

        Assert.True(interview.Complete(Hire(3), Today).IsSuccess);
        Assert.Equal("Strong on design.", interview.Feedback);
        Assert.True(interview.Complete(new InterviewOutcome(5, InterviewRecommendation.StrongHire, null), Today).IsSuccess);
        Assert.Equal((5, InterviewRecommendation.StrongHire), (interview.Score, interview.Recommendation));
        Assert.Null(interview.Feedback);
    }

    [Theory]
    [InlineData(InterviewRecommendation.Hire, true)]
    [InlineData(InterviewRecommendation.StrongHire, true)]
    [InlineData(InterviewRecommendation.NoHire, false)]
    [InlineData(null, false)]
    public void Only_hire_and_strong_hire_are_positive(InterviewRecommendation? recommendation, bool positive) =>
        Assert.Equal(positive, InterviewRecommendationText.IsPositive(recommendation));

    // ---- Cancelled and completed interviews ----

    [Fact]
    public void A_cancelled_interview_is_locked()
    {
        var interview = Scheduled();
        Assert.True(interview.Cancel("Candidate withdrew").IsSuccess);

        var update = interview.Update(Details(minutes: 30), Today);
        var complete = interview.Complete(Hire(), Today.AddDays(5));
        var again = interview.Cancel();

        Assert.Equal(ErrorType.Conflict, update.Error.Type);
        Assert.Equal("A cancelled interview cannot be changed. Schedule a new one instead.", update.Error.Message);
        Assert.Equal(update.Error.Message, complete.Error.Message);
        Assert.Equal(update.Error.Message, again.Error.Message);
        Assert.Equal("Candidate withdrew", interview.Feedback);
    }

    [Fact]
    public void A_completed_interview_cannot_be_cancelled_or_deleted()
    {
        var interview = Completed();

        var cancel = interview.Cancel();
        var delete = interview.EnsureCanDelete();

        Assert.Equal("A completed interview cannot be cancelled.", cancel.Error.Message);
        Assert.Equal(ErrorType.Conflict, delete.Error.Type);
        Assert.Equal("A completed interview is part of the hiring record and cannot be deleted.", delete.Error.Message);
    }

    [Fact]
    public void Scheduled_and_cancelled_interviews_can_be_deleted()
    {
        var cancelled = Scheduled();
        cancelled.Cancel();

        Assert.True(Scheduled().EnsureCanDelete().IsSuccess);
        Assert.True(cancelled.EnsureCanDelete().IsSuccess);
    }

    [Fact]
    public void The_application_of_an_interview_cannot_change()
    {
        var interview = Scheduled();

        var result = interview.Update(Details(application: 2), Today);

        Assert.Equal("The application of an interview cannot be changed.", result.Error.Message);
        Assert.Equal(1, interview.ApplicationId);
    }

    // ---- Double booking arithmetic ----

    [Theory]
    [InlineData(600, 60, 630, 60, true)]
    [InlineData(600, 60, 660, 30, false)]
    [InlineData(660, 30, 600, 60, false)]
    [InlineData(600, 60, 540, 61, true)]
    [InlineData(600, 60, 540, 60, false)]
    [InlineData(600, 15, 600, 15, true)]
    public void Two_interviews_overlap_when_one_starts_before_the_other_ends(int startA, int durationA, int startB, int durationB, bool overlap) =>
        Assert.Equal(overlap, Interview.Overlap(startA, durationA, startB, durationB));

    [Fact]
    public void The_start_and_end_minutes_follow_the_start_time_and_duration()
    {
        var interview = Interview.Schedule(Details(start: 9, minutes: 90), Today).Value;

        Assert.Equal(540, interview.StartMinute);
        Assert.Equal(630, interview.EndMinute);
    }
}
