import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { Route, Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes as APP_ROUTES } from '../../app.routes';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { NAVIGATION, visibleNavigation } from '../../core/constants/navigation';
import { permissionGuard } from '../../core/guards/permission.guard';
import { EvaluationPeriod, KpiCategory, RatingBand } from '../../core/models/kpi.models';
import { Company, Department } from '../../core/models/organization.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiTemplateDataService,
  RatingBandDataService,
} from '../../core/services/data/kpi-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import {
  ASSIGNMENT_COLUMNS,
  scoreText,
  weightIsOff,
} from './assignments/assignment-list.component';
import { KpiCategoriesPageComponent } from './categories/kpi-categories-page.component';
import { buildKpiCategoriesConfig } from './categories/kpi-categories.config';
import { PERFORMANCE_ROUTES } from './performance.routes';
import {
  buildEvaluationPeriodsConfig,
  canDeletePeriod,
  canEditPeriod,
  periodFormNotice,
} from './periods/evaluation-periods.config';
import { buildRatingBandsConfig } from './rating-bands/rating-bands.config';
import {
  buildKpiTemplatesConfig,
  directionOptions,
  measurementOptions,
  showsCap,
  templateRuleErrors,
} from './templates/kpi-templates.config';

const keys = (items: { key: string }[]) => items.map((i) => i.key);
const sortableKeys = (columns: { key: string; sortable?: boolean }[]) =>
  columns.filter((c) => c.sortable).map((c) => c.key);

/** The sort whitelists of the API (`api/README.md`, KPI sections). */
const API_SORT = {
  categories: ['id', 'code', 'name', 'status'],
  templates: [
    'id',
    'code',
    'name',
    'categoryId',
    'defaultWeight',
    'scoringMethod',
    'frequency',
    'status',
  ],
  ratingBands: ['id', 'label', 'minScore', 'status'],
  periods: ['id', 'name', 'companyId', 'periodType', 'startDate', 'endDate', 'status'],
  scorecards: [
    'id',
    'employeeCode',
    'employeeName',
    'periodName',
    'departmentName',
    'status',
    'overallScore',
    'planFinalizedOn',
    'modifiedAt',
  ],
};

const STATUSES: EvaluationPeriod['status'][] = ['Draft', 'Active', 'Completed', 'Closed'];

const period = (status: EvaluationPeriod['status']): EvaluationPeriod => ({
  id: 9,
  companyId: 1,
  name: '2027 Q1',
  periodType: 'Quarterly',
  startDate: '2027-01-01',
  endDate: '2027-03-31',
  submissionDueDate: null,
  requiresHrReview: false,
  status,
  rowVersion: '4',
});

describe('KPI categories config', () => {
  const config = buildKpiCategoriesConfig({} as KpiCategoryDataService);

  it('has the columns, a status filter and a code-or-name search', () => {
    expect(keys(config.columns)).toEqual(['code', 'name', 'description', 'status']);
    expect(config.filters?.map((f) => f.key)).toEqual(['status']);
    expect(config.filters?.[0].options.map((o) => o.value)).toEqual(['Active', 'Inactive']);
    expect(config.searchLabel).toContain('code or name');
    expect(config.columns.find((c) => c.key === 'status')?.type).toBe('status');
    expect(sortableKeys(config.columns).every((k) => API_SORT.categories.includes(k))).toBe(true);
  });

  it('activates and deactivates through the status, and locks the code after creation', () => {
    expect(config.hasStatus).not.toBe(false);
    const code = config.fields.find((f) => f.key === 'code');
    expect(code?.lockedOnEdit).toBe(true);
    expect(code?.maxLength).toBe(20);
    expect(code?.pattern).toBe('code');
    expect(config.fields.find((f) => f.key === 'name')?.maxLength).toBe(100);
    expect(config.newItem()).toEqual({ status: 'Active' });
  });
});

describe('Rating bands config', () => {
  const config = buildRatingBandsConfig({} as RatingBandDataService);

  it('sorts by minimum score, highest first, and lists the band fields', () => {
    expect(config.defaultSort).toEqual({ active: 'minScore', direction: 'desc' });
    expect(keys(config.columns)).toEqual(['minScore', 'label', 'description', 'status']);
    expect(keys(config.fields)).toEqual(['label', 'minScore', 'description', 'status']);
    expect(config.nameOf({ label: 'Exceeds' } as RatingBand)).toBe('Exceeds');
    expect(config.hasStatus).not.toBe(false);
    expect(sortableKeys(config.columns).every((k) => API_SORT.ratingBands.includes(k))).toBe(true);
  });

  it('keeps the minimum score between 0 and 500', () => {
    const minScore = config.fields.find((f) => f.key === 'minScore');
    expect([minScore?.min, minScore?.max, minScore?.required]).toEqual([0, 500, true]);
  });
});

describe('KPI templates config', () => {
  const categories = [
    { id: 1, name: 'Productivity', status: 'Active' },
    { id: 2, name: 'Old', status: 'Inactive' },
  ] as KpiCategory[];
  const departments = [{ id: 4, name: 'Engineering' }] as Department[];
  const config = buildKpiTemplatesConfig({
    service: {} as KpiTemplateDataService,
    categories,
    departments,
  });

  it('filters by category, department, scoring method and status', () => {
    expect(config.filters?.map((f) => f.key)).toEqual([
      'categoryId',
      'departmentId',
      'scoringMethod',
      'status',
    ]);
    expect(config.filters?.find((f) => f.key === 'scoringMethod')?.options).toHaveLength(5);
    expect(
      config.filters?.find((f) => f.key === 'categoryId')?.options.map((o) => o.label),
    ).toEqual(['Old', 'Productivity']);
  });

  it('lists the category, department and rule, sorting only by the API’s keys', () => {
    expect(keys(config.columns)).toEqual([
      'code',
      'name',
      'categoryId',
      'departmentId',
      'direction',
      'scoringMethod',
      'defaultTarget',
      'defaultWeight',
      'status',
    ]);
    const department = config.columns.find((c) => c.key === 'departmentId');
    expect(department?.value?.({ departmentId: null } as never)).toBe('All departments');
    expect(department?.value?.({ departmentId: 4 } as never)).toBe('Engineering');
    expect(
      config.columns.find((c) => c.key === 'categoryId')?.value?.({ categoryId: 1 } as never),
    ).toBe('Productivity');
    expect(sortableKeys(config.columns).every((k) => API_SORT.templates.includes(k))).toBe(true);
  });

  it('offers every field, with the department labelled as optional', () => {
    expect(keys(config.fields)).toEqual([
      'code',
      'name',
      'description',
      'categoryId',
      'departmentId',
      'scoringMethod',
      'measurementType',
      'direction',
      'unit',
      'frequency',
      'defaultTarget',
      'defaultWeight',
      'maxRating',
      'rangeMin',
      'rangeMax',
      'maxAchievementPct',
      'status',
    ]);
    const department = config.fields.find((f) => f.key === 'departmentId');
    expect(department?.label).toBe('Department-specific (optional)');
    expect(department?.nullable).toBe(true);
    expect(department?.required).toBeFalsy();
  });

  it('offers an inactive category only while the template already has it', () => {
    const field = config.fields.find((f) => f.key === 'categoryId')!;
    const ctx = { id: null };
    expect(field.options?.({ categoryId: null }, ctx).map((o) => o.label)).toEqual([
      'Productivity',
    ]);
    expect(field.options?.({ categoryId: 2 }, ctx).map((o) => o.label)).toEqual([
      'Old',
      'Productivity',
    ]);
  });

  it('limits the measurement types and directions to what the scoring method allows', () => {
    expect(measurementOptions('Rating Based').map((o) => o.value)).toEqual(['Rating']);
    expect(measurementOptions('Boolean').map((o) => o.value)).toEqual(['Boolean']);
    expect(measurementOptions('Capped Percentage').map((o) => o.value)).toEqual([
      'Number',
      'Percentage',
      'Currency',
      'Hours',
    ]);
    expect(directionOptions('Range Based').map((o) => o.value)).toEqual(['Range Based']);
    expect(directionOptions('Direct Percentage').map((o) => o.value)).toEqual([
      'Higher Is Better',
      'Lower Is Better',
      'Exact Target',
    ]);
  });

  it('shows the cap for everything but Direct Percentage, and always for Lower Is Better', () => {
    const cap = (scoringMethod: string, direction: string) =>
      showsCap({ scoringMethod, direction });
    expect(cap('Direct Percentage', 'Higher Is Better')).toBe(false);
    expect(cap('Capped Percentage', 'Higher Is Better')).toBe(true);
    expect(cap('Rating Based', 'Higher Is Better')).toBe(true);
    // Shown so its error ("a result of 0 scores the cap") sits on the field the server names.
    expect(cap('Direct Percentage', 'Lower Is Better')).toBe(true);
  });

  // The cases of KPI-05: the same function and messages as the API, per field.
  const rule = (over: Record<string, unknown>) =>
    templateRuleErrors({
      measurementType: 'Number',
      direction: 'Higher Is Better',
      scoringMethod: 'Capped Percentage',
      defaultTarget: 100,
      maxRating: null,
      rangeMin: null,
      rangeMax: null,
      maxAchievementPct: 120,
      ...over,
    });

  it('reports each inconsistent rule of KPI-05 on the field the server names', () => {
    expect(rule({ defaultTarget: 0 })['target']).toContain('greater than 0');
    expect(
      rule({ scoringMethod: 'Rating Based', measurementType: 'Rating', maxAchievementPct: null })[
        'maxRating'
      ],
    ).toContain('rating scale');
    expect(
      rule({
        scoringMethod: 'Range Based',
        direction: 'Range Based',
        rangeMin: 105,
        rangeMax: 95,
        maxAchievementPct: null,
      })['rangeMax'],
    ).toContain('greater than the range minimum');
    expect(rule({ maxAchievementPct: null })['maxAchievementPct']).toContain('needs a maximum');
    expect(rule({ scoringMethod: 'Boolean' })['scoringMethod']).toContain('Boolean');
    expect(
      rule({
        scoringMethod: 'Direct Percentage',
        direction: 'Lower Is Better',
        maxAchievementPct: null,
        defaultTarget: 5,
      })['maxAchievementPct'],
    ).toContain('a result of 0 scores it');
  });

  it('reports nothing for a consistent rule, nor before the three choices are made', () => {
    expect(rule({})).toEqual({});
    expect(templateRuleErrors({ scoringMethod: 'Boolean' })).toEqual({});
  });

  it('starts a new template as an active Quarterly Direct Percentage KPI for every department', () => {
    expect(config.newItem()).toMatchObject({
      departmentId: null,
      scoringMethod: 'Direct Percentage',
      direction: 'Higher Is Better',
      frequency: 'Quarterly',
      maxAchievementPct: null,
      status: 'Active',
    });
  });
});

describe('Evaluation periods config', () => {
  const companies = [{ id: 1, name: 'APEX Technologies', status: 'Active' }] as Company[];
  const run = vi.fn(() => of(period('Active')));
  const service = {
    activate: run,
    complete: run,
    close: run,
  } as unknown as EvaluationPeriodDataService;
  const config = buildEvaluationPeriodsConfig({ service, companies });

  it('lists the period with company, type, dates, due date, HR review and a status chip', () => {
    expect(keys(config.columns)).toEqual([
      'name',
      'companyId',
      'periodType',
      'startDate',
      'endDate',
      'submissionDueDate',
      'requiresHrReview',
      'status',
    ]);
    expect(config.columns.find((c) => c.key === 'status')?.type).toBe('status');
    expect(config.columns.find((c) => c.key === 'companyId')?.value?.(period('Draft'))).toBe(
      'APEX Technologies',
    );
    expect(config.filters?.map((f) => f.key)).toEqual(['companyId', 'periodType', 'status']);
    expect(sortableKeys(config.columns).every((k) => API_SORT.periods.includes(k))).toBe(true);
    // The status moves only through the commands, so no plain activate / deactivate.
    expect(config.hasStatus).toBe(false);
  });

  it('offers the company, type, dates, due date and the HR review toggle on the form', () => {
    const form = config.fields.filter((f) => !f.detailsOnly);
    expect(keys(form)).toEqual([
      'companyId',
      'name',
      'periodType',
      'startDate',
      'endDate',
      'submissionDueDate',
      'requiresHrReview',
    ]);
    expect(form.find((f) => f.key === 'requiresHrReview')).toMatchObject({
      type: 'toggle',
      label: 'Requires HR final review',
    });
  });

  it('allows editing in Draft and Active, deleting only in Draft', () => {
    expect(STATUSES.map((s) => canEditPeriod(period(s)))).toEqual([true, true, false, false]);
    expect(STATUSES.map((s) => canDeletePeriod(period(s)))).toEqual([true, false, false, false]);
    expect(STATUSES.map((s) => config.canEdit?.(period(s)))).toEqual([true, true, false, false]);
    expect(STATUSES.map((s) => config.canDelete?.(period(s)))).toEqual([true, false, false, false]);
  });

  it('locks all but the name and due date while Active, and everything once Completed', () => {
    const lockedFor = (status: string) =>
      config.fields.filter((f) => f.lockedWhen?.({ status })).map((f) => f.key);
    expect(lockedFor('Draft')).toEqual([]);
    expect(lockedFor('Active')).toEqual([
      'companyId',
      'periodType',
      'startDate',
      'endDate',
      'requiresHrReview',
    ]);
    const all = config.fields.filter((f) => !f.detailsOnly).map((f) => f.key);
    expect(lockedFor('Completed')).toEqual(all);
    expect(lockedFor('Closed')).toEqual(all);
  });

  it('explains the edit rule of the period’s status above the form', () => {
    expect(periodFormNotice({ status: 'Active' })).toContain(
      'only its name and submission due date',
    );
    expect(periodFormNotice({ status: 'Completed' })).toContain('read-only');
    expect(periodFormNotice({ status: 'Closed' })).toContain('read-only');
    expect(periodFormNotice({ status: 'Draft' })).toBeNull();
    expect(periodFormNotice(null)).toBeNull();
  });

  it('refuses an end date that is not after the start date, and an early due date', () => {
    const end = config.fields.find((f) => f.key === 'endDate')!;
    expect(end.rule?.({ startDate: '2027-01-01', endDate: '2026-12-31' })).toContain(
      'after the start',
    );
    expect(end.rule?.({ startDate: '2027-01-01', endDate: '2027-01-01' })).toContain(
      'after the start',
    );
    expect(end.rule?.({ startDate: '2027-01-01', endDate: '2027-03-31' })).toBeNull();
    const due = config.fields.find((f) => f.key === 'submissionDueDate')!;
    expect(due.rule?.({ startDate: '2027-01-01', submissionDueDate: '2026-12-31' })).toContain(
      'before the period starts',
    );
    // A due date after the end date is allowed.
    expect(due.rule?.({ startDate: '2027-01-01', submissionDueDate: '2027-04-14' })).toBeNull();
  });

  it('offers Activate for Draft, Complete for Active and Close for Completed, each confirmed', () => {
    const actions = config.extraActions ?? [];
    expect(actions.map((a) => a.id)).toEqual(['activate', 'complete', 'close']);
    const visibleFor = (status: EvaluationPeriod['status']) =>
      actions.filter((a) => a.visible?.(period(status))).map((a) => a.id);
    expect(visibleFor('Draft')).toEqual(['activate']);
    expect(visibleFor('Active')).toEqual(['complete']);
    expect(visibleFor('Completed')).toEqual(['close']);
    expect(visibleFor('Closed')).toEqual([]);
    expect(actions.every((a) => !!a.confirm && (a.permission ?? 'Edit') === 'Edit')).toBe(true);

    const message = (id: string, status: EvaluationPeriod['status']) =>
      actions.find((a) => a.id === id)!.confirm!.message(period(status));
    expect(message('activate', 'Draft')).toContain('can no longer be changed');
    expect(message('complete', 'Active')).toContain('can no longer be entered');
    const close = actions.find((a) => a.id === 'close')!;
    expect(close.destructive).toBe(true);
    expect(message('close', 'Completed')).toContain('becomes final');
  });

  it('runs a command with the period’s own rowVersion', () => {
    const complete = config.extraActions!.find((a) => a.id === 'complete')!;
    complete.run(period('Active'), '').subscribe();
    expect(run).toHaveBeenCalledWith(9, '4');
  });
});

describe('KPI assignments list columns', () => {
  it('shows code, name, department, period, status, KPIs, weight, score and reviewer', () => {
    expect(keys(ASSIGNMENT_COLUMNS)).toEqual([
      'employeeCode',
      'employeeName',
      'departmentName',
      'periodName',
      'status',
      'itemCount',
      'totalWeight',
      'overallScore',
      'reviewerName',
    ]);
    expect(ASSIGNMENT_COLUMNS.find((c) => c.key === 'status')?.type).toBe('status');
    expect(sortableKeys(ASSIGNMENT_COLUMNS).every((k) => API_SORT.scorecards.includes(k))).toBe(
      true,
    );
  });

  it('highlights a total weight that is not exactly 100, and formats the score', () => {
    expect([60, 99.99, 100, 100.01].map((totalWeight) => weightIsOff({ totalWeight }))).toEqual([
      true,
      true,
      false,
      true,
    ]);
    expect(scoreText({ overallScore: null, ratingLabel: null })).toBeNull();
    expect(scoreText({ overallScore: 80.875, ratingLabel: 'Exceeds Expectations' })).toBe(
      '80.88 · Exceeds Expectations',
    );
  });
});

describe('Performance routes and navigation', () => {
  const setupScreens = ['assignments', 'templates', 'categories', 'periods', 'rating-bands'];

  it('guards every setup screen with kpi-setup:View', () => {
    for (const path of setupScreens) {
      const route = PERFORMANCE_ROUTES.find((r) => r.path === path) as Route;
      expect(route.data?.['permission']).toBe('kpi-setup:View');
      expect(route.canActivate).toContain(permissionGuard);
      expect(route.title ?? route.children?.[0].title).toBeTruthy();
    }
  });

  it('lazily loads the module under /performance without a module-wide key', () => {
    const shell = APP_ROUTES.find((r) => r.path === '')!;
    const performance = shell.children!.find((r) => r.path === 'performance')!;
    expect(performance.loadChildren).toBeDefined();
    // Each screen names its key, so My KPIs (kpi:View) can join later without kpi-setup.
    expect(performance.data?.['permission']).toBeUndefined();
  });

  it('shows the setup entries only to a role that holds kpi-setup:View', () => {
    const labels = (held: string[]) =>
      visibleNavigation(NAVIGATION, (k) => held.includes(k)).flatMap((i) => [
        i.label,
        ...(i.children ?? []).map((c) => c.label),
      ]);
    const group = NAVIGATION.find((i) => i.label === 'Performance')!;
    expect(group.permission).toBe('kpi-setup:View');
    expect(group.children?.map((c) => c.label)).toEqual([
      'My KPIs',
      'Reviews',
      'Assignments',
      'Templates',
      'Categories',
      'Evaluation Periods',
      'Rating Bands',
    ]);
    expect(group.children?.slice(2).map((c) => c.route)).toEqual(
      setupScreens.map((s) => `/performance/${s}`),
    );
    expect(labels(['kpi-setup:View'])).toContain('Performance');
    expect(labels(['kpi-setup:View'])).toContain('Evaluation Periods');
    expect(labels(['kpi-setup:View'])).not.toContain('My KPIs');
    // The starter Employee role (kpi:View and kpi:Create): the group with My KPIs only.
    const employee = labels(['kpi:View', 'kpi:Create', 'dashboard:View']);
    expect(employee).toContain('Performance');
    expect(employee).toContain('My KPIs');
    expect(employee).not.toContain('Reviews');
    expect(employee).not.toContain('Assignments');
    expect(employee).not.toContain('Evaluation Periods');
  });
});

describe('Performance setup permission gating', () => {
  function user(permissions?: string[]): CurrentUser {
    return {
      id: 1,
      employeeId: null,
      displayName: 'Test User',
      email: 'test@example.test',
      jobTitle: '',
      roles: ['Tester'],
      permissions,
    };
  }

  async function open(permissions?: string[]) {
    TestBed.configureTestingModule({
      imports: [MatDialogModule],
      providers: [
        provideRouter([
          {
            path: 'performance',
            children: [
              {
                path: 'categories',
                data: { permission: 'kpi-setup:View' },
                canActivate: [permissionGuard],
                component: KpiCategoriesPageComponent,
              },
            ],
          },
          { path: 'forbidden', children: [] },
        ]),
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
        { provide: CurrentUserService, useValue: { current: signal(user(permissions)) } },
      ],
    });
    const harness = await RouterTestingHarness.create('/performance/categories');
    for (let i = 0; i < 6; i++) {
      await harness.fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, 15));
      harness.detectChanges();
    }
    return harness;
  }

  function menuLabels(harness: RouterTestingHarness): string[] {
    const menu = harness.fixture.debugElement.query(By.css('button[aria-label^="Actions for"]'));
    menu.nativeElement.click();
    harness.detectChanges();
    const labels = Array.from(document.querySelectorAll('[role="menuitem"]')).map(
      (e) => e.textContent?.trim() ?? '',
    );
    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    return labels;
  }

  it('shows Add and the row actions to a role that holds every key', async () => {
    const harness = await open([
      'kpi-setup:View',
      'kpi-setup:Create',
      'kpi-setup:Edit',
      'kpi-setup:Delete',
    ]);
    const text = (harness.routeNativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Add category');
    const labels = menuLabels(harness);
    expect(labels.some((l) => l.includes('Edit'))).toBe(true);
    expect(labels.some((l) => l.includes('Deactivate'))).toBe(true);
    expect(labels.some((l) => l.includes('Delete'))).toBe(true);
  });

  it('hides Add, Edit, Deactivate and Delete from a role that can only view', async () => {
    const harness = await open(['kpi-setup:View']);
    const text = (harness.routeNativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Add category');
    const labels = menuLabels(harness);
    expect(labels.some((l) => l.includes('View details'))).toBe(true);
    expect(labels.some((l) => l.includes('Edit'))).toBe(false);
    expect(labels.some((l) => l.includes('Delete'))).toBe(false);
    expect(labels.some((l) => l.includes('Deactivate'))).toBe(false);
  });

  it('sends a role without kpi-setup:View to /forbidden', async () => {
    await open(['kpi:View', 'kpi:Create']);
    expect(TestBed.inject(Router).url).toBe('/forbidden');
  });
});
