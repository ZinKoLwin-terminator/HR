import { Entity } from '../../core/models/common.models';
import { ReviewAction, ScorecardItem, ScorecardStatus } from '../../core/models/kpi.models';

/**
 * What the mock scorecard store keeps for one scorecard: the plan, the copied KPI items (with their
 * evidence metadata) and the append-only review history. Names, department and period fields are
 * joined from the other mock stores when a scorecard is read, so they follow the live records.
 */
export interface ScorecardRecord extends Entity {
  employeeId: number;
  periodId: number;
  /** Copied from the employee when the scorecard was created; kept if the employee moves later. */
  companyId: number;
  branchId: number;
  departmentId: number;
  positionId: number;
  status: ScorecardStatus;
  reviewerEmployeeId: number | null;
  planFinalizedOn: string | null;
  submittedOn: string | null;
  reviewedOn: string | null;
  finalizedOn: string | null;
  employeeComment: string | null;
  managerRating: number | null;
  managerComment: string | null;
  hrComment: string | null;
  overallScore: number | null;
  ratingLabel: string | null;
  /** User id that recorded the manager-stage approval (the HR stage may not be the same user). */
  managerApprovedByUserId: number | null;
  items: ScorecardItem[];
  history: ReviewAction[];
  rowVersion: string;
}

/** The next concurrency token of the mock: a counter in text form. */
export function nextRowVersion(current: string | undefined): string {
  return String((Number(current) || 0) + 1);
}
