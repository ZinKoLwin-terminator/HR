import { TestBed } from '@angular/core/testing';
import { PermissionAction } from '../../core/models/settings.models';
import {
  GRIEVANCE_INSIGHT_METHODS,
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { GrievanceCaseRecord } from '../../mock-data/grievance/grievance-records';
import { GrievanceInsightMockService } from './grievance-insight-mock.service';
import { departmentCounts, resolutionRatePct, roundHalfAwayFromZero } from './grievance-metrics';
import {
  DANIEL,
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
  PAGE,
  configureGrievanceTest,
  failure,
  wait,
} from './grievance-test-support';

describe('grievance mock: dashboards and reports', () => {
  let insights: GrievanceInsightDataService;
  let session: ReturnType<typeof configureGrievanceTest>;

  beforeEach(() => {
    session = configureGrievanceTest(HR_ADMIN);
    insights = TestBed.inject(GrievanceInsightDataService);
  });

  it('binds every contract method to the mock', () => {
    for (const method of GRIEVANCE_INSIGHT_METHODS) {
      expect(typeof GrievanceInsightMockService.prototype[method], method).toBe('function');
    }
  });

  it('computes the HR dashboard from the sample cases', async () => {
    const d = await wait(insights.hrDashboard());
    expect(d).toMatchObject({
      total: 16,
      new: 2,
      underReview: 3,
      underInvestigation: 3,
      awaitingEmployeeResponse: 1,
      pendingResolution: 1,
      resolved: 2,
      closed: 3,
      rejected: 1,
      overdue: 3,
      escalated: 2,
      // (2 Resolved + 3 Closed) / (16 - 1 Rejected)
      resolutionRatePct: 33.33,
      // 12, 22, 20, 14 and 16 days
      averageResolutionDays: 16.8,
    });
    expect(d.byStatus).toHaveLength(11);
    expect(d.byStatus.reduce((n, s) => n + s.count, 0)).toBe(16);
    expect(d.monthlyTrend).toHaveLength(12);
    expect(d.recentActivity.length).toBeLessThanOrEqual(10);
  });

  it('suppresses small departments and never discloses anonymous ones', async () => {
    const d = await wait(insights.hrDashboard());
    expect(d.byDepartment).toEqual([
      { bucket: 'Department', departmentId: 8, departmentName: 'Customer Support', count: 4 },
      { bucket: 'Department', departmentId: 4, departmentName: 'Engineering', count: 5 },
      { bucket: 'NotDisclosed', departmentId: null, departmentName: 'Not disclosed', count: 5 },
      { bucket: 'Suppressed', departmentId: null, departmentName: 'Other / suppressed', count: 2 },
    ]);
  });

  it('gives every viewer the same totals, their own cases included', async () => {
    const my = TestBed.inject(MyGrievanceDataService);
    const draft = await wait(
      my.createDraft({
        categoryId: 1,
        subject: 'Sample case of the administrator',
        description: 'Sample text long enough to pass the description rule.',
        incidentDate: null,
        location: null,
        involvedPersonText: null,
        involvedDepartmentId: null,
        isAnonymous: true,
      }),
    );
    await wait(my.submit(draft.id, { rowVersion: draft.rowVersion, declarationAccepted: true }));
    // The administrator submitted it, and still counts it: totals never depend on the viewer.
    expect((await wait(insights.hrDashboard())).total).toBe(17);
  });

  it('lists recent activity by case number only', async () => {
    const d = await wait(insights.hrDashboard());
    for (const entry of d.recentActivity) {
      expect(Object.keys(entry).sort()).toEqual(
        ['action', 'caseId', 'caseNo', 'occurredAtUtc', 'toStatus'].sort(),
      );
    }
  });

  it('keeps the HR dashboard and the report to management', async () => {
    session.as(HR_OFFICER);
    expect((await failure(insights.hrDashboard())).code).toBe('Forbidden');
    expect((await failure(insights.casesReport(PAGE))).code).toBe('Forbidden');
    session.as(DANIEL);
    expect((await failure(insights.summary())).code).toBe('Forbidden');
  });

  it('computes the review dashboard over the review queue', async () => {
    const d = await wait(insights.reviewDashboard());
    expect(d).toMatchObject({
      pendingProposals: 1,
      escalated: 2,
      immediateEscalations: 1,
      overdue: 2,
      oldestPendingDays: 3,
      decidedByMe: { approved: 6, rejected: 0 },
    });
    expect(d.byPriority).toEqual([
      { priority: 'Low', count: 1 },
      { priority: 'Medium', count: 1 },
      { priority: 'High', count: 0 },
      { priority: 'Critical', count: 1 },
      { priority: null, count: 0 },
    ]);
  });

  it('reports cases with Not disclosed for anonymous ones, and filters', async () => {
    const all = await wait(insights.casesReport(PAGE));
    expect(all.total).toBe(16);
    const anonymous = all.items.find((r) => r.caseId === 9)!;
    expect(anonymous).toMatchObject({ departmentId: null, departmentName: 'Not disclosed' });
    expect(Object.keys(anonymous)).not.toContain('subject');
    const engineering = await wait(insights.casesReport({ ...PAGE, filters: { departmentId: 4 } }));
    expect(engineering.total).toBe(5);
    const slow = await wait(insights.casesReport({ ...PAGE, filters: { minResolutionDays: 15 } }));
    expect(slow.items.map((r) => r.caseId).sort((a, b) => a - b)).toEqual([13, 14, 18]);
    const overdue = await wait(insights.casesReport({ ...PAGE, filters: { overdue: true } }));
    expect(overdue.total).toBe(3);
    const badSort = await failure(
      insights.casesReport({ ...PAGE, sort: { active: 'subject', direction: 'asc' } }),
    );
    expect(badSort.code).toBe('Validation');
  });

  it('gives a reviewer the summary (no department filter)', async () => {
    const permissions = TestBed.inject(RolePermissionDataService);
    const current = await wait(permissions.getForRole(3));
    const review: PermissionAction[] = ['View', 'Approve'];
    await wait(permissions.save(3, { ...current.grants, 'grievance-review': review }));
    session.as(MANAGER);
    const summary = await wait(insights.summary({ priority: 'Low' }));
    // #11, #14 and #17 are Low.
    expect(summary.total).toBe(3);
    expect(summary.byDepartment.every((d) => d.bucket !== 'Department' || d.count >= 3)).toBe(true);
  });
});

describe('grievance metrics', () => {
  it('rounds half away from zero', () => {
    expect(roundHalfAwayFromZero(2.345, 2)).toBe(2.35);
    expect(roundHalfAwayFromZero(-2.345, 2)).toBe(-2.35);
    expect(roundHalfAwayFromZero(16.85, 1)).toBe(16.9);
    expect(roundHalfAwayFromZero(1.005, 2)).toBe(1.01);
  });

  const record = (over: Partial<GrievanceCaseRecord>): GrievanceCaseRecord =>
    ({ status: 'Submitted', isAnonymous: false, departmentId: 1, ...over }) as GrievanceCaseRecord;

  it('gives a rate of 0 when every case is rejected', () => {
    expect(resolutionRatePct([record({ status: 'Rejected' })])).toBe(0);
    expect(resolutionRatePct([])).toBe(0);
  });

  it('merges groups under three and counts named cases without a department as Not disclosed', () => {
    const rows = [
      record({ departmentId: 1 }),
      record({ departmentId: 1 }),
      record({ departmentId: 1 }),
      record({ departmentId: 2 }),
      record({ departmentId: null }),
      record({ isAnonymous: true, departmentId: null }),
    ];
    expect(departmentCounts(rows, (id) => `D${id}`)).toEqual([
      { bucket: 'Department', departmentId: 1, departmentName: 'D1', count: 3 },
      { bucket: 'NotDisclosed', departmentId: null, departmentName: 'Not disclosed', count: 2 },
      { bucket: 'Suppressed', departmentId: null, departmentName: 'Other / suppressed', count: 1 },
    ]);
  });
});
