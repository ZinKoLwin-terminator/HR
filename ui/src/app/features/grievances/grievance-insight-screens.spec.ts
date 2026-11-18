import { Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { MODULE_GUIDE } from '../../core/constants/module-guide';
import { NAVIGATION } from '../../core/constants/navigation';
import { SCREEN_DETAILS } from '../../core/constants/screen-details';
import { WORKFLOWS } from '../../core/constants/workflow-guide';
import { ExportRequest } from '../../core/models/excel.models';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { ExcelModuleCatalogDataService } from '../../core/services/data/excel-data.services';
import {
  GrievanceCaseDataService,
  GrievanceInsightDataService,
} from '../../core/services/data/grievance-data.services';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { PrintService } from '../../core/services/print.service';
import {
  GRIEVANCE_CASES_EXPORT_COLUMNS,
  GRIEVANCE_CASES_MODULE_INFO,
} from '../../mock-services/excel/modules/grievance-excel.modules';
import { GrievanceInsightMockService } from '../../mock-services/grievance/grievance-insight-mock.service';
import { GrievanceMockStore } from '../../mock-services/grievance/grievance-mock-store';
import {
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
} from '../../mock-services/grievance/grievance-test-support';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { GrievanceAccessLogComponent } from './insights/grievance-access-log.component';
import { GrievanceDashboardComponent } from './insights/grievance-dashboard.component';
import { GrievanceReportsComponent } from './insights/grievance-reports.component';
import { GrievanceReviewSummaryComponent } from './insights/grievance-review-summary.component';

async function settle(fixture: ComponentFixture<unknown>, rounds = 6) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

function configure(user: CurrentUser = HR_ADMIN) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user) } },
      { provide: ConfirmDialogService, useValue: { confirm: () => of(true) } },
    ],
  });
  vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
}

async function render<T>(component: Type<T>) {
  const fixture = TestBed.createComponent(component);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const text = (el: HTMLElement) => el.textContent?.replace(/\s+/g, ' ') ?? '';
const chartLabels = (el: HTMLElement) =>
  Array.from(el.querySelectorAll('canvas[role="img"]')).map(
    (c) => c.getAttribute('aria-label') ?? '',
  );

describe('grievance dashboard', () => {
  it('shows the server’s counts and charts, with the suppressed and not-disclosed groups', async () => {
    configure();
    const { el } = await render(GrievanceDashboardComponent);
    expect(text(el)).toContain('Total');
    expect(text(el)).toContain('33.33%');
    expect(text(el)).toContain('16.8 days');
    const charts = chartLabels(el);
    expect(charts.length).toBe(5);
    const departments = charts.find((c) => c.includes('By department'))!;
    expect(departments).toContain('Not disclosed 5');
    expect(departments).toContain('Other / suppressed 2');
    // Recent activity: case numbers only, linking to the case.
    const links = Array.from(el.querySelectorAll('a[href^="/grievances/cases/"]'));
    expect(links.length).toBeGreaterThan(0);
    expect(links[0].textContent).toMatch(/^GRV-\d{4}-\d{5}$/);
  });
});

describe('grievance review summary', () => {
  it('gives a reviewer-only role the queue figures and the summary', async () => {
    configure(MANAGER);
    const permissions = TestBed.inject(RolePermissionDataService);
    const now = await firstValueFrom(permissions.getForRole(3));
    await firstValueFrom(
      permissions.save(3, { ...now.grants, 'grievance-review': ['View', 'Approve'] }),
    );
    const { el } = await render(GrievanceReviewSummaryComponent);
    expect(text(el)).toContain('Proposals to decide');
    expect(text(el)).toContain('Grievance summary');
    expect(chartLabels(el).length).toBe(4);
  });
});

// Regression: value() of a resource in error throws (ResourceValueError), so the refused screens
// never reached their error state. Ye Yint Naing (HR Officer) holds no management or review key.
describe('grievance insight error states', () => {
  it('shows the error state of each refused screen', async () => {
    const screens: [Type<unknown>, string][] = [
      [GrievanceDashboardComponent, 'The dashboard could not be loaded'],
      [GrievanceReportsComponent, 'The report could not be loaded'],
      [GrievanceReviewSummaryComponent, 'The review figures could not be loaded'],
    ];
    for (const [component, title] of screens) {
      configure(HR_OFFICER);
      const { el } = await render(component);
      expect(text(el)).toContain(title);
      expect(text(el)).toContain('You do not have permission to do this.');
      TestBed.resetTestingModule();
    }
  });

  it('shows the access log’s error state when the log cannot be read', async () => {
    configure(HR_ADMIN);
    vi.spyOn(TestBed.inject(GrievanceCaseDataService), 'auditLog').mockReturnValue(
      throwError(() => ({ code: 'Unknown', message: 'The log is unavailable.' })),
    );
    const { el } = await render(GrievanceAccessLogComponent);
    expect(text(el)).toContain('The access log could not be loaded');
    expect(text(el)).toContain('The log is unavailable.');
  });
});

describe('grievance reports', () => {
  it('sends the filters to the report query and the summary', async () => {
    configure();
    const insights = TestBed.inject(GrievanceInsightDataService);
    const report = vi.spyOn(insights, 'casesReport');
    const summary = vi.spyOn(insights, 'summary');
    const { fixture, el } = await render(GrievanceReportsComponent);
    const page = fixture.componentInstance as unknown as {
      set(key: string, value: unknown): void;
    };
    page.set('departmentId', 4);
    page.set('categoryId', 7);
    await settle(fixture);
    expect(report).toHaveBeenLastCalledWith(
      expect.objectContaining({ filters: { departmentId: 4, categoryId: 7 } }),
    );
    expect(summary).toHaveBeenLastCalledWith(expect.objectContaining({ categoryId: 7 }));
    expect(summary.mock.lastCall?.[0]).not.toHaveProperty('departmentId', 4);
    // Unfair Treatment in Engineering: #3 and #8.
    expect(el.querySelectorAll('tr.mat-mdc-row').length).toBe(2);
    expect(text(el)).not.toContain('Promotion review skipped');
  });

  it('prints the loaded report through the print service', async () => {
    configure();
    const printer = TestBed.inject(PrintService);
    const print = vi.spyOn(printer, 'print').mockImplementation(() => undefined);
    const { fixture, el } = await render(GrievanceReportsComponent);
    const button = el.querySelector(
      'button[aria-label="Print the grievance report"]',
    ) as HTMLButtonElement;
    button.click();
    await settle(fixture);
    expect(print).toHaveBeenCalledTimes(1);
    expect(el.querySelector('[data-testid="grievance-report-print"]')).not.toBeNull();
    printer.afterPrint$.next();
    await settle(fixture);
    expect(el.querySelector('[data-testid="grievance-report-print"]')).toBeNull();
  });
});

describe('grievance Excel export', () => {
  const request = (extra: Partial<ExportRequest> = {}): ExportRequest => ({
    moduleCode: 'grievance-cases',
    scope: 'filtered',
    filters: {},
    ...extra,
  });

  it('never has a sensitive column and is export only', () => {
    const keys = GRIEVANCE_CASES_EXPORT_COLUMNS.map((c) => c.key);
    expect(keys).toEqual([
      'caseNo',
      'category',
      'status',
      'priority',
      'department',
      'submittedOn',
      'targetResolutionDate',
      'resolvedOn',
      'resolutionDays',
      'isOverdue',
      'leadInvestigator',
      'isEscalated',
    ]);
    // The API module's headers and types (GrievanceExcelModule.cs).
    expect(GRIEVANCE_CASES_EXPORT_COLUMNS.map((c) => [c.header, c.type])).toEqual([
      ['Case No', 'text'],
      ['Category', 'text'],
      ['Status', 'text'],
      ['Priority', 'text'],
      ['Department', 'text'],
      ['Submitted', 'date'],
      ['Target', 'date'],
      ['Resolved', 'date'],
      ['Days', 'integer'],
      ['Overdue', 'boolean'],
      ['Lead Investigator', 'text'],
      ['Escalated', 'boolean'],
    ]);
    for (const banned of ['subject', 'description', 'notes', 'submitter', 'employee']) {
      expect(
        keys.some((k) => k.toLowerCase().includes(banned)),
        banned,
      ).toBe(false);
    }
    expect(GRIEVANCE_CASES_MODULE_INFO).toMatchObject({ canImport: false, canExport: true });
  });

  it('labels the anonymous department "Not disclosed", logs the export and is listed lazily', async () => {
    configure();
    const service = TestBed.inject(GrievanceInsightMockService);
    const rows = service.exportRows(request(), 100);
    expect(rows).toHaveLength(16);
    // The five anonymous sample cases.
    expect(rows.filter((r) => r['department'] === 'Not disclosed')).toHaveLength(5);
    expect(rows.every((r) => !('subject' in r) && !('description' in r))).toBe(true);
    const filtered = service.exportRows(request({ filters: { departmentId: 4 } }), 100);
    expect(filtered).toHaveLength(5);
    // Every export is written to the access log (with no case: it covers many).
    const exports = TestBed.inject(GrievanceMockStore)
      .audits()
      .filter((a) => a.action === 'Export');
    expect(exports).toHaveLength(2);
    expect(exports[0]).toMatchObject({
      caseId: null,
      actorUserId: 1,
      detail: 'module:grievance-cases;rows:16',
    });
    const modules = await firstValueFrom(
      TestBed.inject(ExcelModuleCatalogDataService).getModules(),
    );
    expect(modules.find((m) => m.code === 'grievance-cases')).toMatchObject({
      canImport: false,
      canExport: true,
    });
  });

  it('writes the API’s row values and applies the case-number search of a filtered export', () => {
    configure();
    const service = TestBed.inject(GrievanceInsightMockService);
    const all = service.exportRows(request(), 100);
    // Status and priority codes, real booleans, nulls left empty (as the API writes them).
    expect(all.every((r) => typeof r['isOverdue'] === 'boolean')).toBe(true);
    expect(all.every((r) => typeof r['isEscalated'] === 'boolean')).toBe(true);
    expect(all.map((r) => r['status'])).toContain('UnderInvestigation');
    // Newest submission first.
    const submitted = all.map((r) => String(r['submittedOn']));
    expect(submitted).toEqual([...submitted].sort().reverse());
    const caseNo = String(all[3]['caseNo']);
    const found = service.exportRows(request({ search: caseNo.slice(-5).toLowerCase() }), 100);
    expect(found.map((r) => r['caseNo'])).toEqual([caseNo]);
    // The "all" scope ignores the search, like the API.
    expect(service.exportRows(request({ scope: 'all', search: caseNo }), 100)).toHaveLength(16);
  });

  it('refuses the export without grievance-management:View', () => {
    configure(HR_OFFICER);
    expect(() => TestBed.inject(GrievanceInsightMockService).exportRows(request(), 100)).toThrow();
  });
});

describe('grievance employee options (triage picker)', () => {
  it('lists at most 20 current employees by name, for management Edit only', async () => {
    configure();
    const cases = TestBed.inject(GrievanceCaseDataService);
    const all = await firstValueFrom(cases.employeeOptions(''));
    expect(all.length).toBe(20);
    const daniel = await firstValueFrom(cases.employeeOptions('EMP005'));
    expect(daniel.map((o) => o.displayName)).toEqual(['Daniel Carter']);
    TestBed.resetTestingModule();
    configure(HR_OFFICER);
    await expect(
      firstValueFrom(TestBed.inject(GrievanceCaseDataService).employeeOptions('a')),
    ).rejects.toMatchObject({ code: 'Forbidden' });
  });
});

describe('grievance guides', () => {
  const routes = NAVIGATION.find((i) => i.label === 'Grievances')!.children!.map((c) => c.route!);

  it('describes every Grievances screen in the module guide and the screen details', () => {
    const guide = MODULE_GUIDE.find((m) => m.id === 'grievances')!;
    expect(guide.screens.map((s) => s.route).sort()).toEqual([...routes].sort());
    for (const route of routes) {
      expect(SCREEN_DETAILS[route]?.length, route).toBeGreaterThan(0);
    }
  });

  it('is honest about anonymity and the demo', () => {
    const guide = MODULE_GUIDE.find((m) => m.id === 'grievances')!;
    expect(guide.note?.en).toContain('enforce nothing');
    const article = JSON.stringify(guide.article);
    expect(article).toContain('What can still give someone away');
    expect(article).toContain('simulation');
  });

  it('walks the grievance workflow through existing screens', () => {
    const workflow = WORKFLOWS.find((w) => w.id === 'grievance')!;
    for (const step of workflow.steps) {
      expect(routes, step.title.en).toContain(step.route);
    }
  });
});
