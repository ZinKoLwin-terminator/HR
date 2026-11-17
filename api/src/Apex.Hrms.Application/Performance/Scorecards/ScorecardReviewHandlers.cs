using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The reviewer starts the review (<c>POST /api/kpi/scorecards/{id}/start-review</c>): Submitted or Resubmitted to Under Review. The
/// reviewer is the employee's manager when the plan was finalized (fixed from then on), or anyone of the HR tier; nobody reviews
/// their own scorecard. The period must be Active or Completed.
/// </summary>
internal sealed class StartScorecardReviewHandler(ScorecardSteps steps) : ICommandHandler<StartScorecardReviewCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(StartScorecardReviewCommand command, CancellationToken cancellationToken)
    {
        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.Reviewer, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var entry = scorecard.StartReview(steps.Actor, steps.UtcNow);
        return entry.IsFailure
            ? Result.Failure<ScorecardDetailDto>(entry.Error)
            : await steps.SaveAsync(scorecard, period, entry.Value, ScorecardActivity.ReviewStarted, cancellationToken);
    }
}

/// <summary>
/// The reviewer saves the review draft (<c>PUT /api/kpi/scorecards/{id}/review</c>): an overall rating and comment and a rating and
/// comment per KPI, while the scorecard is Submitted, Resubmitted or Under Review. No status change and no history entry.
/// </summary>
internal sealed class SaveScorecardReviewHandler(
    IValidator<SaveScorecardReviewRequest> validator,
    ScorecardSteps steps) : IUpdateHandler<SaveScorecardReviewRequest, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(int id, SaveScorecardReviewRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(id, request.RowVersion, StepRole.Reviewer, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var reviewable = scorecard.EnsureCanSaveReview();
        if (reviewable.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(reviewable.Error);
        }

        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var ordered = await steps.ItemsOfAsync(id, cancellationToken);
        var requested = request.Items!;
        var matched = ScorecardSteps.Match([.. requested.Select(item => item.Id)], ordered, new Violations());
        if (matched.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(matched.Error);
        }

        var saved = scorecard.SaveReview(request.ManagerRating, request.ManagerComment);
        if (saved.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(saved.Error);
        }

        for (var index = 0; index < requested.Count; index++)
        {
            matched.Value[index].RecordReview(requested[index].ManagerRating, requested[index].ManagerComment);
        }

        return await steps.SaveAsync(scorecard, period, entry: null, ScorecardActivity.ReviewSaved, cancellationToken);
    }
}

/// <summary>
/// The reviewer approves (<c>POST /api/kpi/scorecards/{id}/approve</c>). The score is calculated again from the stored results (the
/// authoritative figure: nothing the client sends is trusted) and replaces the one stored at submission. In a period that requires
/// an HR review the scorecard becomes Approved and waits for HR; otherwise the approval finalizes it.
/// </summary>
internal sealed class ApproveScorecardHandler(
    IValidator<ApproveScorecardCommand> validator,
    ScorecardSteps steps) : ICommandHandler<ApproveScorecardCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(ApproveScorecardCommand command, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.Reviewer, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var approvable = scorecard.EnsureCanApprove();
        if (approvable.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(approvable.Error);
        }

        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var ordered = await steps.ItemsOfAsync(command.Id, cancellationToken);
        var scored = await steps.ScoreAsync(ordered, cancellationToken);
        if (scored.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(scored.Error);
        }

        var entry = scorecard.Approve(
            period.RequiresHrReview,
            steps.Today,
            scored.Value.Score,
            scored.Value.Label,
            command.Comment,
            steps.Actor,
            steps.UtcNow);
        if (entry.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(entry.Error);
        }

        ScorecardSteps.ApplyScores(ordered, scored.Value.Score);
        var finalized = scorecard.Status == ScorecardStatus.Finalized;
        return await steps.SaveAsync(
            scorecard,
            period,
            entry.Value,
            (code, periodName) => ScorecardActivity.Approved(code, periodName, finalized),
            cancellationToken);
    }
}

/// <summary>
/// Returns the scorecard to the employee (<c>POST /api/kpi/scorecards/{id}/return</c>) with a required reason. From the manager stage
/// the reviewer or HR may do it; from Approved only HR, and not the person who approved it. The employee edits the results and
/// submits again (Resubmitted).
/// </summary>
internal sealed class ReturnScorecardHandler(
    IValidator<ReturnScorecardCommand> validator,
    ScorecardSteps steps) : ICommandHandler<ReturnScorecardCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(ReturnScorecardCommand command, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.ReviewerOrHrByStatus, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var entry = scorecard.Return(steps.Actor, command.Comment, steps.UtcNow);
        return entry.IsFailure
            ? Result.Failure<ScorecardDetailDto>(entry.Error)
            : await steps.SaveAsync(scorecard, period, entry.Value, ScorecardActivity.Returned, cancellationToken);
    }
}

/// <summary>Rejects the scorecard (<c>POST /api/kpi/scorecards/{id}/reject</c>) with a required reason: final, nothing can be done to it afterwards. Same who-may-act rules as return.</summary>
internal sealed class RejectScorecardHandler(
    IValidator<RejectScorecardCommand> validator,
    ScorecardSteps steps) : ICommandHandler<RejectScorecardCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(RejectScorecardCommand command, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.ReviewerOrHrByStatus, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var entry = scorecard.Reject(steps.Actor, command.Comment, steps.UtcNow);
        return entry.IsFailure
            ? Result.Failure<ScorecardDetailDto>(entry.Error)
            : await steps.SaveAsync(scorecard, period, entry.Value, ScorecardActivity.Rejected, cancellationToken);
    }
}

/// <summary>
/// HR's final review (<c>POST /api/kpi/scorecards/{id}/finalize</c>): Approved to Finalized, HR tier only and never the person who
/// recorded the manager's approval (separation of duties). The optional comment is stored as the HR comment.
/// </summary>
internal sealed class FinalizeScorecardHandler(
    IValidator<FinalizeScorecardCommand> validator,
    ScorecardSteps steps) : ICommandHandler<FinalizeScorecardCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(FinalizeScorecardCommand command, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(command.Id, command.RowVersion, StepRole.Hr, null, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(opened.Error);
        }

        var scorecard = opened.Value;
        var period = await steps.PeriodOfAsync(scorecard, cancellationToken);
        var open = period.EnsureAllowsReview();
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(open.Error);
        }

        var entry = scorecard.FinalizeByHr(steps.Today, command.Comment, steps.Actor, steps.UtcNow);
        return entry.IsFailure
            ? Result.Failure<ScorecardDetailDto>(entry.Error)
            : await steps.SaveAsync(scorecard, period, entry.Value, ScorecardActivity.Finalized, cancellationToken);
    }
}
