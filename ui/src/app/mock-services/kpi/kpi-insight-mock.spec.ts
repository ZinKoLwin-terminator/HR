import { TestBed } from '@angular/core/testing';
import { PageRequest } from '../../core/models/common.models';
import { ExportRequest } from '../../core/models/excel.models';
import { KpiReportFilter } from '../../core/models/kpi-insight.models';
import { Scorecard } from '../../core/models/kpi.models';
import { CurrentUser } from '../../core/services/current-user.service';
import {
  ExcelExportDataService,
  ExcelModuleCatalogDataService,
} from '../../core/services/data/excel-data.services';
import {
  KpiInsightDataService,
  ScorecardDataService,
} from '../../core/services/data/kpi-data.services';
import { MOCK_EVALUATION_PERIODS } from '../../mock-data/kpi/evaluation-periods.mock';
import { addDays } from '../../shared/utilities/date.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { KpiInsightMockService } from './kpi-insight-mock.service';
import {
  EMPLOYEE_46,
  EMPLOYEE_56,
  HR_ADMIN,
  MANAGER,
  PAGE,
  configureKpiTest,
  failure,
  wait,
} from './kpi-test-support';

// Seed (mock-data/kpi/scorecards.mock.ts). Period 2 (Active): EMP046 Submitted, EMP017 Draft, EMP019
// Planning, EMP006 Under Review, EMP014 Returned, EMP056 Draft, EMP002 Submitted. Period 1 (Closed):
// EMP046, EMP017, EMP014 and EMP006 Finalized, EMP056 Rejected. Daniel Carter (EMP005) manages
// EMP046, EMP017, EMP014, EMP019 and EMP023. Numbers below are counted by hand from that list.

const NO_EMPLOYEE: CurrentUser = { ...EMPLOYEE_46, id: 45, employeeId: null };
const request = (
  filters: Partial<KpiReportFilter> = {},
  extra: Partial<PageRequest<KpiReportFilter>> = {},
): PageRequest<KpiReportFilter> => ({ ...PAGE, filters, ...extra });

/** Mean of 2-decimal scores, rounded half away from zero, worked out the long way. */
const mean = (scores: number[]) =>
  Math.round(scores.reduce((sum, s) => sum + Math.round(s * 100), 0) / scores.length) / 100;

describe('KpiInsightMockService', () => {
  let as: (user: CurrentUser) => void;
  let insights: KpiInsightDataService;

  beforeEach(() => {
    ({ as } = configureKpiTest(HR_ADMIN));
    insights = TestBed.inject(KpiInsightDataService);
  });

  /** The Finalized scorecards of a period, as the scorecard list shows them (HR sees all). */
  async function finalizedOf(periodId: number): Promise<Scorecard[]> {
    const page = await wait(
      TestBed.inject(ScorecardDataService).query({
        ...PAGE,
        filters: { periodId, status: 'Finalized' },
      }),
    );
    return page.items;
  }

  describe('HR dashboard', () => {
    it('defaults to the Active period and counts each figure as defined', async () => {
      const d = await wait(insights.hrDashboard());
      expect(d.period?.id).toBe(2);
      expect(d.employeesWithKpis).toBe(7);
      expect(d.totalAssignments).toBe(5 + 5 + 2 + 4 + 5 + 4 + 4);
      expect(d.completedEvaluations).toBe(0);
      expect(d.pendingSubmissions).toBe(3); // Draft ×2, Returned
      expect(d.pendingApprovals).toBe(3); // Submitted ×2, Under Review
      expect(d.averageScore).toBeNull(); // nothing Finalized: null, never 0
      expect(d.completionRatePct).toBe(0); // 0 ÷ 6 past Planning
      expect(d.attentionThreshold).toBe(60); // the default bands: Meets Expectations at 60
      expect(d.byStatus.map((s) => s.status)).toHaveLength(9);
      expect(d.byStatus.find((s) => s.status === 'Planning')?.count).toBe(1);
    });

    it('averages Finalized scores only, never the Rejected one, and ranks the top performers', async () => {
      const d = await wait(insights.hrDashboard(1));
      const finalized = await finalizedOf(1);
      const scores = finalized.map((c) => c.overallScore as number);
      expect(d.completedEvaluations).toBe(4);
      expect(d.completionRatePct).toBe(80); // 4 Finalized ÷ 5 past Planning
      expect(d.averageScore).toBe(mean(scores)); // matches the scorecard list (KPI-38)
      expect(d.topPerformers.map((p) => p.overallScore)).toEqual([...scores].sort((a, b) => b - a));
      expect(d.topPerformers.map((p) => p.employeeCode)).not.toContain('EMP056'); // Rejected
      expect(d.ratingDistribution.reduce((sum, r) => sum + r.count, 0)).toBe(4);
      const below = scores.filter((s) => s < 60).length;
      expect(d.requiringAttentionCount).toBe(below);
      const eng = d.departments.find((x) => x.departmentName === 'Engineering');
      expect(eng?.finalized).toBe(3);
    });

    it('refuses anyone below the HR tier, as the API does (403)', async () => {
      as(MANAGER);
      const error = await failure(wait(insights.hrDashboard()));
      expect(error).toMatchObject({
        code: 'Forbidden',
        message: 'The HR dashboard needs HR rights.',
      });
    });

    it('answers NotFound for a period that does not exist', async () => {
      expect((await failure(wait(insights.hrDashboard(999)))).code).toBe('NotFound');
    });
  });

  describe('manager dashboard', () => {
    it('counts the direct reports and the scorecards naming the manager, never their own', async () => {
      as(MANAGER);
      const d = await wait(insights.managerDashboard());
      expect(d.period?.id).toBe(2);
      // EMP046 Submitted, EMP017 Draft, EMP019 Planning, EMP014 Returned.
      expect(d.teamSize).toBe(4);
      expect(d.pendingReviews).toBe(1); // EMP046, naming him reviewer
      expect(d.approvedOrFinalized).toBe(0);
      expect(d.employees.map((e) => e.employeeCode)).not.toContain('EMP056');
      // Highest score first, unscored last.
      expect(d.employees[0].employeeCode).toBe('EMP046');
      expect(d.employees.slice(1).every((e) => e.overallScore === null)).toBe(true);
    });

    it('gives the team average of the Finalized scorecards of the chosen period', async () => {
      as(MANAGER);
      const d = await wait(insights.managerDashboard(1));
      as(HR_ADMIN);
      const team = (await finalizedOf(1)).filter((c) =>
        ['EMP046', 'EMP017', 'EMP014'].includes(c.employeeCode),
      );
      expect(d.teamSize).toBe(3);
      expect(d.approvedOrFinalized).toBe(3);
      expect(d.averageScore).toBe(mean(team.map((c) => c.overallScore as number)));
    });
  });

  describe('employee dashboard', () => {
    it('shows the own scorecard of the default period, its KPIs, feedback and history', async () => {
      as(EMPLOYEE_46);
      const d = await wait(insights.employeeDashboard());
      expect(d.period?.id).toBe(2);
      expect(d.scorecard?.status).toBe('Submitted');
      expect(d.items).toHaveLength(5);
      expect(d.items.map((i) => i.kpiCode)[0]).toBe('ENG-REVIEW-HOURS');
      expect(d.pending.isPending).toBe(false);
      expect(d.history.map((h) => [h.periodId, h.status])).toEqual([[1, 'Finalized']]);
      const earlier = await wait(insights.employeeDashboard(1));
      expect(earlier.feedback?.managerRating).toBe(4);
      expect(earlier.history).toEqual([]); // nothing starts before period 1
    });

    it('flags a pending submission, and overdue after the due date', async () => {
      as(EMPLOYEE_56);
      const d = await wait(insights.employeeDashboard());
      const due = MOCK_EVALUATION_PERIODS.find((p) => p.id === 2)!.submissionDueDate!;
      expect(d.pending).toEqual({ isPending: true, dueDate: due, isOverdue: false });

      vi.useFakeTimers({ toFake: ['Date'] });
      try {
        vi.setSystemTime(new Date(`${addDays(due, 1)}T09:00:00`));
        const late = await wait(insights.employeeDashboard());
        expect(late.pending).toEqual({ isPending: true, dueDate: due, isOverdue: true });
      } finally {
        vi.useRealTimers();
      }
    });

    it('is empty for an account without an employee', async () => {
      as(NO_EMPLOYEE);
      const d = await wait(insights.employeeDashboard());
      expect(d).toMatchObject({ period: null, scorecard: null, items: [], history: [] });
    });
  });

  describe('reports', () => {
    it('summarises each period, newest first, with the completion rate', async () => {
      const page = await wait(insights.summaryReport(request()));
      expect(page.items.map((r) => r.periodId)).toEqual([2, 1]);
      const [current, previous] = page.items;
      expect(current).toMatchObject({ scorecards: 7, inPlanning: 1, finalized: 0, rejected: 0 });
      expect(current.completionRatePct).toBe(0);
      expect(current.averageScore).toBeNull();
      expect(previous).toMatchObject({ scorecards: 5, finalized: 4, rejected: 1 });
      expect(previous.completionRatePct).toBe(80);
      const scores = (await finalizedOf(1)).map((c) => c.overallScore as number);
      expect(previous.minScore).toBe(Math.min(...scores));
      expect(previous.maxScore).toBe(Math.max(...scores));
    });

    it('filters the summary by period type and year (Monthly, Quarterly, Annual)', async () => {
      expect((await wait(insights.summaryReport(request({ periodType: 'Quarterly' })))).total).toBe(
        2,
      );
      expect((await wait(insights.summaryReport(request({ periodType: 'Annual' })))).total).toBe(0);
      const year = Number(MOCK_EVALUATION_PERIODS[1].startDate.slice(0, 4));
      const inYear = await wait(insights.summaryReport(request({ year })));
      expect(inYear.items.map((r) => r.periodId)).toContain(2);
    });

    it('refuses unknown filters and sort fields with the API messages (400)', async () => {
      const status = await failure(
        wait(insights.summaryReport(request({ status: 'Done' as never }))),
      );
      expect(status.fieldErrors?.['status']).toContain('Unknown status "Done"');
      const type = await failure(
        wait(insights.summaryReport(request({ periodType: 'Weekly' as never }))),
      );
      expect(type.fieldErrors?.['periodType']).toContain('(or Annual)');
      const year = await failure(wait(insights.summaryReport(request({ year: 1899 }))));
      expect(year.fieldErrors?.['year']).toBe('Year must be between 1900 and 9998.');
      const sort = await failure(
        wait(insights.summaryReport(request({}, { sort: { active: 'score', direction: 'asc' } }))),
      );
      expect(sort.fieldErrors?.['sort']).toContain('Cannot sort by "score"');
    });

    it('ranks Finalized scorecards only, with ranks that hold across pages', async () => {
      const all = await wait(insights.rankingReport(request({ periodId: 1 })));
      expect(all.total).toBe(4); // the Rejected one is not ranked
      const scores = all.items.map((r) => r.overallScore);
      expect(scores).toEqual([...scores].sort((a, b) => b - a));
      expect(all.items[0].rank).toBe(1);
      const second = await wait(
        insights.rankingReport({ pageIndex: 1, pageSize: 2, filters: { periodId: 1 } }),
      );
      expect(second.items.map((r) => r.rank)).toEqual(all.items.slice(2).map((r) => r.rank));
    });

    it('accepts only rank:asc as the ranking order', async () => {
      const ok = await wait(
        insights.rankingReport(request({}, { sort: { active: 'rank', direction: 'asc' } })),
      );
      expect(ok.total).toBe(4);
      const bad = await failure(
        wait(insights.rankingReport(request({}, { sort: { active: 'rank', direction: 'desc' } }))),
      );
      expect(bad.fieldErrors?.['sort']).toBe(
        'The ranking is always ordered by rank (sort=rank:asc).',
      );
    });

    it('lists a manager’s team only (scoped like the scorecard reads)', async () => {
      as(MANAGER);
      const rows = await wait(insights.targetVsActualReport(request()));
      const codes = new Set(rows.items.map((r) => r.employeeCode));
      // Own (none), direct reports and the scorecards naming him reviewer.
      expect([...codes].every((c) => ['EMP046', 'EMP017', 'EMP014', 'EMP019'].includes(c))).toBe(
        true,
      );
      expect(codes.has('EMP056')).toBe(false);
    });

    it('gives one row per KPI in target vs actual, newest period first, and narrows by category', async () => {
      const all = await wait(insights.targetVsActualReport(request({ periodId: 1 })));
      expect(all.total).toBe(5 + 5 + 5 + 4 + 4); // every status, Rejected included
      expect(all.items.some((r) => r.status === 'Rejected')).toBe(true);
      const categoryId = (await wait(TestBed.inject(ScorecardDataService).getById(8))).items[0]
        .categoryId;
      const narrowed = await wait(
        insights.targetVsActualReport(request({ periodId: 1, categoryId })),
      );
      expect(narrowed.total).toBeGreaterThan(0);
      expect(narrowed.total).toBeLessThan(all.total);
      const newest = await wait(insights.targetVsActualReport(request()));
      expect(newest.items[0].periodId).toBe(2);
    });

    it('averages KPI achievement over Finalized scorecards only', async () => {
      const page = await wait(insights.kpiAchievementReport(request({ periodId: 1 })));
      const output = page.items.find((r) => r.kpiCode === 'OUTPUT-TASKS')!;
      // On the engineering plans (EMP046, EMP017, EMP014, all Finalized) and the general plan of
      // EMP056 (Rejected): 4 assigned, 3 counted. The support plan of EMP006 does not have it.
      expect(output.assigned).toBe(4);
      expect(output.counted).toBe(3);
      expect(page.items.map((r) => r.kpiCode)).toEqual(
        [...page.items.map((r) => r.kpiCode)].sort(),
      );
    });

    it('shows one employee’s history with totals over every matching scorecard', async () => {
      const report = await wait(insights.employeeReport(46, request()));
      expect(report.employeeCode).toBe('EMP046');
      expect(report.finalized).toBe(1);
      expect(report.scorecards.items.map((s) => s.periodId)).toEqual([2, 1]);
      expect(report.scorecards.items[1].items).toHaveLength(5);
      as(EMPLOYEE_56);
      expect((await failure(wait(insights.employeeReport(46, request())))).code).toBe('NotFound');
    });

    it('reports a department for one period by the department copied onto the scorecard', async () => {
      const card = await wait(TestBed.inject(ScorecardDataService).getById(8));
      const report = await wait(
        insights.departmentReport(card.departmentId, request({ periodId: 1 })),
      );
      expect(report.period?.id).toBe(1);
      expect(report.rows.items.map((r) => r.employeeCode)).toContain('EMP046');
      const codes = report.rows.items.map((r) => r.employeeCode);
      expect(codes).toEqual([...codes].sort()); // by employee code
      expect((await failure(wait(insights.departmentReport(9999, request())))).code).toBe(
        'NotFound',
      );
    });

    it('keeps a transferred employee in their old department for the period (KPI-42)', async () => {
      const cards = TestBed.inject(ScorecardDataService);
      const engineering = (await wait(cards.getById(8))).departmentId; // EMP046, period 1
      const otherCard = await wait(cards.getById(12)); // EMP056 (another department)
      const employees = TestBed.inject(EmployeeMockService);
      const other = employees.findById(56)!;
      // EMP046 moves to EMP056's department, branch, section and position after period 1.
      employees.updateNow(
        46,
        {
          departmentId: other.departmentId,
          branchId: other.branchId,
          sectionId: other.sectionId,
          positionId: other.positionId,
          jobGradeId: other.jobGradeId,
        },
        false,
      );
      expect(employees.findById(46)!.departmentId).toBe(otherCard.departmentId);
      const before = await wait(insights.departmentReport(engineering, request({ periodId: 1 })));
      expect(before.rows.items.map((r) => r.employeeCode)).toContain('EMP046');
      const after = await wait(
        insights.departmentReport(otherCard.departmentId, request({ periodId: 1 })),
      );
      expect(after.rows.items.map((r) => r.employeeCode)).not.toContain('EMP046');
    });
  });

  describe('Excel exports', () => {
    const exportRequest = (moduleCode: string, extra: Partial<ExportRequest> = {}) =>
      ({ moduleCode, scope: 'filtered', filters: { periodId: 1 }, ...extra }) as ExportRequest;

    it('lists the three KPI modules as export only', async () => {
      const modules = await wait(TestBed.inject(ExcelModuleCatalogDataService).getModules());
      for (const code of ['kpi-scorecards', 'kpi-ranking', 'kpi-target-vs-actual']) {
        expect(
          modules.find((m) => m.code === code),
          code,
        ).toMatchObject({
          canImport: false,
          canExport: true,
          exportDateLabel: 'Period dates',
        });
      }
    });

    it('exports the report rows with the same filters, ranks over the exported rows', async () => {
      const service = TestBed.inject(KpiInsightMockService);
      const ranking = service.exportRows('kpi-ranking', exportRequest('kpi-ranking'), 100);
      expect(ranking.map((r) => r['rank'])[0]).toBe(1);
      expect(ranking).toHaveLength(4);
      const cards = service.exportRows('kpi-scorecards', exportRequest('kpi-scorecards'), 100);
      expect(cards).toHaveLength(5);
      expect(Object.keys(cards[0])).not.toContain('managerComment');
      const selected = service.exportRows(
        'kpi-ranking',
        exportRequest('kpi-ranking', { scope: 'selected', selectedIds: [8, 12] }),
        100,
      );
      expect(selected).toHaveLength(1); // 12 is Rejected: never ranked
      expect(selected[0]['rank']).toBe(1);
      const items = service.exportRows(
        'kpi-target-vs-actual',
        exportRequest('kpi-target-vs-actual'),
        100,
      );
      expect(items).toHaveLength(23);
    });

    it('writes a workbook through the shared export flow', { timeout: 30_000 }, async () => {
      const file = await wait(
        TestBed.inject(ExcelExportDataService).export(exportRequest('kpi-ranking')),
      );
      expect(file.fileName).toMatch(/^kpi-ranking-export-\d{8}-\d{4}\.xlsx$/);
      expect(file.blob.size).toBeGreaterThan(0);
    });
  });
});
