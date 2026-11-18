import { EvaluationPeriodStatus, ScorecardStatus } from '../models/kpi.models';

/**
 * What an evaluation period allows, mirrored from the API's `EvaluationPeriod` so the demo mock and
 * the screens follow the same rules. NOT authoritative: with the API on, the server decides and
 * answers 409 with its own message whatever the screen offers.
 */

/**
 * Whether the employee may work on a scorecard (enter results, submit, add or remove evidence):
 * while the period is Active, and for a scorecard a reviewer Returned also while it is Completed (a
 * reviewer may return one then, so the employee must be able to correct and resubmit it). A Draft
 * scorecard's window ended with the Active period; a Draft or Closed period refuses everything.
 * (`EvaluationPeriod.AcceptsEmployeeWork`.)
 */
export function periodAcceptsEmployeeWork(
  periodStatus: EvaluationPeriodStatus,
  scorecardStatus: ScorecardStatus,
): boolean {
  return (
    periodStatus === 'Active' || (periodStatus === 'Completed' && scorecardStatus === 'Returned')
  );
}

/**
 * The employee dashboard's `isPending`: the scorecard waits for the employee (Draft or Returned)
 * and the period still accepts their work ({@link periodAcceptsEmployeeWork}).
 */
export function isSubmissionPending(
  periodStatus: EvaluationPeriodStatus,
  scorecardStatus: ScorecardStatus,
): boolean {
  return (
    (scorecardStatus === 'Draft' || scorecardStatus === 'Returned') &&
    periodAcceptsEmployeeWork(periodStatus, scorecardStatus)
  );
}

/**
 * The API's 409 text when the period refuses the employee's work, or null when it accepts it
 * (`EvaluationPeriod.EnsureAcceptsResults` and `EnsureAcceptsEvidence`). `results` covers saving
 * results and submitting.
 */
export function employeeWorkRefusal(
  period: { name: string; status: EvaluationPeriodStatus },
  scorecardStatus: ScorecardStatus,
  work: 'results' | 'evidence',
): string | null {
  if (periodAcceptsEmployeeWork(period.status, scorecardStatus)) return null;
  const prefix = `Period "${period.name}" is ${period.status};`;
  if (scorecardStatus === 'Returned') {
    return work === 'results'
      ? `${prefix} a returned scorecard can only be changed and resubmitted while the period is Active or Completed.`
      : `${prefix} evidence of a returned scorecard can only be added or removed while the period is Active or Completed.`;
  }
  return work === 'results'
    ? `${prefix} results can only be entered and submitted while the period is Active.`
    : `${prefix} evidence can only be added or removed while the period is Active.`;
}

/** Review steps (start, notes, approve, return, reject, HR final review) need an Active or Completed period. */
export function periodAllowsReview(status: EvaluationPeriodStatus): boolean {
  return status === 'Active' || status === 'Completed';
}

/**
 * The scorecard statuses that keep a period from closing: waiting for a reviewer or HR (Submitted,
 * Resubmitted, Under Review, Approved) or for the employee (Returned, who may resubmit while the
 * period is Completed). Draft and Planning do not block; Finalized and Rejected are final.
 * (`EvaluationPeriod.StatusesBlockingClose`.)
 */
export const KPI_STATUSES_BLOCKING_CLOSE: readonly ScorecardStatus[] = [
  'Submitted',
  'Resubmitted',
  'Under Review',
  'Approved',
  'Returned',
];

/** The API's 409 text when a Completed period still has scorecards that block closing, else null. */
export function periodCloseRefusal(periodName: string, blockingCount: number): string | null {
  return blockingCount > 0
    ? `Period "${periodName}" still has ${blockingCount} scorecard(s) in review or returned to the employee. Finish them before closing.`
    : null;
}
