import { DashboardSummary } from '../../core/models/dashboard.models';
import { buildDashboardReportCsv } from './dashboard-report';

const summary = (overrides: Partial<DashboardSummary>): DashboardSummary =>
  ({
    generatedAt: '2026-11-13T00:00:00Z',
    headcount: {},
    attendanceToday: {},
    pendingLeaveApprovals: 0,
    pendingLeaves: [],
    payroll: null,
    recruitment: null,
    departmentDistribution: [],
    attendanceTrend: [],
    headcountGrowth: [],
    recentActivities: [],
    upcomingEvents: [],
    ...overrides,
  }) as unknown as DashboardSummary;

describe('dashboard report CSV', () => {
  it('leaves out recruitment and payroll when the caller may not see them (API sends null)', () => {
    const csv = buildDashboardReportCsv(summary({}));
    expect(csv).not.toContain('Recruitment');
    expect(csv).not.toContain('Payroll');
  });

  it('includes recruitment figures when they are present', () => {
    const csv = buildDashboardReportCsv(
      summary({
        recruitment: {
          openPositions: 3,
          activeCandidates: 7,
          upcomingInterviews: 2,
          funnel: [{ label: 'Applied', count: 7 }],
        } as unknown as DashboardSummary['recruitment'],
      }),
    );
    expect(csv).toContain('Recruitment,Open positions,3');
    expect(csv).toContain('Recruitment funnel,Applied,7');
  });
});
