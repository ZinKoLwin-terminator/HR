using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Recruitment;

/// <summary>The fields that describe when, where and with whom an interview takes place.</summary>
public sealed record InterviewDetails(
    int ApplicationId,
    InterviewRound Round,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    InterviewMode Mode,
    int InterviewerId,
    string? Location)
{
    public InterviewDetails Normalized() => this with { Location = TextNormalization.CleanOptional(Location) };
}

/// <summary>What an interview came to: a score from 1 to 5, a recommendation and the interviewer's notes.</summary>
public sealed record InterviewOutcome(int? Score, InterviewRecommendation? Recommendation, string? Feedback)
{
    public InterviewOutcome Normalized() => this with { Feedback = TextNormalization.CleanOptional(Feedback) };
}

/// <summary>
/// An interview of an application (rec.Interview). It is Scheduled, and then Completed (with a score and a
/// recommendation) or Cancelled; a cancelled interview is locked and a completed one cannot be deleted. A scheduled
/// interview cannot be in the past (a completed one may be recorded for a past date but never for a future one).
/// Rules that need other records (the interviewer is a current employee, no double booking, the application's stage)
/// are checked by the Application layer.
/// </summary>
public sealed class Interview : AuditedEntity
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 240;
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int FeedbackMaxLength = 500;

    public const string CancelledLockedMessage = "A cancelled interview cannot be changed. Schedule a new one instead.";

    private Interview()
    {
    }

    public int ApplicationId { get; private set; }

    public InterviewRound Round { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public int DurationMinutes { get; private set; }

    public InterviewMode Mode { get; private set; }

    public int InterviewerEmployeeId { get; private set; }

    /// <summary>A room or meeting link (sample text only).</summary>
    public string? Location { get; private set; }

    public InterviewStatus Status { get; private set; }

    public int? Score { get; private set; }

    public InterviewRecommendation? Recommendation { get; private set; }

    public string? Feedback { get; private set; }

    /// <summary>The minute of the day the interview starts and the minute it ends, for overlap checks.</summary>
    public int StartMinute => MinuteOf(StartTime);

    public int EndMinute => StartMinute + DurationMinutes;

    public static int MinuteOf(TimeOnly time) => (time.Hour * 60) + time.Minute;

    /// <summary>True when two interviews on the same day share any minute: one starts before the other ends.</summary>
    public static bool Overlap(int startA, int durationA, int startB, int durationB) =>
        startA < startB + durationB && startB < startA + durationA;

    /// <summary>Checks the rules of a scheduled interview that need no other record, without changing anything.</summary>
    public static Result ValidateScheduled(InterviewDetails details, DateOnly today, Interview? existing = null)
    {
        var violations = DurationViolations(details);
        var changedSlot = existing is null || existing.Date != details.Date || existing.StartTime != details.StartTime;
        return violations
            .AddIf(
                changedSlot && details.Date < today,
                "date",
                "Choose today or a later date.",
                "A scheduled interview cannot be in the past.")
            .ToResult();
    }

    /// <summary>Starts a new interview as Scheduled.</summary>
    public static Result<Interview> Schedule(InterviewDetails details, DateOnly today)
    {
        var valid = ValidateScheduled(details, today);
        if (valid.IsFailure)
        {
            return Result.Failure<Interview>(valid.Error);
        }

        var interview = new Interview { Status = InterviewStatus.Scheduled };
        interview.Apply(details.Normalized());
        return Result.Success(interview);
    }

    /// <summary>Records an interview that has already taken place: it is created Completed. Never for a future date.</summary>
    public static Result<Interview> RecordCompleted(InterviewDetails details, InterviewOutcome outcome, DateOnly today)
    {
        var interview = new Interview { Status = InterviewStatus.Scheduled };
        var duration = DurationViolations(details).ToResult();
        if (duration.IsFailure)
        {
            return Result.Failure<Interview>(duration.Error);
        }

        interview.Apply(details.Normalized());
        var completed = interview.Complete(outcome, today);
        return completed.IsFailure ? Result.Failure<Interview>(completed.Error) : Result.Success(interview);
    }

    /// <summary>
    /// Changes when, where and with whom a scheduled interview takes place, or the details of a completed one. The
    /// application never changes. A scheduled interview moved to another day or time cannot move into the past.
    /// </summary>
    public Result Update(InterviewDetails details, DateOnly today)
    {
        var valid = ValidateUpdate(details, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        Apply(details.Normalized());
        return Result.Success();
    }

    /// <summary>The rules of <see cref="Update"/> that need no other record, without changing anything.</summary>
    public Result ValidateUpdate(InterviewDetails details, DateOnly today)
    {
        if (Status == InterviewStatus.Cancelled)
        {
            return Result.Failure(AppError.Conflict(CancelledLockedMessage));
        }

        if (details.ApplicationId != ApplicationId)
        {
            return Result.Failure(AppError.Validation(
                "The application of an interview cannot be changed.",
                "applicationId",
                "Schedule a new interview for another application."));
        }

        return Status == InterviewStatus.Scheduled
            ? ValidateScheduled(details, today, this)
            : ValidateCompleted(details, today);
    }

    /// <summary>
    /// Records the outcome: Scheduled (or an already completed interview, to correct it) becomes Completed. It needs a
    /// score from 1 to 5 and a recommendation, and the interview cannot be in the future.
    /// </summary>
    public Result Complete(InterviewOutcome outcome, DateOnly today)
    {
        var valid = CheckOutcome(outcome, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        var cleaned = outcome.Normalized();
        Status = InterviewStatus.Completed;
        Score = cleaned.Score;
        Recommendation = cleaned.Recommendation;
        Feedback = cleaned.Feedback;
        return Result.Success();
    }

    /// <summary>The rules of <see cref="Complete"/>, without changing anything.</summary>
    public Result CheckOutcome(InterviewOutcome outcome, DateOnly today)
    {
        if (Status == InterviewStatus.Cancelled)
        {
            return Result.Failure(AppError.Conflict(CancelledLockedMessage));
        }

        var cleaned = outcome.Normalized();
        return new Violations()
            .AddIf(
                Date > today,
                "status",
                "The interview date is in the future.",
                "A future interview cannot be marked completed.")
            .AddIf(
                cleaned.Score is not { } score || score < MinScore || score > MaxScore,
                "score",
                "Enter a score from 1 to 5.",
                "A completed interview needs a score from 1 to 5.")
            .AddIf(
                cleaned.Recommendation is null,
                "recommendation",
                "Choose a recommendation.",
                "A completed interview needs a recommendation.")
            .AddIf(
                cleaned.Feedback is { Length: > FeedbackMaxLength },
                "feedback",
                $"Feedback must be at most {FeedbackMaxLength} characters.")
            .ToResult();
    }

    /// <summary>Scheduled to Cancelled. A completed interview is part of the hiring record; a cancelled one is locked.</summary>
    public Result Cancel(string? note = null)
    {
        if (Status == InterviewStatus.Cancelled)
        {
            return Result.Failure(AppError.Conflict(CancelledLockedMessage));
        }

        if (Status == InterviewStatus.Completed)
        {
            return Result.Failure(AppError.Conflict("A completed interview cannot be cancelled."));
        }

        var cleaned = TextNormalization.CleanOptional(note);
        if (cleaned is { Length: > FeedbackMaxLength })
        {
            return Result.Failure(AppError.Validation(
                $"Reason must be at most {FeedbackMaxLength} characters.",
                "reason",
                $"Reason must be at most {FeedbackMaxLength} characters."));
        }

        Status = InterviewStatus.Cancelled;
        if (cleaned is not null)
        {
            Feedback = cleaned;
        }

        return Result.Success();
    }

    /// <summary>A completed interview is part of the hiring record and cannot be deleted.</summary>
    public Result EnsureCanDelete() =>
        Status == InterviewStatus.Completed
            ? Result.Failure(AppError.Conflict("A completed interview is part of the hiring record and cannot be deleted."))
            : Result.Success();

    private static Violations DurationViolations(InterviewDetails details) =>
        new Violations().AddIf(
            details.DurationMinutes is < MinDurationMinutes or > MaxDurationMinutes,
            "durationMinutes",
            "Enter 15 to 240 minutes.",
            "The duration must be between 15 and 240 minutes.");

    private static Result ValidateCompleted(InterviewDetails details, DateOnly today) =>
        DurationViolations(details)
            .AddIf(
                details.Date > today,
                "status",
                "The interview date is in the future.",
                "A future interview cannot be marked completed.")
            .ToResult();

    private void Apply(InterviewDetails normalized)
    {
        ApplicationId = normalized.ApplicationId;
        Round = normalized.Round;
        Date = normalized.Date;
        StartTime = normalized.StartTime;
        DurationMinutes = normalized.DurationMinutes;
        Mode = normalized.Mode;
        InterviewerEmployeeId = normalized.InterviewerId;
        Location = normalized.Location;
    }
}
