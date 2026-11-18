import { ReviewActionName, ScorecardStatus } from '../models/kpi.models';

/**
 * What a viewer may read of the review of a scorecard, mirrored from the API's `ReviewVisibility`
 * so the demo mock answers the same way. NOT access control: with the API on, the server leaves
 * the hidden fields out (as null) and the screens only display what they get.
 *
 * - Anyone who is not the scorecard's own employee reads everything.
 * - The scorecard's own employee reads the manager's overall and per-KPI rating and comment only
 *   once the decision is final (Finalized or Rejected; while Approved, HR can still return it).
 * - The HR comment only when Finalized.
 * - History: Return and Reject comments always (the employee needs the reason); every other comment
 *   (the manager's Approve, the HR Finalize) only once Finalized or Rejected.
 *
 * A hidden field is null, exactly like a field that was never filled.
 */
export interface KpiReviewVisibility {
  managerReview: boolean;
  hrComment: boolean;
  decisionComments: boolean;
}

const EVERYTHING: KpiReviewVisibility = {
  managerReview: true,
  hrComment: true,
  decisionComments: true,
};

/** The visibility for the scorecard's own employee (`ReviewVisibility.ForOwner`). */
export function reviewVisibilityForOwner(status: ScorecardStatus): KpiReviewVisibility {
  const decided = status === 'Finalized' || status === 'Rejected';
  return { managerReview: decided, hrComment: status === 'Finalized', decisionComments: decided };
}

/** The visibility for a viewer (their linked employee, or null) (`ReviewVisibility.For`). */
export function reviewVisibility(
  status: ScorecardStatus,
  scorecardEmployeeId: number,
  viewerEmployeeId: number | null,
): KpiReviewVisibility {
  return viewerEmployeeId !== null && viewerEmployeeId === scorecardEmployeeId
    ? reviewVisibilityForOwner(status)
    : EVERYTHING;
}

/** Whether the comment of a history entry of this action may be shown. */
export function showsHistoryComment(
  visibility: KpiReviewVisibility,
  action: ReviewActionName,
): boolean {
  return visibility.decisionComments || action === 'Return' || action === 'Reject';
}
