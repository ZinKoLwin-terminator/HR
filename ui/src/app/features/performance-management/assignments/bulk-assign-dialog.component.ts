import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin, map } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { EmployeeLookupItem } from '../../../core/models/employee.models';
import { BulkAssignKpiRequest, BulkAssignmentResult } from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiTemplateDataService,
  ScorecardDataService,
} from '../../../core/services/data/kpi-data.services';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { AssignmentRow, toItemInputs, validateAssignmentRows } from '../shared/assignment-rows';
import { EmployeePickerComponent } from '../shared/employee-picker.component';
import { KpiAssignmentRowsComponent } from '../shared/kpi-assignment-rows.component';
import { safeValue } from '../../../shared/utilities/resource-value';

/** How the employees of a bulk assignment are chosen. */
export type BulkSelector = 'department' | 'branch' | 'position' | 'jobGrade' | 'employees';

export const BULK_SELECTORS: readonly { value: BulkSelector; label: string }[] = [
  { value: 'department', label: 'Everyone in a department' },
  { value: 'branch', label: 'Everyone in a branch' },
  { value: 'position', label: 'Everyone in a position' },
  { value: 'jobGrade', label: 'Everyone in a job grade' },
  { value: 'employees', label: 'Hand-picked employees' },
];

/** Most employees one bulk call may name (the API enforces the same). */
export const BULK_MAX_EMPLOYEES = 500;

const KEY_OF: Record<Exclude<BulkSelector, 'employees'>, keyof BulkAssignKpiRequest> = {
  department: 'departmentId',
  branch: 'branchId',
  position: 'positionId',
  jobGrade: 'jobGradeId',
};

/**
 * The request for a bulk assignment: either the hand-picked ids, or the one org filter chosen,
 * never both. Returns null while the selector has nothing chosen.
 */
export function buildBulkRequest(
  periodId: number,
  selector: BulkSelector,
  filterId: number | null,
  employeeIds: readonly number[],
  rows: readonly AssignmentRow[],
): BulkAssignKpiRequest | null {
  const items = toItemInputs(rows);
  if (selector === 'employees') {
    return employeeIds.length > 0 ? { periodId, employeeIds: [...employeeIds], items } : null;
  }
  return filterId === null ? null : { periodId, [KEY_OF[selector]]: filterId, items };
}

/** Assigns the same KPIs to many employees, then shows who was assigned and who was skipped. */
@Component({
  selector: 'app-bulk-assign-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    EmployeePickerComponent,
    KpiAssignmentRowsComponent,
  ],
  template: `
    <h2 mat-dialog-title>Bulk assign KPIs</h2>
    <mat-dialog-content>
      @if (result(); as done) {
        <p class="summary" role="status" data-testid="bulk-summary">
          <strong>{{ done.assigned.length }} assigned</strong>,
          <strong>{{ done.skipped.length }} skipped</strong>.
        </p>

        <h3>Assigned employees</h3>
        @if (done.assigned.length === 0) {
          <p class="none">Nobody was assigned.</p>
        } @else {
          <div class="scroll" role="region" aria-label="Assigned employees" tabindex="0">
            <table class="result">
              <caption class="cdk-visually-hidden">
                Assigned employees
              </caption>
              <thead>
                <tr>
                  <th scope="col">Employee</th>
                  <th scope="col" class="num">KPIs added</th>
                </tr>
              </thead>
              <tbody>
                @for (row of done.assigned; track row.employeeId) {
                  <tr>
                    <td>{{ row.employeeCode }}</td>
                    <td class="num">{{ row.itemsAdded }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <h3>Skipped employees</h3>
        @if (done.skipped.length === 0) {
          <p class="none">Nobody was skipped.</p>
        } @else {
          <div class="scroll" role="region" aria-label="Skipped employees" tabindex="0">
            <table class="result">
              <caption class="cdk-visually-hidden">
                Skipped employees and the reason
              </caption>
              <thead>
                <tr>
                  <th scope="col">Employee</th>
                  <th scope="col">Reason</th>
                </tr>
              </thead>
              <tbody>
                @for (row of done.skipped; track $index) {
                  <tr>
                    <td>{{ row.employeeCode ?? '#' + row.employeeId }}</td>
                    <td>{{ row.reason }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      } @else {
        @if (lookups.isLoading()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading periods and KPI templates" />
        }
        @if (lookups.error()) {
          <div class="banner" role="alert">
            <mat-icon aria-hidden="true">error</mat-icon>
            <span>{{ lookupError() }}</span>
          </div>
        }
        @if (formError()) {
          <div class="banner" role="alert">
            <mat-icon aria-hidden="true">error</mat-icon>
            <span>{{ formError() }}</span>
          </div>
        }

        <mat-form-field class="field">
          <mat-label>Evaluation period</mat-label>
          <mat-select [value]="periodId()" (selectionChange)="periodId.set($event.value)">
            @for (period of periods(); track period.id) {
              <mat-option [value]="period.id">{{ period.name }} ({{ period.status }})</mat-option>
            }
          </mat-select>
          <mat-hint>Only Draft and Active periods accept new KPIs.</mat-hint>
        </mat-form-field>
        @if (errors()['periodId']) {
          <p class="error" role="alert">{{ errors()['periodId'] }}</p>
        }

        <mat-form-field class="field">
          <mat-label>Choose employees by</mat-label>
          <mat-select [value]="selector()" (selectionChange)="chooseSelector($event.value)">
            @for (s of selectors; track s.value) {
              <mat-option [value]="s.value">{{ s.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (selector() === 'employees') {
          <app-employee-picker
            label="Add an employee"
            [clearOnPick]="true"
            [excluded]="pickedIds()"
            [hint]="'Up to ' + maxEmployees + ' employees'"
            (picked)="addEmployee($event)"
          />
          @if (picked().length > 0) {
            <ul class="picked" aria-label="Chosen employees">
              @for (employee of picked(); track employee.id) {
                <li>
                  <span>{{ employee.employeeCode }} · {{ employee.displayName }}</span>
                  <button
                    mat-icon-button
                    type="button"
                    [attr.aria-label]="'Remove ' + employee.employeeCode"
                    (click)="removeEmployee(employee.id)"
                  >
                    <mat-icon>close</mat-icon>
                  </button>
                </li>
              }
            </ul>
          }
        } @else {
          <mat-form-field class="field">
            <mat-label>{{ selectorLabel() }}</mat-label>
            <mat-select [value]="filterId()" (selectionChange)="filterId.set($event.value)">
              @for (option of filterOptions(); track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
            <mat-hint>Current employees of the period's company only.</mat-hint>
          </mat-form-field>
        }
        @if (errors()['employeeIds'] || errors()['filters']) {
          <p class="error" role="alert">{{ errors()['employeeIds'] || errors()['filters'] }}</p>
        }

        <app-kpi-assignment-rows
          [templates]="fittingTemplates()"
          [errors]="errors()"
          [disabled]="saving()"
          [hint]="rowsHint()"
          [(rows)]="rows"
          (edited)="clearRowErrors()"
        />
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (result(); as done) {
        <button mat-flat-button type="button" (click)="dialogRef.close(done)">Done</button>
      } @else {
        <button mat-button type="button" mat-dialog-close [disabled]="saving()">Cancel</button>
        <button
          mat-flat-button
          type="button"
          [disabled]="saving() || lookups.isLoading()"
          (click)="submit()"
        >
          @if (saving()) {
            <mat-spinner diameter="18" aria-hidden="true" />
            <span>Assigning…</span>
          } @else {
            Assign KPIs
          }
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    h2[mat-dialog-title] {
      margin: 0;
      padding: 22px 28px 16px;
      font: var(--mat-sys-title-large);
      font-weight: 700;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    mat-dialog-content {
      display: block;
      padding: 22px 28px 8px !important;
      max-height: 68vh;
    }
    mat-dialog-actions {
      padding: 14px 28px !important;
      gap: 8px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    .field {
      width: 100%;
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .error {
      margin: -12px 0 12px;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
    .summary {
      margin: 0 0 8px;
      font: var(--mat-sys-title-medium);
    }
    h3 {
      margin: 16px 0 6px;
      font: var(--mat-sys-title-small);
    }
    .none {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .scroll {
      overflow-x: auto;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
    }
    .result {
      width: 100%;
      border-collapse: collapse;
      font: var(--mat-sys-body-medium);
    }
    .result th,
    .result td {
      padding: 8px 12px;
      text-align: start;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .result tbody tr:last-child td {
      border-bottom: 0;
    }
    .result .num {
      text-align: end;
    }
    .picked {
      margin: 0 0 12px;
      padding: 0;
      list-style: none;
      max-height: 160px;
      overflow: auto;
    }
    .picked li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 0 0 0 8px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    button mat-spinner {
      display: inline-block;
      margin-right: 8px;
    }
  `,
})
export class BulkAssignDialogComponent {
  protected readonly dialogRef =
    inject<MatDialogRef<BulkAssignDialogComponent, BulkAssignmentResult | undefined>>(MatDialogRef);
  private readonly scorecards = inject(ScorecardDataService);
  private readonly templatesApi = inject(KpiTemplateDataService);
  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly lookupService = inject(OrganizationLookupService);

  protected readonly selectors = BULK_SELECTORS;
  protected readonly maxEmployees = BULK_MAX_EMPLOYEES;

  protected readonly periodId = signal<number | null>(null);
  protected readonly selector = signal<BulkSelector>('department');
  protected readonly filterId = signal<number | null>(null);
  protected readonly picked = signal<EmployeeLookupItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly formError = signal('');
  /** Field errors (local checks and the server's). Changing who is chosen clears them. */
  protected readonly errors = linkedSignal<unknown, Record<string, string>>({
    source: () => [this.periodId(), this.selector(), this.filterId(), this.picked()],
    computation: () => ({}),
  });
  /** The outcome, once the call has answered: the dialog then shows it instead of the form. */
  protected readonly result = signal<BulkAssignmentResult | null>(null);

  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        templates: this.templatesApi.getAll(),
        periods: this.periodsApi.getAll(),
        // Organization names from the lookup: no organization:View needed, never fails.
        org: this.lookupService.organization(),
      }).pipe(
        map(({ templates, periods, org }) => ({
          templates,
          periods,
          departments: org.departments,
          branches: org.branches,
          positions: org.positions,
          grades: org.jobGrades,
        })),
      ),
  });

  protected readonly periods = computed(() =>
    (safeValue(this.lookups)?.periods ?? [])
      .filter((p) => p.status === 'Draft' || p.status === 'Active')
      .sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  protected readonly pickedIds = computed(() => this.picked().map((e) => e.id));

  protected readonly lookupError = computed(() =>
    errorMessage(this.lookups.error(), 'The periods and KPI templates could not be loaded.'),
  );

  protected readonly rowsHint = computed(() =>
    this.selector() === 'department'
      ? 'Active templates for every department, and those for the chosen department.'
      : 'Active templates. A department-specific one is skipped for employees of other departments.',
  );

  protected readonly selectorLabel = computed(
    () =>
      ({
        department: 'Department',
        branch: 'Branch',
        position: 'Position',
        jobGrade: 'Job grade',
        employees: '',
      })[this.selector()],
  );

  protected readonly filterOptions = computed(() => {
    const l = safeValue(this.lookups);
    if (!l) return [];
    switch (this.selector()) {
      case 'department':
        return options(l.departments, (d) => d.name);
      case 'branch':
        return options(l.branches, (b) => b.name);
      case 'position':
        return options(l.positions, (p) => p.title);
      case 'jobGrade':
        return options(l.grades, (g) => `${g.code} · ${g.name}`);
      default:
        return [];
    }
  });

  /**
   * Active templates. Choosing a department narrows the department-specific ones to it; with any
   * other selector they stay available and the server skips the employees they do not fit.
   */
  protected readonly fittingTemplates = computed(() => {
    const department = this.selector() === 'department' ? this.filterId() : null;
    return (safeValue(this.lookups)?.templates ?? []).filter(
      (t) =>
        t.status === 'Active' &&
        (t.departmentId === null || department === null || t.departmentId === department),
    );
  });

  protected readonly rows = linkedSignal<number[], AssignmentRow[]>({
    source: () => this.fittingTemplates().map((t) => t.id),
    computation: (ids, previous) =>
      (previous?.value ?? []).filter((r) => ids.includes(r.templateId)),
  });

  /** Row indexes shift when a row is removed, so row errors are dropped on any row edit. */
  protected clearRowErrors(): void {
    this.errors.update((errors) =>
      Object.fromEntries(Object.entries(errors).filter(([key]) => !key.startsWith('items'))),
    );
    if (!this.saving()) {
      this.formError.set('');
    }
  }

  protected chooseSelector(selector: BulkSelector): void {
    this.selector.set(selector);
    this.filterId.set(null);
  }

  protected addEmployee(employee: EmployeeLookupItem): void {
    if (this.picked().length < BULK_MAX_EMPLOYEES && !this.pickedIds().includes(employee.id)) {
      this.picked.update((list) => [...list, employee]);
    }
  }

  protected removeEmployee(id: number): void {
    this.picked.update((list) => list.filter((e) => e.id !== id));
  }

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    const periodId = this.periodId();
    const rows = this.rows();
    const errors = validateAssignmentRows(rows, safeValue(this.lookups)?.templates ?? []);
    if (periodId === null) errors['periodId'] = 'Choose an evaluation period.';
    const request =
      periodId === null
        ? null
        : buildBulkRequest(periodId, this.selector(), this.filterId(), this.pickedIds(), rows);
    if (periodId !== null && request === null) {
      errors['employeeIds'] =
        this.selector() === 'employees'
          ? 'Choose at least one employee.'
          : `Choose a ${this.selectorLabel().toLowerCase()}.`;
    }
    this.errors.set(errors);
    if (Object.keys(errors).length > 0 || request === null) {
      this.formError.set('Please correct the highlighted fields.');
      return;
    }

    this.formError.set('');
    this.saving.set(true);
    this.scorecards.bulkAssign(request).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.result.set(result);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (isAppError(error)) {
          this.formError.set(error.message);
          this.errors.set(error.fieldErrors ?? {});
        } else {
          this.formError.set('The KPIs could not be assigned. Please try again.');
        }
      },
    });
  }
}
