using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The one rule of what a viewer may read of the review of a scorecard, used by every answer that carries review fields (the
/// scorecard detail of every read and write route, a planned item, the employee dashboard's feedback). It only ever narrows what the
/// caller's scope already lets them read.
/// <list type="bullet">
/// <item>Anyone who is not the scorecard's own employee (the reviewer, a manager, HR, the built-in administrator) reads everything.</item>
/// <item>The scorecard's own employee (their linked employee id is the scorecard's) reads the manager's overall and per-KPI rating and
/// comment only once the decision is final: <see cref="ScorecardStatus.Finalized"/> or <see cref="ScorecardStatus.Rejected"/>. While it is
/// Approved, HR can still return it, so it is hidden there too.</item>
/// <item>The HR comment only at <see cref="ScorecardStatus.Finalized"/>.</item>
/// <item>History: the comments of Return and Reject always (the employee must see the reason); every other comment (the manager's Approve
/// and the HR Finalize) only once the scorecard is Finalized or Rejected. The action, the statuses, the actor and the time always.</item>
/// </list>
/// A hidden field is <c>null</c> in the answer, exactly as a field that was never filled.
/// </summary>
internal sealed record ReviewVisibility(bool ManagerReview, bool HrComment, bool DecisionComments)
{
    /// <summary>What a viewer who is not the scorecard's employee reads: everything their scope allows.</summary>
    public static ReviewVisibility Everything { get; } = new(true, true, true);

    /// <summary>The visibility for a viewer (their linked employee, or null) of a scorecard of <paramref name="scorecardEmployeeId"/> in <paramref name="status"/>.</summary>
    public static ReviewVisibility For(ScorecardStatus status, int scorecardEmployeeId, int? viewerEmployeeId) =>
        viewerEmployeeId is { } viewer && viewer == scorecardEmployeeId ? ForOwner(status) : Everything;

    /// <summary>The visibility for the scorecard's own employee.</summary>
    public static ReviewVisibility ForOwner(ScorecardStatus status)
    {
        var decided = status is ScorecardStatus.Finalized or ScorecardStatus.Rejected;
        return new ReviewVisibility(decided, status == ScorecardStatus.Finalized, decided);
    }

    /// <summary>True when the comment of a history entry of this action may be shown.</summary>
    public bool ShowsComment(ReviewActionType action) =>
        DecisionComments || action is ReviewActionType.Return or ReviewActionType.Reject;

    public decimal? Rating(decimal? managerRating) => ManagerReview ? managerRating : null;

    public string? ManagerText(string? managerComment) => ManagerReview ? managerComment : null;

    public string? HrText(string? hrComment) => HrComment ? hrComment : null;
}
