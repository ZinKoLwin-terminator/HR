import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError, Entity } from '../../core/models/common.models';
import { EvaluationPeriod, KpiCategory } from '../../core/models/kpi.models';
import { Company, Department } from '../../core/models/organization.models';
import {
  EvaluationPeriodDataService,
  KpiTemplateDataService,
} from '../../core/services/data/kpi-data.services';
import {
  CrudPageConfig,
  EntityFormDialogData,
  FormValues,
} from '../../shared/components/crud/crud.models';
import { CrudPageComponent } from '../../shared/components/crud/crud-page.component';
import { EntityFormDialogComponent } from '../../shared/components/crud/entity-form-dialog.component';
import { FieldFormGroup } from '../../shared/forms/form-builder';
import { buildEvaluationPeriodsConfig } from './periods/evaluation-periods.config';
import { buildKpiTemplatesConfig } from './templates/kpi-templates.config';

interface Harness {
  fixture: ComponentFixture<EntityFormDialogComponent>;
  el: HTMLElement;
  form: FieldFormGroup;
  save: ReturnType<typeof vi.fn>;
  submit: () => void;
}

type Save = (values: FormValues) => Observable<Entity>;

/**
 * Opens the entity form dialog the way the CRUD page does (details-only fields left out, the
 * form note from the config), without a page around it.
 */
async function openForm<T extends Entity>(
  config: CrudPageConfig<T>,
  value: FormValues,
  id: number | null,
  save: Save = () => of({ id: 1 }),
): Promise<Harness> {
  const saveSpy = vi.fn(save);
  const data: EntityFormDialogData = {
    title: id === null ? 'Add' : 'Edit',
    fields: config.fields.filter((f) => !f.detailsOnly && !(id !== null && f.createOnly)),
    value,
    id,
    submitLabel: 'Save',
    notice: config.formNotice?.(id === null ? null : value) ?? null,
    save: saveSpy,
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: { close: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(EntityFormDialogComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  const instance = fixture.componentInstance as unknown as { form: FieldFormGroup; submit(): void };
  return {
    fixture,
    el: fixture.nativeElement as HTMLElement,
    form: instance.form,
    save: saveSpy,
    submit: () => {
      instance.submit();
      fixture.detectChanges();
    },
  };
}

async function change(h: Harness, key: string, value: unknown) {
  h.form.controls[key].setValue(value);
  h.fixture.detectChanges();
  await h.fixture.whenStable();
  h.fixture.detectChanges();
}

const labelsOf = (el: HTMLElement) =>
  Array.from(el.querySelectorAll('mat-label')).map((l) => l.textContent?.trim());
const shows = (h: Harness, label: string) => labelsOf(h.el).includes(label);
const ruleError = (h: Harness, key: string) =>
  h.form.controls[key].errors?.['rule'] as string | undefined;

describe('KPI template form: adapts to the scoring method', () => {
  const categories = [{ id: 1, name: 'Productivity', status: 'Active' }] as KpiCategory[];
  const departments = [{ id: 4, name: 'Engineering' }] as Department[];
  const config = buildKpiTemplatesConfig({
    service: {} as KpiTemplateDataService,
    categories,
    departments,
  });
  const valid = () => ({ ...config.newItem(), code: 'TST-1', name: 'Test', categoryId: 1 });
  const open = (values: FormValues = config.newItem(), save?: Save) =>
    openForm(config, values, null, save);

  it('starts as Direct Percentage: no cap, no rating or range fields', async () => {
    const h = await open();
    expect(shows(h, 'Maximum achievement (%)')).toBe(false);
    expect(shows(h, 'Maximum rating')).toBe(false);
    expect(shows(h, 'Range minimum')).toBe(false);
    expect(shows(h, 'Department-specific (optional)')).toBe(true);
  });

  it('Rating Based: asks for the maximum rating and fixes the type to Rating', async () => {
    const h = await open();
    await change(h, 'scoringMethod', 'Rating Based');
    expect(shows(h, 'Maximum rating')).toBe(true);
    expect(h.form.controls['measurementType'].value).toBe('Rating');
    expect(h.form.controls['measurementType'].disabled).toBe(true);
    expect(ruleError(h, 'maxRating')).toContain('greater than 0');
    // Leaving Rating Based hides the field again, clears it and lets the type be chosen.
    await change(h, 'maxRating', 5);
    expect(ruleError(h, 'maxRating')).toBeUndefined();
    await change(h, 'scoringMethod', 'Capped Percentage');
    expect(shows(h, 'Maximum rating')).toBe(false);
    expect(h.form.controls['maxRating'].value).toBeNull();
    expect(h.form.controls['measurementType'].enabled).toBe(true);
  });

  it('Boolean: fixes the type to Boolean and the target to 1', async () => {
    const h = await open();
    await change(h, 'scoringMethod', 'Boolean');
    expect(h.form.controls['measurementType'].value).toBe('Boolean');
    expect(h.form.controls['defaultTarget'].value).toBe(1);
    expect(h.form.controls['defaultTarget'].disabled).toBe(true);
    expect(h.el.textContent).toContain('A yes / no KPI has a target of 1');
  });

  it('Range Based: asks for the range and fixes the direction', async () => {
    const h = await open();
    await change(h, 'scoringMethod', 'Range Based');
    expect(shows(h, 'Range minimum')).toBe(true);
    expect(shows(h, 'Range maximum')).toBe(true);
    expect(h.form.controls['direction'].value).toBe('Range Based');
    expect(h.form.controls['direction'].disabled).toBe(true);
    await change(h, 'rangeMin', 105);
    await change(h, 'rangeMax', 95);
    expect(ruleError(h, 'rangeMax')).toContain('greater than the range minimum');
    await change(h, 'rangeMax', 110);
    expect(ruleError(h, 'rangeMax')).toBeUndefined();
  });

  it('Capped Percentage shows the cap; Direct Percentage hides it', async () => {
    const h = await open();
    await change(h, 'scoringMethod', 'Capped Percentage');
    expect(shows(h, 'Maximum achievement (%)')).toBe(true);
    expect(ruleError(h, 'maxAchievementPct')).toContain('needs a maximum');
    await change(h, 'maxAchievementPct', 120);
    expect(h.form.controls['maxAchievementPct'].errors).toBeNull();
    await change(h, 'scoringMethod', 'Direct Percentage');
    expect(shows(h, 'Maximum achievement (%)')).toBe(false);
    expect(h.form.controls['maxAchievementPct'].value).toBeNull();
  });

  it('Lower Is Better requires a cap and says that a result of 0 scores it', async () => {
    const h = await open();
    await change(h, 'direction', 'Lower Is Better');
    expect(shows(h, 'Maximum achievement (%)')).toBe(true);
    expect(h.el.textContent).toContain('a result of 0 scores the cap');
    expect(ruleError(h, 'maxAchievementPct')).toContain('a result of 0 scores it');
    // With Capped Percentage and a cap it is a valid rule (the DEFECT-RATE example).
    await change(h, 'scoringMethod', 'Capped Percentage');
    await change(h, 'defaultTarget', 5);
    await change(h, 'maxAchievementPct', 100);
    expect(h.form.controls['maxAchievementPct'].errors).toBeNull();
  });

  it('refuses to save a rule the checks reject, naming the field, without calling the server', async () => {
    const h = await open(valid());
    await change(h, 'defaultTarget', 0);
    expect(ruleError(h, 'defaultTarget')).toContain('greater than 0');
    h.submit();
    expect(h.save).not.toHaveBeenCalled();
    expect(h.el.textContent).toContain('Please correct the highlighted fields.');
  });

  it('refuses a weight of 0 and above 100', async () => {
    const h = await open(valid());
    await change(h, 'defaultWeight', 0);
    expect(h.form.controls['defaultWeight'].invalid).toBe(true);
    await change(h, 'defaultWeight', 100.01);
    expect(h.form.controls['defaultWeight'].invalid).toBe(true);
    await change(h, 'defaultWeight', 100);
    expect(h.form.controls['defaultWeight'].valid).toBe(true);
  });

  it('saves a consistent template, with the hidden fields empty', async () => {
    const h = await open(valid());
    h.submit();
    expect(h.save).toHaveBeenCalledTimes(1);
    expect(h.save.mock.calls[0][0]).toMatchObject({
      code: 'TST-1',
      scoringMethod: 'Direct Percentage',
      departmentId: null,
      maxRating: null,
      rangeMin: null,
      rangeMax: null,
      maxAchievementPct: null,
    });
  });

  it('shows the server’s field errors too, on the field it names', async () => {
    const error: AppError = {
      code: 'Validation',
      message: 'The KPI rule is not valid.',
      fieldErrors: { defaultTarget: 'The server disagrees with the target.' },
    };
    const h = await open(valid(), () => throwError(() => error));
    h.submit();
    expect(h.form.controls['defaultTarget'].errors?.['server']).toBe(
      'The server disagrees with the target.',
    );
    expect(h.el.textContent).toContain('The KPI rule is not valid.');
    expect(h.el.textContent).toContain('The server disagrees with the target.');
  });
});

describe('Evaluation period form: edit restrictions', () => {
  const companies = [{ id: 1, name: 'APEX Technologies', status: 'Active' }] as Company[];
  const config = buildEvaluationPeriodsConfig({
    service: {} as EvaluationPeriodDataService,
    companies,
  });
  const row = (status: EvaluationPeriod['status']): FormValues =>
    ({
      id: 9,
      companyId: 1,
      name: '2027 Q1',
      periodType: 'Quarterly',
      startDate: '2027-01-01',
      endDate: '2027-03-31',
      submissionDueDate: '2027-04-14',
      requiresHrReview: true,
      status,
      rowVersion: '1',
    }) as FormValues;
  const editing = (status: EvaluationPeriod['status'], save?: Save) =>
    openForm(config, row(status), 9, save);
  const disabledKeys = (h: Harness) =>
    Object.entries(h.form.controls)
      .filter(([, c]) => c.disabled)
      .map(([key]) => key);
  const note = (h: Harness) => h.el.querySelector('[role="note"]')?.textContent ?? null;

  it('creates a period with every field open and no note', async () => {
    const h = await openForm(config, config.newItem(), null);
    expect(disabledKeys(h)).toEqual([]);
    expect(note(h)).toBeNull();
    expect(h.el.textContent).toContain('Requires HR final review');
  });

  it('lets a Draft period change every field', async () => {
    const h = await editing('Draft');
    expect(disabledKeys(h)).toEqual([]);
    expect(note(h)).toBeNull();
  });

  it('lets an Active period change only the name and the due date, and says why', async () => {
    const h = await editing('Active');
    expect(disabledKeys(h)).toEqual([
      'companyId',
      'periodType',
      'startDate',
      'endDate',
      'requiresHrReview',
    ]);
    expect(h.form.controls['name'].enabled).toBe(true);
    expect(h.form.controls['submissionDueDate'].enabled).toBe(true);
    expect(note(h)).toContain('only its name and submission due date');
    expect(h.el.textContent).toContain('Fixed once the period is Active');
  });

  it('saves an Active period with its locked values unchanged', async () => {
    const h = await editing('Active');
    await change(h, 'name', '2027 Q1 (renamed)');
    h.submit();
    expect(h.save.mock.calls[0][0]).toMatchObject({
      name: '2027 Q1 (renamed)',
      companyId: 1,
      startDate: '2027-01-01',
      endDate: '2027-03-31',
      requiresHrReview: true,
    });
  });

  it('refuses an end date that is not after the start date', async () => {
    const h = await editing('Draft');
    await change(h, 'endDate', '2027-01-01');
    expect(ruleError(h, 'endDate')).toContain('after the start date');
    await change(h, 'endDate', '2026-12-31');
    expect(ruleError(h, 'endDate')).toContain('after the start date');
    await change(h, 'endDate', '2027-03-31');
    expect(h.form.controls['endDate'].errors).toBeNull();
  });

  it('shows the server’s overlap conflict above the form', async () => {
    const error: AppError = {
      code: 'Conflict',
      message:
        'The dates overlap period "2027 Q1" (2027-01-01 to 2027-03-31), which is also Quarterly.',
    };
    const h = await editing('Draft', () => throwError(() => error));
    h.submit();
    expect(h.el.querySelector('[role="alert"]')?.textContent).toContain('overlap period "2027 Q1"');
  });
});

describe('Evaluation periods page: Completed and Closed periods are read-only', () => {
  const period = (status: EvaluationPeriod['status']): EvaluationPeriod =>
    ({
      id: 9,
      companyId: 1,
      name: '2027 Q1',
      periodType: 'Quarterly',
      startDate: '2027-01-01',
      endDate: '2027-03-31',
      submissionDueDate: null,
      requiresHrReview: false,
      status,
      rowVersion: '1',
    }) as EvaluationPeriod;

  /** Opens the period's details dialog on the CRUD page and says whether it offers Edit. */
  async function detailsOffersEdit(status: EvaluationPeriod['status']): Promise<boolean> {
    const row = period(status);
    const service = {
      query: () => of({ items: [row], total: 1, pageIndex: 0, pageSize: 10 }),
    } as unknown as EvaluationPeriodDataService;
    const companies = [{ id: 1, name: 'APEX Technologies', status: 'Active' }] as Company[];
    TestBed.configureTestingModule({
      imports: [MatDialogModule],
      providers: [
        provideRouter([]),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    const fixture = TestBed.createComponent(CrudPageComponent<EvaluationPeriod>);
    fixture.componentRef.setInput('config', buildEvaluationPeriodsConfig({ service, companies }));
    fixture.detectChanges();
    await fixture.whenStable();
    (
      fixture.componentInstance as unknown as { openDetails(r: EvaluationPeriod): void }
    ).openDetails(row);
    fixture.detectChanges();
    await fixture.whenStable();
    const buttons = [...document.querySelectorAll('mat-dialog-actions button')];
    const offered = buttons.some((b) => b.textContent?.includes('Edit'));
    TestBed.inject(MatDialog).closeAll();
    fixture.destroy();
    TestBed.resetTestingModule();
    return offered;
  }

  it('offers Edit in the details dialog for Draft and Active periods only', async () => {
    const offered: boolean[] = [];
    for (const status of ['Draft', 'Active', 'Completed', 'Closed'] as const) {
      offered.push(await detailsOffersEdit(status));
    }
    expect(offered).toEqual([true, true, false, false]);
  });
});
