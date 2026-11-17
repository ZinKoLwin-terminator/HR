using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>
/// The life of a scorecard after the plan is final. Every status change is a method here that returns the
/// <see cref="ReviewAction"/> to append to the history (like <see cref="FinalizePlan"/>); nothing else moves a status.
/// <code>
/// Draft --submit--> Submitted --start review--> Under Review --approve--> Approved --finalize (HR)--> Finalized
///                       |                           |                \--> Finalized (period without HR review)
///                       +--- return / reject -------+--- from Approved: return / reject (HR step)
/// Returned --submit--> Resubmitted (then reviewed like Submitted).  Rejected and Finalized are final.
/// </code>
/// Who may do a step, the period state and the row version are rules of the application layer, which needs other records.
/// </summary>
public sealed partial class Scorecard
{
    public const int CommentMaxLength = 1000;

    public const decimal MinRating = 1m;

    public const decimal MaxRating = 5m;

    /// <summary>Submitted, Resubmitted or Under Review: waiting for the reviewer (the manager stage).</summary>
    public bool IsInManagerStage => Status is ScorecardStatus.Submitted or ScorecardStatus.Resubmitted or ScorecardStatus.UnderReview;

    /// <summary>Results can only be entered while the scorecard is Draft or Returned (409 otherwise).</summary>
    public Result EnsureResultsEditable() => Status is ScorecardStatus.Draft or ScorecardStatus.Returned
        ? Result.Success()
        : Refuse("results can only be entered while it is Draft or Returned");

    /// <summary>Evidence files can only be added or removed while the scorecard is Draft or Returned (409 otherwise).</summary>
    public Result EnsureEvidenceEditable() => Status is ScorecardStatus.Draft or ScorecardStatus.Returned
        ? Result.Success()
        : Refuse("evidence can only be added or removed while it is Draft or Returned");

    /// <summary>Saves the employee's overall comment while the scorecard is Draft or Returned. The KPI results are stored on the items.</summary>
    public Result SaveResults(string? employeeComment)
    {
        var editable = EnsureResultsEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        var violations = CommentViolations(employeeComment, "employeeComment");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        EmployeeComment = TextNormalization.CleanOptional(employeeComment);
        return Result.Success();
    }

    /// <summary>Only a Draft or Returned scorecard can be submitted (409 otherwise).</summary>
    public Result EnsureCanSubmit() => Status is ScorecardStatus.Draft or ScorecardStatus.Returned
        ? Result.Success()
        : Refuse("only a Draft or Returned scorecard can be submitted");

    /// <summary>Only a Submitted, Resubmitted or Under Review scorecard can be approved (409 otherwise).</summary>
    public Result EnsureCanApprove() => IsInManagerStage
        ? Result.Success()
        : Refuse("only a Submitted, Resubmitted or Under Review scorecard can be approved");

    /// <summary>
    /// The employee submits: Draft to Submitted, or Returned to Resubmitted. The score is the one calculated from every result
    /// (<see cref="KpiCalculator.Score"/>) and is stored with the calculation version; <see cref="SubmittedOn"/> becomes today.
    /// </summary>
    public Result<ReviewAction> Submit(DateOnly today, ScorecardScore score, string? ratingLabel, ReviewActor actor, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(actor);

        var submittable = EnsureCanSubmit();
        if (submittable.IsFailure)
        {
            return Result.Failure<ReviewAction>(submittable.Error);
        }

        var again = Status == ScorecardStatus.Returned;
        var target = again ? ScorecardStatus.Resubmitted : ScorecardStatus.Submitted;
        var entry = Entry(
            ReviewStep.Employee,
            again ? ReviewActionType.Resubmit : ReviewActionType.Submit,
            target,
            actor,
            comment: null,
            score.OverallScore,
            occurredAtUtc);
        if (entry.IsFailure)
        {
            return entry;
        }

        Status = target;
        ApplyScore(score, ratingLabel);
        SubmittedOn = today;
        return entry;
    }

    /// <summary>The reviewer starts the review: Submitted or Resubmitted to Under Review.</summary>
    public Result<ReviewAction> StartReview(ReviewActor actor, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (Status is not (ScorecardStatus.Submitted or ScorecardStatus.Resubmitted))
        {
            return Refuse<ReviewAction>("only a Submitted or Resubmitted scorecard can be put under review");
        }

        var entry = Entry(ReviewStep.Manager, ReviewActionType.StartReview, ScorecardStatus.UnderReview, actor, null, OverallScore, occurredAtUtc);
        if (entry.IsSuccess)
        {
            Status = ScorecardStatus.UnderReview;
        }

        return entry;
    }

    /// <summary>A review draft can only be saved while the scorecard is Submitted, Resubmitted or Under Review (409 otherwise).</summary>
    public Result EnsureCanSaveReview() => IsInManagerStage
        ? Result.Success()
        : Refuse("a review can only be saved while it is Submitted, Resubmitted or Under Review");

    /// <summary>The review draft (overall rating and comment) can be saved while the scorecard is in the manager stage. No status change.</summary>
    public Result SaveReview(decimal? managerRating, string? managerComment)
    {
        var reviewable = EnsureCanSaveReview();
        if (reviewable.IsFailure)
        {
            return reviewable;
        }

        var checkedReview = ScorecardItem.CheckReview(managerRating, managerComment);
        if (checkedReview.IsFailure)
        {
            return checkedReview;
        }

        ManagerRating = managerRating;
        ManagerComment = TextNormalization.CleanOptional(managerComment);
        return Result.Success();
    }

    /// <summary>
    /// The manager approves from Submitted, Resubmitted or Under Review. The score is recalculated by the caller from the stored
    /// results and replaces the earlier one. With <paramref name="requiresHrReview"/> the scorecard becomes Approved and waits
    /// for HR; without it the approval finalizes it (<see cref="FinalizedOn"/>, <see cref="FinalizedByUserId"/>).
    /// </summary>
    public Result<ReviewAction> Approve(
        bool requiresHrReview,
        DateOnly today,
        ScorecardScore score,
        string? ratingLabel,
        string? comment,
        ReviewActor actor,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(actor);

        var approvable = EnsureCanApprove();
        if (approvable.IsFailure)
        {
            return Result.Failure<ReviewAction>(approvable.Error);
        }

        var target = requiresHrReview ? ScorecardStatus.Approved : ScorecardStatus.Finalized;
        var entry = Entry(ReviewStep.Manager, ReviewActionType.Approve, target, actor, comment, score.OverallScore, occurredAtUtc);
        if (entry.IsFailure)
        {
            return entry;
        }

        Status = target;
        ApplyScore(score, ratingLabel);
        ReviewedOn = today;
        if (target == ScorecardStatus.Finalized)
        {
            FinalizedOn = today;
            FinalizedByUserId = actor.UserId;
        }

        return entry;
    }

    /// <summary>
    /// Sends the scorecard back to the employee (Returned) with a reason, from the manager stage (step Manager) or from Approved
    /// (the HR step). The employee edits the results and submits again. The score stored so far is cleared, because the results
    /// will change; the history entry keeps it. The review draft is cleared too (the overall and per-KPI manager rating and comment,
    /// and the HR comment): it was never a decision, the employee must not read it, and the reviewer starts again on the resubmitted
    /// results. The reason the employee needs is the comment of the history entry. <paramref name="items"/> are every item of this
    /// scorecard (an item of another scorecard is a programming error).
    /// </summary>
    public Result<ReviewAction> Return(ReviewActor actor, string? reason, IReadOnlyCollection<ScorecardItem> items, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Any(item => item.ScorecardId != Id))
        {
            throw new ArgumentException("Every item must belong to the scorecard being returned.", nameof(items));
        }

        var entry = Turn(ReviewActionType.Return, ScorecardStatus.Returned, "returned", actor, reason, occurredAtUtc);
        if (entry.IsSuccess)
        {
            OverallScore = null;
            RatingLabel = null;
            CalculationVersion = null;
            ReviewedOn = null;
            ManagerRating = null;
            ManagerComment = null;
            HrComment = null;
            foreach (var item in items)
            {
                item.ClearReview();
            }
        }

        return entry;
    }

    /// <summary>Rejects the scorecard (Rejected, final) with a reason, from the manager stage or from Approved.</summary>
    public Result<ReviewAction> Reject(ReviewActor actor, string? reason, DateTime occurredAtUtc) =>
        Turn(ReviewActionType.Reject, ScorecardStatus.Rejected, "rejected", actor, reason, occurredAtUtc);

    /// <summary>HR's final review: Approved to Finalized. The optional comment is kept as <see cref="HrComment"/>.</summary>
    public Result<ReviewAction> FinalizeByHr(DateOnly today, string? comment, ReviewActor actor, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (Status != ScorecardStatus.Approved)
        {
            return Refuse<ReviewAction>("only an Approved scorecard can be finalized by HR");
        }

        var entry = Entry(ReviewStep.HR, ReviewActionType.Finalize, ScorecardStatus.Finalized, actor, comment, OverallScore, occurredAtUtc);
        if (entry.IsFailure)
        {
            return entry;
        }

        Status = ScorecardStatus.Finalized;
        FinalizedOn = today;
        FinalizedByUserId = actor.UserId;
        HrComment = TextNormalization.CleanOptional(comment);
        return entry;
    }

    private Result<ReviewAction> Turn(ReviewActionType action, ScorecardStatus target, string verb, ReviewActor actor, string? reason, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!IsInManagerStage && Status != ScorecardStatus.Approved)
        {
            return Refuse<ReviewAction>($"only a Submitted, Resubmitted, Under Review or Approved scorecard can be {verb}");
        }

        var step = Status == ScorecardStatus.Approved ? ReviewStep.HR : ReviewStep.Manager;
        var entry = Entry(step, action, target, actor, reason, OverallScore, occurredAtUtc);
        if (entry.IsSuccess)
        {
            Status = target;
        }

        return entry;
    }

    /// <summary>The history entry of a move from the current status. The reason and the length are checked by <see cref="ReviewAction.Create"/>.</summary>
    private Result<ReviewAction> Entry(
        ReviewStep step,
        ReviewActionType action,
        ScorecardStatus target,
        ReviewActor actor,
        string? comment,
        decimal? scoreAtTheTime,
        DateTime occurredAtUtc) =>
        ReviewAction.Create(Id, step, action, Status, target, actor.UserId, actor.EmployeeId, actor.Name, comment, scoreAtTheTime, occurredAtUtc);

    private void ApplyScore(ScorecardScore score, string? ratingLabel)
    {
        OverallScore = score.OverallScore;
        RatingLabel = ratingLabel;
        CalculationVersion = KpiCalculator.CalculationVersion;
    }

    private static Violations CommentViolations(string? comment, string field) => new Violations().AddIf(
        TextNormalization.CleanOptional(comment) is { Length: > CommentMaxLength },
        field,
        $"The comment must be at most {CommentMaxLength} characters.");

    private Result Refuse(string what) => Result.Failure(AppError.Conflict(
        $"The scorecard is {ScorecardStatusText.ToText(Status)}; {what}."));

    private Result<T> Refuse<T>(string what) => Result.Failure<T>(AppError.Conflict(
        $"The scorecard is {ScorecardStatusText.ToText(Status)}; {what}."));
}
