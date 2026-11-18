import { Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError, PageRequest } from '../../core/models/common.models';
import {
  KpiEmployeeDashboard,
  KpiHrDashboard,
  KpiManagerDashboard,
  KpiPeriodRef,
  KpiRankingRow,
  KpiReportFilter,
} from '../../core/models/kpi-insight.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { KpiInsightDataService } from '../../core/services/data/kpi-data.services';
import { PrintService } from '../../core/services/print.service';
import { kpiByStatus } from '../../core/utilities/kpi-figures';
import { HR_ADMIN } from '../../mock-services/kpi/kpi-test-support';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { KpiDashboardComponent } from './dashboard/kpi-dashboard.component';
import { kpiDashboardTabs } from './dashboard/kpi-dashboard-views';
import { KpiReportsComponent } from './reports/kpi-reports.component';

// Fictional dashboard answers: the screens only display them, so the stubs give fixed figures.

const PERIOD: KpiPeriodRef = {
  id: 2,
  name: '2026 Q4',
  periodType: 'Quarterly',
  startDate: '2026-10-01',
  endDate: '2026-12-31',
  status: 'Active',
  submissionDueDate: '2027-01-14',
};

const EMPLOYEE_DASHBOARD: KpiEmployeeDashboard = {
  period: PERIOD,
  scorecard: {
    id: 6,
    status: 'Draft',
    overallScore: null,
    ratingLabel: null,
    reviewerEmployeeId: 5,
    reviewerName: 'Daniel Carter',
    submittedOn: null,
    finalizedOn: null,
  },
  items: [
    {
      id: 61,
      kpiCode: 'OUTPUT-TASKS',
      kpiName: 'Tasks completed',
      unit: 'tasks',
      target: 30,
      actualValue: 24,
      achievementPct: 80,
      weight: 40,
      weightedScore: 32,
    },
  ],
  pending: { isPending: true, dueDate: '2027-01-14', isOverdue: true },
  feedback: { managerRating: null, managerComment: null },
  history: [
    {
      scorecardId: 12,
      periodId: 1,
      periodName: '2026 Q3',
      periodType: 'Quarterly',
      startDate: '2026-07-01',
      endDate: '2026-09-30',
      status: 'Finalized',
      overallScore: 80.88,
      ratingLabel: 'Exceeds Expectations',
    },
  ],
};

const MANAGER_DASHBOARD: KpiManagerDashboard = {
  period: PERIOD,
  teamSize: 2,
  byStatus: kpiByStatus(['Submitted', 'Finalized']),
  pendingReviews: 1,
  approvedOrFinalized: 1,
  averageScore: 75.88,
  employees: [
    {
      scorecardId: 1,
      employeeId: 46,
      employeeCode: 'EMP046',
      employeeName: 'Hein Thant Zin',
      departmentId: 3,
      departmentName: 'Engineering',
      status: 'Finalized',
      overallScore: 75.88,
      ratingLabel: 'Exceeds Expectations',
    },
  ],
  employeeCount: 2,
  employeesTruncated: true,
};

const HR_DASHBOARD: KpiHrDashboard = {
  period: PERIOD,
  companyId: null,
  employeesWithKpis: 7,
  totalAssignments: 29,
  completedEvaluations: 4,
  pendingSubmissions: 3,
  pendingApprovals: 3,
  averageScore: 82.13,
  completionRatePct: 66.67,
  byStatus: kpiByStatus(['Finalized']),
  departments: [
    {
      departmentId: 3,
      departmentName: 'Engineering',
      scorecards: 3,
      finalized: 3,
      averageScore: 82.13,
    },
  ],
  topPerformers: MANAGER_DASHBOARD.employees,
  attentionThreshold: 65,
  requiringAttentionCount: 0,
  requiringAttention: [],
  ratingDistribution: [{ ratingLabel: 'Exceeds Expectations', count: 4 }],
};

const ranking = (rank: number, code: string, score: number): KpiRankingRow => ({
  rank,
  scorecardId: rank * 10 + code.length,
  employeeId: rank,
  employeeCode: code,
  employeeName: `Sample ${code}`,
  departmentId: 3,
  departmentName: 'Engineering',
  periodId: 1,
  periodName: '2026 Q3',
  overallScore: score,
  ratingLabel: 'Outstanding',
});
const RANKING = [ranking(1, 'EMP014', 92), ranking(1, 'EMP046', 92), ranking(3, 'EMP017', 80)];

const page = <T>(items: T[], request?: PageRequest<object>) => ({
  items,
  total: items.length,
  pageIndex: request?.pageIndex ?? 0,
  pageSize: request?.pageSize ?? 10,
});

function stubInsights() {
  return {
    employeeDashboard: vi.fn(() => of(EMPLOYEE_DASHBOARD)),
    managerDashboard: vi.fn(() => of(MANAGER_DASHBOARD)),
    hrDashboard: vi.fn(() => of(HR_DASHBOARD)),
    summaryReport: vi.fn((r: PageRequest<KpiReportFilter>) => of(page([], r))),
    employeeReport: vi.fn(() =>
      of({
        employeeId: 46,
        employeeCode: 'EMP046',
        employeeName: 'Hein Thant Zin',
        finalized: 0,
        averageScore: null,
        scorecards: page([]),
      }),
    ),
    departmentReport: vi.fn(() =>
      of({
        departmentId: 3,
        departmentName: 'Engineering',
        period: PERIOD,
        scorecards: 0,
        finalized: 0,
        averageScore: null,
        rows: page([]),
      }),
    ),
    rankingReport: vi.fn((r: PageRequest<KpiReportFilter>) => of(page(RANKING, r))),
    kpiAchievementReport: vi.fn((r: PageRequest<KpiReportFilter>) => of(page([], r))),
    targetVsActualReport: vi.fn((r: PageRequest<KpiReportFilter>) => of(page([], r))),
  };
}

let insights: ReturnType<typeof stubInsights>;

function configure(permissions: string[] | undefined) {
  insights = stubInsights();
  const user: CurrentUser = { ...HR_ADMIN, permissions };
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user) } },
      { provide: KpiInsightDataService, useValue: insights },
    ],
  });
}

async function settle(fixture: ComponentFixture<unknown>, rounds = 8) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
}

async function render<T>(component: Type<T>) {
  const fixture = TestBed.createComponent(component);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const internals = <T>(fixture: ComponentFixture<unknown>) =>
  fixture.componentInstance as unknown as T;
const tabLabels = (el: HTMLElement) =>
  Array.from(el.querySelectorAll('[role="tab"]')).map((t) => t.textContent?.trim());

const EMPLOYEE = ['kpi:View', 'kpi:Create'];
const MANAGER = ['kpi:View', 'kpi:Create', 'kpi:Approve', 'employees:View'];
const HR = [...MANAGER, 'employees:Edit', 'excel:Export', 'organization:View', 'kpi-setup:View'];

describe('KPI dashboard: views per role', () => {
  it('offers own, team and HR views by kpi:View, kpi:Approve and the HR tier', () => {
    const tabs = (held: string[]) => kpiDashboardTabs((k) => held.includes(k)).map((t) => t.view);
    expect(tabs(EMPLOYEE)).toEqual(['employee']);
    expect(tabs(MANAGER)).toEqual(['employee', 'manager']);
    expect(tabs(HR)).toEqual(['employee', 'manager', 'hr']);
    // employees:Edit alone is not the HR tier (it needs kpi:Approve too).
    expect(tabs(['kpi:View', 'employees:Edit'])).toEqual(['employee']);
    expect(tabs([])).toEqual([]);
  });

  it('shows a single view without tabs to an employee, with the overdue banner', async () => {
    configure(EMPLOYEE);
    const { el } = await render(KpiDashboardComponent);
    expect(el.querySelector('[role="tablist"]')).toBeNull();
    expect(insights.employeeDashboard).toHaveBeenCalledWith(null);
    expect(insights.managerDashboard).not.toHaveBeenCalled();
    expect(insights.hrDashboard).not.toHaveBeenCalled();
    const banner = el.querySelector('[data-testid="pending-banner"]');
    expect(banner?.textContent).toContain('Overdue');
    expect(banner?.getAttribute('role')).toBe('status');
    expect(el.textContent).toContain('OUTPUT-TASKS');
    expect(el.textContent).toContain('No feedback yet');
    // The history chart has a text alternative and a table twin.
    const canvas = el.querySelector('canvas[role="img"]');
    expect(canvas?.getAttribute('aria-label')).toContain('2026 Q3 80.88');
  });

  it('gives a manager two tabs, opens the team view and never asks for the HR one', async () => {
    configure(MANAGER);
    const { el } = await render(KpiDashboardComponent);
    expect(tabLabels(el)).toEqual(['My performance', 'My team']);
    expect(el.querySelector('[role="tab"][aria-selected="true"]')?.textContent).toContain(
      'My team',
    );
    expect(insights.managerDashboard).toHaveBeenCalled();
    expect(insights.hrDashboard).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Pending reviews');
    expect(el.textContent).toContain('EMP046');
    // The server capped the list (1 of 2 here): the screen says so and points to the reports.
    const note = el.querySelector('[data-testid="team-truncated"]');
    expect(note?.textContent).toContain('Showing the first 1 of 2 team scorecards');
    expect(note?.querySelector('a')?.getAttribute('href')).toBe('/performance/reports');
  });

  it('says nothing about a cap when the team list is complete', async () => {
    configure(MANAGER);
    insights.managerDashboard.mockReturnValue(
      of({ ...MANAGER_DASHBOARD, teamSize: 1, employeeCount: 1, employeesTruncated: false }),
    );
    const { el } = await render(KpiDashboardComponent);
    expect(el.textContent).toContain('EMP046');
    expect(el.querySelector('[data-testid="team-truncated"]')).toBeNull();
  });

  it('gives HR three tabs, the HR overview first, with the threshold the server used', async () => {
    configure(HR);
    const { fixture, el } = await render(KpiDashboardComponent);
    expect(tabLabels(el)).toEqual(['My performance', 'My team', 'HR overview']);
    expect(insights.hrDashboard).toHaveBeenCalledWith(null, null);
    expect(el.textContent).toContain('66.67%');
    expect(el.querySelector('[data-testid="attention-threshold"]')?.textContent).toContain('65.00');

    // A period choice applies to the view; the employee tab loads only when opened.
    internals<{ choosePeriod(id: number): void }>(fixture).choosePeriod(1);
    await settle(fixture);
    expect(insights.hrDashboard).toHaveBeenLastCalledWith(1, null);
    expect(insights.employeeDashboard).not.toHaveBeenCalled();
    (el.querySelectorAll('[role="tab"]')[0] as HTMLElement).click();
    await settle(fixture);
    expect(insights.employeeDashboard).toHaveBeenCalledWith(1);
  });

  it('shows the server’s 403 as “not available” instead of an error to retry', async () => {
    configure(HR);
    const forbidden: AppError = { code: 'Forbidden', message: 'The HR dashboard needs HR rights.' };
    insights.hrDashboard.mockReturnValue(throwError(() => forbidden) as never);
    const { el } = await render(KpiDashboardComponent);
    expect(el.textContent).toContain('Not available for your role');
    expect(el.textContent).toContain('The HR dashboard needs HR rights.');
    expect(el.textContent).not.toContain('Try again');
  });
});

interface ReportsPage {
  selectReport(id: string): void;
  setFilter(key: keyof KpiReportFilter, value: unknown): void;
  chooseDepartment(id: number | null): void;
  query: {
    onPage(e: { pageIndex: number; pageSize: number; length: number }): void;
    setSort(s: { active: string; direction: 'asc' | 'desc' | '' }): void;
  };
}

const lastRequest = (fn: { mock: { calls: unknown[][] } }, arg = 0) =>
  fn.mock.calls.at(-1)?.[arg] as PageRequest<KpiReportFilter>;

describe('KPI reports: filters, paging and sorting', () => {
  it('loads the summary, then sends filters (back to page 1), paging and whitelisted sorts', async () => {
    configure(HR);
    const { fixture } = await render(KpiReportsComponent);
    const screen = internals<ReportsPage>(fixture);
    expect(lastRequest(insights.summaryReport)).toMatchObject({ pageIndex: 0, filters: {} });

    screen.query.onPage({ pageIndex: 2, pageSize: 25, length: 100 });
    await settle(fixture);
    expect(lastRequest(insights.summaryReport)).toMatchObject({ pageIndex: 2, pageSize: 25 });

    screen.setFilter('periodId', 1);
    screen.setFilter('status', 'Finalized');
    await settle(fixture);
    expect(lastRequest(insights.summaryReport)).toMatchObject({
      pageIndex: 0,
      filters: { periodId: 1, status: 'Finalized' },
    });

    screen.query.setSort({ active: 'periodName', direction: 'desc' });
    await settle(fixture);
    expect(lastRequest(insights.summaryReport).sort).toEqual({
      active: 'periodName',
      direction: 'desc',
    });
    // Not a sort field of the summary: nothing is sent.
    screen.query.setSort({ active: 'scorecards', direction: 'asc' });
    await settle(fixture);
    expect(lastRequest(insights.summaryReport).sort).toBeUndefined();
  });

  it('presets the period type for the Monthly, Quarterly and Annual summaries', async () => {
    configure(HR);
    const { fixture } = await render(KpiReportsComponent);
    internals<ReportsPage>(fixture).selectReport('summary-annual');
    await settle(fixture);
    expect(lastRequest(insights.summaryReport).filters).toEqual({ periodType: 'Yearly' });
    internals<ReportsPage>(fixture).selectReport('summary-monthly');
    await settle(fixture);
    expect(lastRequest(insights.summaryReport).filters).toEqual({ periodType: 'Monthly' });
  });

  it('shows the ranking in its fixed order with shared ranks, never sending a sort', async () => {
    configure(HR);
    const { fixture, el } = await render(KpiReportsComponent);
    const screen = internals<ReportsPage>(fixture);
    screen.selectReport('ranking');
    await settle(fixture);
    screen.query.setSort({ active: 'overallScore', direction: 'desc' });
    await settle(fixture);
    expect(lastRequest(insights.rankingReport).sort).toBeUndefined();
    const ranks = Array.from(el.querySelectorAll('tr.mat-mdc-row')).map((r) =>
      r.querySelector('td')?.textContent?.trim(),
    );
    expect(ranks).toEqual(['1', '1', '3']);
    // Status is not a filter of the ranking (it is Finalized only).
    screen.setFilter('status', 'Draft');
    await settle(fixture);
    expect(lastRequest(insights.rankingReport).filters).toEqual({});
  });

  it('waits for a department, then sends it in the route only', async () => {
    configure(HR);
    const { fixture, el } = await render(KpiReportsComponent);
    const screen = internals<ReportsPage>(fixture);
    screen.selectReport('department');
    await settle(fixture);
    expect(insights.departmentReport).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Choose a department');
    screen.chooseDepartment(3);
    await settle(fixture);
    const [departmentId, request] = insights.departmentReport.mock.calls.at(-1) as unknown as [
      number,
      PageRequest<KpiReportFilter>,
    ];
    expect(departmentId).toBe(3);
    expect(request.filters).not.toHaveProperty('departmentId');
    expect(el.textContent).toContain('2026 Q4'); // the period the server chose
  });
});

describe('KPI reports: Excel export and print', () => {
  const exportButton = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('app-excel-actions button')).find((b) =>
      b.textContent?.includes('Export'),
    );

  it('offers Export for the ranking with excel:Export, and not without it', async () => {
    configure(HR);
    const { fixture, el } = await render(KpiReportsComponent);
    internals<ReportsPage>(fixture).selectReport('ranking');
    await settle(fixture, 15);
    expect(exportButton(el)).toBeTruthy();

    TestBed.resetTestingModule();
    configure(HR.filter((k) => k !== 'excel:Export'));
    const without = await render(KpiReportsComponent);
    internals<ReportsPage>(without.fixture).selectReport('ranking');
    await settle(without.fixture, 15);
    expect(exportButton(without.el)).toBeUndefined();
  });

  it('has no export on the KPI achievement report (no Excel module for it)', async () => {
    configure(HR);
    const { fixture, el } = await render(KpiReportsComponent);
    internals<ReportsPage>(fixture).selectReport('kpi-achievement');
    await settle(fixture);
    expect(el.querySelector('app-excel-actions')).toBeNull();
  });

  it('lays the page out for paper and opens the browser print dialog', async () => {
    configure(HR);
    const printer = TestBed.inject(PrintService);
    const print = vi.spyOn(printer, 'print').mockImplementation(() => undefined);
    const { fixture, el } = await render(KpiReportsComponent);
    internals<ReportsPage>(fixture).selectReport('ranking');
    await settle(fixture);
    expect(el.querySelector('.print-only')).toBeNull();

    (el.querySelector('button[aria-label="Print Ranking"]') as HTMLButtonElement).click();
    await settle(fixture);
    expect(print).toHaveBeenCalledTimes(1);
    const layout = el.querySelector('[data-testid="report-print"]');
    expect(layout?.classList).toContain('print-only');
    expect(layout?.querySelector('h1')?.textContent).toContain('Ranking');
    expect(layout?.querySelectorAll('table.print-table tbody tr')).toHaveLength(3);
    expect(el.querySelector('app-data-table')?.classList).toContain('no-print');

    printer.afterPrint$.next();
    await settle(fixture);
    expect(el.querySelector('.print-only')).toBeNull();
  });
});
