import {
  EVALUATION_PERIOD_STATUSES,
  SCORECARD_STATUSES,
  ScorecardStatus,
} from '../models/kpi.models';
import {
  KPI_STATUSES_BLOCKING_CLOSE,
  employeeWorkRefusal,
  isSubmissionPending,
  periodAcceptsEmployeeWork,
  periodAllowsReview,
  periodCloseRefusal,
} from './kpi-period-rules';

describe('KPI period rules (mirror of the API EvaluationPeriod)', () => {
  it('accepts employee work while Active, and for a Returned scorecard also while Completed', () => {
    for (const period of EVALUATION_PERIOD_STATUSES) {
      for (const card of SCORECARD_STATUSES) {
        const expected = period === 'Active' || (period === 'Completed' && card === 'Returned');
        expect(periodAcceptsEmployeeWork(period, card), `${period} / ${card}`).toBe(expected);
      }
    }
  });

  it('flags a pending submission only for Draft or Returned in a period that accepts the work', () => {
    expect(isSubmissionPending('Active', 'Draft')).toBe(true);
    expect(isSubmissionPending('Active', 'Returned')).toBe(true);
    expect(isSubmissionPending('Completed', 'Returned')).toBe(true);
    expect(isSubmissionPending('Completed', 'Draft')).toBe(false);
    expect(isSubmissionPending('Closed', 'Returned')).toBe(false);
    expect(isSubmissionPending('Active', 'Submitted')).toBe(false);
    expect(isSubmissionPending('Active', 'Planning')).toBe(false);
  });

  it('refuses with the API texts: a Draft scorecard needs Active, a Returned one Active or Completed', () => {
    const period = (status: 'Draft' | 'Active' | 'Completed' | 'Closed') => ({
      name: '2026 Q4',
      status,
    });
    expect(employeeWorkRefusal(period('Active'), 'Draft', 'results')).toBeNull();
    expect(employeeWorkRefusal(period('Completed'), 'Returned', 'results')).toBeNull();
    expect(employeeWorkRefusal(period('Completed'), 'Returned', 'evidence')).toBeNull();

    expect(employeeWorkRefusal(period('Completed'), 'Draft', 'results')).toBe(
      'Period "2026 Q4" is Completed; results can only be entered and submitted while the period is Active.',
    );
    expect(employeeWorkRefusal(period('Completed'), 'Draft', 'evidence')).toBe(
      'Period "2026 Q4" is Completed; evidence can only be added or removed while the period is Active.',
    );
    expect(employeeWorkRefusal(period('Closed'), 'Returned', 'results')).toBe(
      'Period "2026 Q4" is Closed; a returned scorecard can only be changed and resubmitted while the period is Active or Completed.',
    );
    expect(employeeWorkRefusal(period('Draft'), 'Returned', 'evidence')).toBe(
      'Period "2026 Q4" is Draft; evidence of a returned scorecard can only be added or removed while the period is Active or Completed.',
    );
  });

  it('allows review steps only while the period is Active or Completed', () => {
    expect(EVALUATION_PERIOD_STATUSES.filter(periodAllowsReview)).toEqual(['Active', 'Completed']);
  });

  it('blocks closing for scorecards in review or returned, never for Planning, Draft or final ones', () => {
    const blocking: ScorecardStatus[] = [
      'Submitted',
      'Resubmitted',
      'Under Review',
      'Approved',
      'Returned',
    ];
    expect([...KPI_STATUSES_BLOCKING_CLOSE].sort()).toEqual([...blocking].sort());
    for (const free of ['Planning', 'Draft', 'Finalized', 'Rejected'] as const) {
      expect(KPI_STATUSES_BLOCKING_CLOSE).not.toContain(free);
    }
    expect(periodCloseRefusal('2026 Q4', 0)).toBeNull();
    expect(periodCloseRefusal('2026 Q4', 3)).toBe(
      'Period "2026 Q4" still has 3 scorecard(s) in review or returned to the employee. Finish them before closing.',
    );
  });
});
