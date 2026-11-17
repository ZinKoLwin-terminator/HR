import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSelectChange } from '@angular/material/select';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { AppError } from '../../../core/models/common.models';
import { EmployeeLookupItem } from '../../../core/models/employee.models';
import {
  AssignKpiResult,
  BulkAssignmentResult,
  KpiTemplate,
  ScorecardDetail,
  ScorecardItem,
} from '../../../core/models/kpi.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import { HR_ADMIN } from '../../../mock-services/kpi/kpi-test-support';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import {
  AssignmentRow,
  assignmentMessage,
  templateFitsDepartment,
  validateAssignmentRows,
} from '../shared/assignment-rows';
import { KpiAssignmentRowsComponent } from '../shared/kpi-assignment-rows.component';
import { AssignKpisDialogComponent } from './assign-kpis-dialog.component';
import { BulkAssignDialogComponent, buildBulkRequest } from './bulk-assign-dialog.component';
import {
  PlanDetailComponent,
  canFinalizePlan,
  itemFields,
  itemUpdateFrom,
} from './plan-detail.component';

// Seed facts (mock-data/kpi): template 1 OUTPUT-TASKS (target 100, weight 30), 2 DEFECT-RATE
// (5, 20), 8 ENG-REVIEW-HOURS (24, 30, Engineering only). Scorecard 1 is EMP046's Submitted card
// in the Active period 2; scorecard 3 is EMP019 (Engineering, manager Daniel Carter) in Planning
// with templates 8 and 1 at 30 each (60%).

const ENGINEER: EmployeeLookupItem = {
  id: 46,
  employeeCode: 'EMP046',
  displayName: 'Hein Thant Zin',
  jobTitle: 'Software Engineer',
  departmentName: 'Engineering',
};
const IT_PERSON: EmployeeLookupItem = { ...ENGINEER, id: 56, departmentName: 'IT Infrastructure' };

async function settle(fixture: ComponentFixture<unknown>, rounds = 6) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

/** Protected members of a component, for driving it like the template does. */
const internals = <T>(fixture: ComponentFixture<unknown>) =>
  fixture.componentInstance as unknown as T;

function configure(extra: unknown[] = []) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      // Su Myat Noe (HR): the mock scopes scorecards by the signed-in person.
      { provide: CurrentUserService, useValue: { current: () => HR_ADMIN } },
      ...(extra as never[]),
    ],
  });
}

async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(component);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

describe('assignment rows (rules shared by the single and bulk dialogs)', () => {
  const template = (over: Partial<KpiTemplate>): KpiTemplate =>
    ({
      id: 1,
      measurementType: 'Number',
      direction: 'Higher Is Better',
      scoringMethod: 'Direct Percentage',
      rangeMin: null,
      rangeMax: null,
      maxRating: null,
      maxAchievementPct: null,
      departmentId: null,
      ...over,
    }) as KpiTemplate;
  const rows = (...pairs: [number | null, number | null][]): AssignmentRow[] =>
    pairs.map(([target, weight], i) => ({ templateId: i + 1, target, weight }));

  it('needs at least one KPI and refuses weights above 100% in total', () => {
    expect(validateAssignmentRows([], [])).toEqual({ items: 'Choose at least one KPI to assign.' });
    expect(validateAssignmentRows(rows([100, 60], [5, 40]), [])).toEqual({});
    expect(validateAssignmentRows(rows([100, 60], [5, 40.01]), [])['items']).toContain(
      'more than 100%',
    );
    // What the scorecard already holds counts too.
    expect(validateAssignmentRows(rows([100, 41]), [], 60)['items']).toContain('more than 100%');
  });

  it('puts a blank, negative or zero value on its row, with the server’s keys', () => {
    const errors = validateAssignmentRows(rows([null, 10], [-1, null], [5, 0], [1, 10.555]), []);
    expect(errors).toMatchObject({
      'items[0].target': 'Enter a target.',
      'items[1].target': 'The target cannot be negative.',
      'items[1].weight': 'Enter a weight.',
      'items[2].weight': 'The weight must be greater than 0 and at most 100.',
      'items[3].weight': 'Use at most 2 decimal places.',
    });
  });

  it('checks a changed target against the template’s rule (Higher Is Better needs above 0)', () => {
    const errors = validateAssignmentRows(rows([0, 50]), [template({ id: 1 })]);
    expect(errors['items[0].target']).toContain('greater than 0');
    const lower = template({
      id: 1,
      direction: 'Lower Is Better',
      scoringMethod: 'Capped Percentage',
      maxAchievementPct: 120,
    });
    expect(validateAssignmentRows(rows([0, 50]), [lower])).toEqual({});
  });

  it('offers a department-specific template only for that department', () => {
    const names = new Map([[4, 'Engineering']]);
    const general = template({ departmentId: null });
    const eng = template({ departmentId: 4 });
    expect(templateFitsDepartment(general, null, names)).toBe(true);
    expect(templateFitsDepartment(eng, null, names)).toBe(false);
    expect(templateFitsDepartment(eng, 'Engineering', names)).toBe(true);
    expect(templateFitsDepartment(eng, 'IT Infrastructure', names)).toBe(false);
  });

  it('says "Scorecard created" for a 201 and "KPIs added" for a 200', () => {
    const scorecard = { employeeCode: 'EMP046', periodName: '2027 Q1', itemCount: 3 };
    const result = (created: boolean) =>
      ({ created, scorecard: scorecard as ScorecardDetail }) as AssignKpiResult;
    expect(assignmentMessage(result(true))).toBe(
      'Scorecard created for EMP046 (2027 Q1) with 3 KPIs.',
    );
    expect(assignmentMessage(result(false))).toBe(
      'KPIs added to the scorecard of EMP046 (2027 Q1).',
    );
  });

  it('sends either the hand-picked ids or one org filter, never both', () => {
    const items = rows([100, 100]);
    expect(buildBulkRequest(2, 'department', 4, [9], items)).toEqual({
      periodId: 2,
      departmentId: 4,
      items: [{ templateId: 1, target: 100, weight: 100 }],
    });
    expect(buildBulkRequest(2, 'jobGrade', 3, [], items)).toMatchObject({ jobGradeId: 3 });
    expect(buildBulkRequest(2, 'employees', 4, [46, 17], items)).toEqual({
      periodId: 2,
      employeeIds: [46, 17],
      items: [{ templateId: 1, target: 100, weight: 100 }],
    });
    expect(buildBulkRequest(2, 'branch', null, [], items)).toBeNull();
    expect(buildBulkRequest(2, 'employees', null, [], items)).toBeNull();
  });
});

describe('Assign KPIs dialog', () => {
  const close = vi.fn();

  interface DialogInternals {
    periodId: { set(v: number | null): void };
    employee: { set(v: EmployeeLookupItem | null): void };
    rows: () => AssignmentRow[];
    fittingTemplates: () => KpiTemplate[];
    submit(): void;
  }

  async function open(data: { scorecard?: ScorecardDetail } | null = null) {
    close.mockReset();
    configure([
      { provide: MatDialogRef, useValue: { close } },
      { provide: MAT_DIALOG_DATA, useValue: data },
    ]);
    const { fixture, el } = await render(AssignKpisDialogComponent);
    const dialog = internals<DialogInternals>(fixture);
    const rowsComponent = () =>
      fixture.debugElement.query(By.directive(KpiAssignmentRowsComponent))
        .componentInstance as unknown as { add(event: MatSelectChange): void };
    const addKpi = async (templateId: number) => {
      rowsComponent().add({ value: templateId, source: { value: null } } as MatSelectChange);
      await settle(fixture, 2);
    };
    const typeIn = async (label: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await settle(fixture, 2);
    };
    const total = () => el.querySelector('[data-testid="weight-total"]')?.textContent ?? '';
    return { fixture, el, dialog, addKpi, typeIn, total };
  }

  it('offers department-specific templates only for a matching employee', async () => {
    const { fixture, dialog } = await open();
    const codes = () => dialog.fittingTemplates().map((t) => t.code);
    expect(codes()).not.toContain('ENG-REVIEW-HOURS');
    dialog.employee.set(ENGINEER);
    await settle(fixture, 2);
    expect(codes()).toContain('ENG-REVIEW-HOURS');
    dialog.employee.set(IT_PERSON);
    await settle(fixture, 2);
    expect(codes()).not.toContain('ENG-REVIEW-HOURS');
    expect(codes()).toContain('OUTPUT-TASKS');
  });

  it('drops a chosen KPI that no longer fits when the employee changes', async () => {
    const { fixture, dialog, addKpi } = await open();
    dialog.employee.set(ENGINEER);
    await settle(fixture, 2);
    await addKpi(8);
    await addKpi(1);
    expect(dialog.rows().map((r) => r.templateId)).toEqual([8, 1]);
    dialog.employee.set(IT_PERSON);
    await settle(fixture, 2);
    expect(dialog.rows().map((r) => r.templateId)).toEqual([1]);
  });

  it('prefills target and weight from the template and keeps a running total', async () => {
    const { el, dialog, addKpi, typeIn, total } = await open();
    await addKpi(1);
    await addKpi(2);
    expect(dialog.rows()).toEqual([
      { templateId: 1, target: 100, weight: 30 },
      { templateId: 2, target: 5, weight: 20 },
    ]);
    expect(
      el.querySelector<HTMLInputElement>('input[aria-label="Target for OUTPUT-TASKS"]')!.value,
    ).toBe('100');
    expect(total()).toContain('Total weight: 50%');
    await typeIn('Weight for OUTPUT-TASKS', '80');
    expect(dialog.rows()[0].weight).toBe(80);
    expect(total()).toContain('Total weight: 100%');
    await typeIn('Weight for DEFECT-RATE', '20.01');
    expect(total()).toContain('100.01%');
    expect(total()).toContain('cannot add up to more than 100%');
  });

  it('does not call the server while the total is above 100% or a value is missing', async () => {
    const { fixture, el, dialog, addKpi, typeIn } = await open();
    const assign = vi.spyOn(TestBed.inject(ScorecardDataService), 'assign');
    dialog.periodId.set(2);
    dialog.employee.set(ENGINEER);
    await settle(fixture, 2);
    await addKpi(1);
    await typeIn('Weight for OUTPUT-TASKS', '');
    dialog.submit();
    await settle(fixture, 2);
    expect(assign).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Enter a weight.');
    expect(
      el.querySelector('input[aria-label="Weight for OUTPUT-TASKS"]')?.getAttribute('aria-invalid'),
    ).toBe('true');
  });

  it('shows the server’s row errors on their rows and clears them on the next edit', async () => {
    const { fixture, el, dialog, addKpi, typeIn } = await open();
    const error: AppError = {
      code: 'Validation',
      message: 'Please correct the highlighted fields.',
      fieldErrors: {
        'items[1].weight': 'The server refuses this weight.',
        'items[0].templateId': 'This template is for another department.',
      },
    };
    vi.spyOn(TestBed.inject(ScorecardDataService), 'assign').mockReturnValue(
      throwError(() => error),
    );
    dialog.periodId.set(3);
    dialog.employee.set(ENGINEER);
    await settle(fixture, 2);
    await addKpi(1);
    await addKpi(2);
    dialog.submit();
    await settle(fixture, 2);
    const rowsEl = el.querySelectorAll('tbody tr');
    expect(rowsEl[1].textContent).toContain('The server refuses this weight.');
    expect(rowsEl[0].textContent).toContain('KPI: This template is for another department.');
    expect(rowsEl[0].textContent).not.toContain('The server refuses this weight.');
    expect(
      el.querySelector('input[aria-label="Weight for DEFECT-RATE"]')?.getAttribute('aria-invalid'),
    ).toBe('true');
    await typeIn('Weight for DEFECT-RATE', '15');
    expect(el.textContent).not.toContain('The server refuses this weight.');
  });

  it('shows a conflict (already assigned) above the form and stays open', async () => {
    const { fixture, el, dialog, addKpi } = await open();
    // EMP019's plan of the Active period already holds OUTPUT-TASKS (KPI-14).
    dialog.periodId.set(2);
    dialog.employee.set({ ...ENGINEER, id: 19, employeeCode: 'EMP019' });
    await settle(fixture, 2);
    await addKpi(1);
    dialog.submit();
    await settle(fixture);
    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'KPI OUTPUT-TASKS is already assigned to the scorecard of EMP019.',
    );
    expect(close).not.toHaveBeenCalled();
  });

  it('creates a scorecard and closes with the 201-style result', async () => {
    const { fixture, dialog, addKpi } = await open();
    dialog.periodId.set(3);
    dialog.employee.set(ENGINEER);
    await settle(fixture, 2);
    await addKpi(8);
    await addKpi(1);
    dialog.submit();
    await settle(fixture);
    expect(close).toHaveBeenCalledTimes(1);
    const result = close.mock.calls[0][0] as AssignKpiResult;
    expect(result.created).toBe(true);
    expect(result.scorecard.items.map((i) => [i.kpiCode, i.target, i.weight])).toEqual([
      ['ENG-REVIEW-HOURS', 24, 30],
      ['OUTPUT-TASKS', 100, 30],
    ]);
    expect(assignmentMessage(result)).toContain('Scorecard created');
  });

  it('adds KPIs to an existing plan: fixed employee, held KPIs not offered, weight counted', async () => {
    configure();
    const card = await firstValueFrom(TestBed.inject(ScorecardDataService).getById(3));
    TestBed.resetTestingModule();
    const { fixture, el, dialog, addKpi } = await open({ scorecard: card });
    expect(el.textContent).toContain('EMP019');
    expect(el.querySelector('h2')?.textContent).toContain('Add KPIs to the plan');
    const offered = fixture.debugElement.query(By.directive(KpiAssignmentRowsComponent))
      .componentInstance as unknown as { available: () => KpiTemplate[] };
    expect(offered.available().map((t) => t.id)).not.toContain(8);
    expect(offered.available().map((t) => t.id)).not.toContain(1);
    await addKpi(2);
    expect(el.querySelector('[data-testid="weight-total"]')?.textContent).toContain(
      'Total weight: 80%',
    );
    dialog.submit();
    await settle(fixture);
    const result = close.mock.calls[0][0] as AssignKpiResult;
    expect(result.created).toBe(false);
    expect(assignmentMessage(result)).toContain('KPIs added');
  });
});

describe('Bulk assign dialog', () => {
  const result: BulkAssignmentResult = {
    assigned: [
      { employeeId: 3, employeeCode: 'EMP003', scorecardId: 20, itemsAdded: 2 },
      { employeeId: 19, employeeCode: 'EMP019', scorecardId: 3, itemsAdded: 1 },
    ],
    skipped: [
      { employeeId: 46, employeeCode: 'EMP046', reason: 'The scorecard is Submitted.' },
      { employeeId: 999, employeeCode: null, reason: 'Employee not found.' },
    ],
  };

  interface BulkInternals {
    periodId: { set(v: number | null): void };
    filterId: { set(v: number | null): void };
    rows: { set(v: AssignmentRow[]): void };
    submit(): void;
  }

  it('lists the assigned employees with their KPI count and the skipped ones with the reason', async () => {
    configure([{ provide: MatDialogRef, useValue: { close: vi.fn() } }]);
    const bulk = vi
      .spyOn(TestBed.inject(ScorecardDataService), 'bulkAssign')
      .mockReturnValue(of(result));
    const { fixture, el } = await render(BulkAssignDialogComponent);
    const dialog = internals<BulkInternals>(fixture);
    dialog.periodId.set(2);
    dialog.filterId.set(4);
    dialog.rows.set([{ templateId: 6, target: 1, weight: 10 }]);
    await settle(fixture, 2);
    dialog.submit();
    await settle(fixture);

    expect(bulk).toHaveBeenCalledWith({
      periodId: 2,
      departmentId: 4,
      items: [{ templateId: 6, target: 1, weight: 10 }],
    });
    expect(el.querySelector('[data-testid="bulk-summary"]')?.textContent).toMatch(
      /2 assigned\s*,\s*2 skipped/,
    );
    const assigned = el.querySelector('[aria-label="Assigned employees"] tbody')!;
    expect(assigned.textContent).toContain('EMP003');
    expect(assigned.querySelectorAll('tr')[0].textContent).toContain('2');
    const skipped = el.querySelector('[aria-label="Skipped employees"] tbody')!;
    expect(skipped.querySelectorAll('tr')).toHaveLength(2);
    expect(skipped.textContent).toContain('EMP046');
    expect(skipped.textContent).toContain('The scorecard is Submitted.');
    expect(skipped.textContent).toContain('#999');
    expect(el.textContent).toContain('Done');
  });

  it('needs a period, a selection and KPIs before calling the server', async () => {
    configure([{ provide: MatDialogRef, useValue: { close: vi.fn() } }]);
    const bulk = vi.spyOn(TestBed.inject(ScorecardDataService), 'bulkAssign');
    const { fixture, el } = await render(BulkAssignDialogComponent);
    internals<BulkInternals>(fixture).submit();
    await settle(fixture, 2);
    expect(bulk).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Choose an evaluation period.');
    expect(el.textContent).toContain('Choose at least one KPI to assign.');
  });
});

describe('Plan detail', () => {
  const confirm = vi.fn(() => of(true));

  function setup() {
    confirm.mockClear();
    configure([{ provide: ConfirmDialogService, useValue: { confirm } }]);
  }
  const button = (el: HTMLElement, text: string) =>
    Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes(text));

  it('enables Finalize plan only when the weights add up to exactly 100%', () => {
    const items = (...weights: number[]) => weights.map((weight) => ({ weight }));
    expect(canFinalizePlan(items())).toBe(false);
    expect(canFinalizePlan(items(40, 35))).toBe(false);
    expect(canFinalizePlan(items(40, 35, 24.99))).toBe(false);
    expect(canFinalizePlan(items(40, 35, 25))).toBe(true);
    expect(canFinalizePlan(items(40, 35, 25.01))).toBe(false);
    expect(canFinalizePlan(items(33.33, 33.33, 33.34))).toBe(true);
  });

  it('shows a Planning plan at 60% with Finalize disabled and the editing actions', async () => {
    setup();
    const { el } = await render(PlanDetailComponent, { id: '3' });
    expect(el.querySelector('h1')?.textContent).toContain('EMP019');
    expect(el.querySelector('[data-testid="plan-total"]')?.textContent).toContain('60%');
    expect(button(el, 'Finalize plan')?.disabled).toBe(true);
    expect(el.textContent).toContain('They add up to 60% now.');
    expect(button(el, 'Add KPIs')).toBeTruthy();
    expect(button(el, 'Delete scorecard')).toBeTruthy();
    expect(el.querySelector('button[aria-label="Edit OUTPUT-TASKS"]')).toBeTruthy();
    expect(el.querySelector('button[aria-label="Remove OUTPUT-TASKS"]')).toBeTruthy();
    // The reviewer-to-be is the current manager.
    expect(el.querySelector('[data-testid="plan-reviewer"]')?.textContent).toContain(
      'Daniel Carter',
    );
  });

  it('finalizes at 100%, naming the reviewer, with the latest rowVersion, then turns read-only', async () => {
    setup();
    const cards = TestBed.inject(ScorecardDataService);
    await firstValueFrom(
      cards.assign({ employeeId: 19, periodId: 2, items: [{ templateId: 2, weight: 40 }] }),
    );
    const finalize = vi.spyOn(cards, 'finalizePlan');
    const { fixture, el } = await render(PlanDetailComponent, { id: '3' });
    const card = await firstValueFrom(cards.getById(3));
    const finalizeButton = button(el, 'Finalize plan')!;
    expect(finalizeButton.disabled).toBe(false);
    finalizeButton.click();
    await settle(fixture);

    const [{ message }] = confirm.mock.calls[0] as unknown as [{ message: string }];
    expect(message).toContain('The reviewer will be Daniel Carter');
    expect(finalize).toHaveBeenCalledWith(3, card.rowVersion);
    expect(el.textContent).toContain('This scorecard is Draft, so its plan is read-only.');
    expect(button(el, 'Finalize plan')).toBeUndefined();
    expect(el.querySelector('button[aria-label="Edit OUTPUT-TASKS"]')).toBeNull();
    expect(el.querySelector('[data-testid="plan-reviewer"]')?.textContent).toContain(
      'Daniel Carter',
    );
    expect(el.textContent).toContain('Finalize Plan');
  });

  it('is read-only after Planning, with a status banner and the review history', async () => {
    setup();
    const { el } = await render(PlanDetailComponent, { id: '1' });
    expect(el.textContent).toContain('This scorecard is Submitted, so its plan is read-only.');
    expect(button(el, 'Finalize plan')).toBeUndefined();
    expect(button(el, 'Add KPIs')).toBeUndefined();
    expect(button(el, 'Delete scorecard')).toBeUndefined();
    expect(el.querySelector('button[aria-label^="Edit "]')).toBeNull();
    expect(el.querySelector('[aria-label="Review history"]')?.textContent).toContain('Submit');
  });

  it('shows the server’s conflict message with a Reload button', async () => {
    setup();
    const cards = TestBed.inject(ScorecardDataService);
    await firstValueFrom(
      cards.assign({ employeeId: 19, periodId: 2, items: [{ templateId: 2, weight: 40 }] }),
    );
    const stale: AppError = {
      code: 'Conflict',
      message: 'The record changed. Reload and try again.',
    };
    vi.spyOn(cards, 'finalizePlan').mockReturnValue(throwError(() => stale));
    const { fixture, el } = await render(PlanDetailComponent, { id: '3' });
    button(el, 'Finalize plan')!.click();
    await settle(fixture);
    const banner = el.querySelector('.banner--error')!;
    expect(banner.textContent).toContain('The record changed. Reload and try again.');
    const reload = vi.spyOn(cards, 'getById');
    (banner.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);
    expect(reload).toHaveBeenCalledWith(3);
    expect(el.querySelector('.banner--error')).toBeNull();
  });

  it('sends an item edit with the item’s own rowVersion and caps the weight at what is left', () => {
    const item = { id: 7, unit: 'tasks', rowVersion: '5' } as ScorecardItem;
    expect(
      itemUpdateFrom(
        {
          target: '120',
          weight: '40',
          startDate: '2026-10-01',
          endDate: '2026-12-31',
          sortOrder: '',
        },
        item,
      ),
    ).toEqual({
      target: 120,
      weight: 40,
      startDate: '2026-10-01',
      endDate: '2026-12-31',
      sortOrder: undefined,
      rowVersion: '5',
    });
    const weight = itemFields(item, 70).find((f) => f.key === 'weight')!;
    expect(weight.hint).toContain('At most 30%');
    expect(weight.rule?.({ weight: 30 })).toBeNull();
    expect(weight.rule?.({ weight: 30.01 })).toContain('100.01%');
  });
});
