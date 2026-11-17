using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The caller's own scorecards (<c>GET /api/kpi/my-scorecards</c>), newest period first, without the ones still in Planning (HR is
/// still assigning those). A caller with no linked employee (the built-in administrator) has none: an empty page.
/// </summary>
internal sealed class MyScorecardsHandler(ScorecardReader reader, ICurrentUser user) : IMyScorecardsHandler
{
    public Task<Result<PagedResult<ScorecardDto>>> HandleAsync(PageRequest<ScorecardFilter> request, CancellationToken cancellationToken)
    {
        if (user.EmployeeId is not { } own)
        {
            return Task.FromResult(Result.Success(new PagedResult<ScorecardDto>([], 0, 0, Paging.ClampPageSize(request.PageSize))));
        }

        var mine = reader.Scoped().Where(scorecard => scorecard.EmployeeId == own && scorecard.Status != ScorecardStatus.Planning);
        return reader.PageAsync(mine, request, row => row.PeriodStartDate, fallbackDescending: true, cancellationToken);
    }
}

/// <summary>
/// The scorecards waiting for the caller (<c>GET /api/kpi/reviews/pending</c>), oldest submission first. A manager sees the scorecards in
/// the manager stage (Submitted, Resubmitted, Under Review) that name them as the reviewer. The HR tier sees every scorecard in the
/// manager stage and every Approved one, except those whose manager-stage approval they recorded themselves (they cannot give the HR
/// review). Never the caller's own scorecard, and only scorecards whose period is Active or Completed (the periods in which a review step
/// is allowed).
/// </summary>
internal sealed class PendingReviewsHandler(ScorecardReader reader, IReadModel read, ICurrentUser user, SelfService self) : IPendingReviewsHandler
{
    public Task<Result<PagedResult<ScorecardDto>>> HandleAsync(PageRequest<ScorecardFilter> request, CancellationToken cancellationToken)
    {
        // Only scorecards whose period still allows a review (Active or Completed): one in a Draft or Closed period could not be acted on.
        var active = EvaluationPeriodStatus.Active;
        var completed = EvaluationPeriodStatus.Completed;
        var reviewable = read.Query<EvaluationPeriod>().Where(period => period.Status == active || period.Status == completed).Select(period => period.Id);
        var waiting = reader.Scoped().Where(scorecard => reviewable.Contains(scorecard.PeriodId));
        if (user.EmployeeId is { } own)
        {
            waiting = waiting.Where(scorecard => scorecard.EmployeeId != own);
        }

        if (self.TierOf("kpi") == AccessTier.All)
        {
            var userId = user.UserId;
            var approvals = read.Query<ReviewAction>().Where(action => action.Step == ReviewStep.Manager && action.Action == ReviewActionType.Approve);
            waiting = waiting.Where(scorecard =>
                scorecard.Status == ScorecardStatus.Submitted
                || scorecard.Status == ScorecardStatus.Resubmitted
                || scorecard.Status == ScorecardStatus.UnderReview
                || (scorecard.Status == ScorecardStatus.Approved
                    && approvals.Where(action => action.ScorecardId == scorecard.Id).OrderByDescending(action => action.Id).Select(action => action.ActorUserId).FirstOrDefault() != userId));
        }
        else if (user.EmployeeId is { } reviewer)
        {
            waiting = waiting.Where(scorecard =>
                scorecard.ReviewerEmployeeId == reviewer
                && (scorecard.Status == ScorecardStatus.Submitted
                    || scorecard.Status == ScorecardStatus.Resubmitted
                    || scorecard.Status == ScorecardStatus.UnderReview));
        }
        else
        {
            waiting = waiting.Where(Predicates.Never<Scorecard>());
        }

        return reader.PageAsync(waiting, request, row => row.SubmittedOn ?? DateOnly.MinValue, fallbackDescending: false, cancellationToken);
    }
}
