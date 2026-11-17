import {
  byScoreThenCode,
  kpiAttentionThreshold,
  kpiAverage,
  kpiByStatus,
  kpiCompetitionRanks,
  kpiCompletionRate,
  kpiDefaultPeriod,
} from './kpi-figures';

// Every expected value below is worked out by hand (2 decimals, a midpoint away from zero), as the
// API's KpiFigures computes it.

describe('KPI figures: average score', () => {
  it('is the mean, rounded to 2 decimals half away from zero', () => {
    // (80.88 + 75.50 + 90.00) / 3 = 246.38 / 3 = 82.1266… → 82.13
    expect(kpiAverage([80.88, 75.5, 90])).toBe(82.13);
    // (80.01 + 80.02) / 2 = 80.015 → 80.02 (a midpoint goes away from zero)
    expect(kpiAverage([80.01, 80.02])).toBe(80.02);
    // (0.01 + 0.02) / 2 = 0.015 → 0.02, where float division would give 0.01
    expect(kpiAverage([0.01, 0.02])).toBe(0.02);
    expect(kpiAverage([75.88])).toBe(75.88);
    expect(kpiAverage([0])).toBe(0);
  });

  it('is null, never 0, without any value', () => {
    expect(kpiAverage([])).toBeNull();
  });
});

describe('KPI figures: completion rate', () => {
  it('is Finalized ÷ (past Planning) × 100, 2 decimals', () => {
    expect(kpiCompletionRate(2, 3)).toBe(66.67); // 66.666…
    expect(kpiCompletionRate(1, 6)).toBe(16.67); // 16.666…
    expect(kpiCompletionRate(1, 8)).toBe(12.5);
    expect(kpiCompletionRate(1, 32)).toBe(3.13); // 3.125 → 3.13
    expect(kpiCompletionRate(5, 5)).toBe(100);
    expect(kpiCompletionRate(0, 5)).toBe(0);
  });

  it('is 0 when nothing is past Planning (no division by zero)', () => {
    expect(kpiCompletionRate(0, 0)).toBe(0);
    expect(kpiCompletionRate(3, 0)).toBe(0);
  });
});

describe('KPI figures: attention threshold', () => {
  it('is the lowest Active band minimum of at least 60', () => {
    expect(kpiAttentionThreshold([90, 75, 60, 40, 0])).toBe(60); // the default bands
    expect(kpiAttentionThreshold([90, 75, 65, 40])).toBe(65);
    expect(kpiAttentionThreshold([59.99, 61, 80])).toBe(61);
  });

  it('is 60 when no band starts at 60 or above', () => {
    expect(kpiAttentionThreshold([50, 40, 0])).toBe(60);
    expect(kpiAttentionThreshold([])).toBe(60);
  });
});

describe('KPI figures: ranking', () => {
  it('uses standard competition ranking: equal scores share a rank and the next skips', () => {
    expect(kpiCompetitionRanks([90, 90, 80])).toEqual([1, 1, 3]);
    expect(kpiCompetitionRanks([95, 90, 90, 90, 70])).toEqual([1, 2, 2, 2, 5]);
    expect(kpiCompetitionRanks([88])).toEqual([1]);
    expect(kpiCompetitionRanks([])).toEqual([]);
  });

  it('keeps the shared rank on a page that starts inside a tie', () => {
    // Whole ranking 95, 90, 90, 90, 80: page 2 of size 3 holds positions 4 and 5. The first score
    // (90) has one higher score, so rank 2; the next follows by position: 5.
    expect(kpiCompetitionRanks([90, 80], 2, 4)).toEqual([2, 5]);
    expect(kpiCompetitionRanks([90, 90], 2, 3)).toEqual([2, 2]);
  });

  it('orders by score, unscored last, then employee code and id', () => {
    const rows = [
      { scorecardId: 1, employeeCode: 'EMP017', overallScore: 80 },
      { scorecardId: 2, employeeCode: 'EMP046', overallScore: null },
      { scorecardId: 3, employeeCode: 'EMP014', overallScore: 90 },
      { scorecardId: 4, employeeCode: 'EMP006', overallScore: 80 },
      { scorecardId: 5, employeeCode: 'EMP002', overallScore: null },
    ];
    expect([...rows].sort(byScoreThenCode).map((r) => r.scorecardId)).toEqual([3, 4, 1, 5, 2]);
  });
});

describe('KPI figures: status counts and default period', () => {
  it('lists all nine statuses in workflow order, zeros included', () => {
    expect(kpiByStatus(['Finalized', 'Draft', 'Finalized'])).toEqual([
      { status: 'Planning', count: 0 },
      { status: 'Draft', count: 1 },
      { status: 'Submitted', count: 0 },
      { status: 'Under Review', count: 0 },
      { status: 'Returned', count: 0 },
      { status: 'Resubmitted', count: 0 },
      { status: 'Approved', count: 0 },
      { status: 'Rejected', count: 0 },
      { status: 'Finalized', count: 2 },
    ]);
  });

  it('picks the most recent Active period, else the most recent one', () => {
    const closed = { id: 1, startDate: '2026-07-01', status: 'Closed' as const };
    const active = { id: 2, startDate: '2026-10-01', status: 'Active' as const };
    const later = { id: 3, startDate: '2027-01-01', status: 'Completed' as const };
    const twin = { id: 4, startDate: '2027-01-01', status: 'Closed' as const };
    expect(kpiDefaultPeriod([closed, active, later])).toBe(active);
    expect(kpiDefaultPeriod([closed, later])).toBe(later);
    expect(kpiDefaultPeriod([closed, later, twin])).toBe(twin); // same start: the higher id
    expect(kpiDefaultPeriod([])).toBeNull();
  });
});
