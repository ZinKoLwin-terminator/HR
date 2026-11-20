import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin, map } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { EmployeeLookupItem } from '../../../core/models/employee.models';
import { AssignKpiResult, ScorecardDetail } from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiTemplateDataService,
  ScorecardDataService,
} from '../../../core/services/data/kpi-data.services';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { errorMessage } from '../../../core/services/ui-notification.service';
import {
  AssignmentRow,
  templateFitsDepartment,
  toItemInputs,
  validateAssignmentRows,
} from '../shared/assignment-rows';
import { EmployeePickerComponent } from '../shared/employee-picker.component';
import { KpiAssignmentRowsComponent } from '../shared/kpi-assignment-rows.component';
import { safeValue } from '../../../shared/utilities/resource-value';

export interface AssignKpisDialogData {
  /**
   * Adding KPIs to a scorecard that exists (in Planning): the employee and period are fixed, the
   * KPIs it holds are not offered again and its weight counts toward the 100% cap.
   */
  scorecard?: ScorecardDetail;
}

/** Keeps the errors that are not about one KPI row (`items[i].*`). */
const withoutRowErrors = (errors: Record<string, string>): Record<string, string> =>
  Object.fromEntries(Object.entries(errors).filter(([key]) => !key.startsWith('items')));

/** Assigns KPIs to one employee for one period. Closes with the {@link AssignKpiResult}. */
@Component({
  selector: 'app-assign-kpis-dialog',
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
    <h2 mat-dialog-title>{{ existing() ? 'Add KPIs to the plan' : 'Assign KPIs' }}</h2>
    <mat-dialog-content>
      @if (lookups.isLoading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Loading KPI templates and periods" />
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

      @if (existing(); as card) {
        <p class="fixed">
          <strong>{{ card.employeeCode }} · {{ card.employeeName }}</strong>
          · {{ card.periodName }} · {{ card.departmentName }}
        </p>
      } @else {
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

        <app-employee-picker
          [(selected)]="employee"
          [error]="errors()['employeeId'] ?? ''"
          hint="Current employees only"
        />
      }

      <app-kpi-assignment-rows
        [templates]="fittingTemplates()"
        [excludedIds]="assignedTemplateIds()"
        [existingWeight]="existing()?.totalWeight ?? 0"
        [errors]="errors()"
        [disabled]="saving()"
        [hint]="rowsHint()"
        [(rows)]="rows"
        (edited)="clearRowErrors()"
      />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close [disabled]="saving()">Cancel</button>
      <button
        mat-flat-button
        type="button"
        [disabled]="saving() || lookups.isLoading()"
        (click)="submit()"
      >
        @if (saving()) {
          <mat-spinner diameter="18" aria-hidden="true" />
          <span>Saving…</span>
        } @else {
          {{ existing() ? 'Add KPIs' : 'Assign KPIs' }}
        }
      </button>
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
    .fixed {
      margin: 0 0 16px;
      color: var(--mat-sys-on-surface-variant);
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
    button mat-spinner {
      display: inline-block;
      margin-right: 8px;
    }
    @media (max-width: 600px) {
      h2[mat-dialog-title] {
        padding: 16px 16px 12px;
      }
      mat-dialog-content {
        padding: 16px 16px 8px !important;
      }
      mat-dialog-actions {
        padding: 12px 16px !important;
      }
    }
  `,
})
export class AssignKpisDialogComponent {
  private readonly data = inject<AssignKpisDialogData | null>(MAT_DIALOG_DATA, { optional: true });
  private readonly dialogRef =
    inject<MatDialogRef<AssignKpisDialogComponent, AssignKpiResult>>(MatDialogRef);
  private readonly scorecards = inject(ScorecardDataService);
  private readonly templatesApi = inject(KpiTemplateDataService);
  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly lookupService = inject(OrganizationLookupService);

  protected readonly existing = signal<ScorecardDetail | null>(this.data?.scorecard ?? null);
  protected readonly periodId = signal<number | null>(null);
  protected readonly employee = signal<EmployeeLookupItem | null>(null);
  protected readonly saving = signal(false);
  protected readonly formError = signal('');
  /** Field errors (local checks and the server's). Choosing another period or employee clears them. */
  protected readonly errors = linkedSignal<unknown, Record<string, string>>({
    source: () => [this.periodId(), this.employee()],
    computation: () => ({}),
  });

  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        templates: this.templatesApi.getAll(),
        periods: this.periodsApi.getAll(),
        // From the organization lookup: no organization:View needed, never fails.
        departments: this.lookupService.organization().pipe(map((o) => o.departments)),
      }),
  });
  protected readonly lookupError = computed(() =>
    errorMessage(this.lookups.error(), 'The templates and periods could not be loaded.'),
  );

  /** Periods that accept new KPIs: Draft or Active, newest first. */
  protected readonly periods = computed(() =>
    (safeValue(this.lookups)?.periods ?? [])
      .filter((p) => p.status === 'Draft' || p.status === 'Active')
      .sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  private readonly departmentNames = computed(
    () => new Map((safeValue(this.lookups)?.departments ?? []).map((d) => [d.id, d.name])),
  );

  /** Department of the person the KPIs are for (null until an employee is chosen). */
  private readonly departmentName = computed(
    () => this.existing()?.departmentName ?? this.employee()?.departmentName ?? null,
  );

  /** Active templates that fit the employee's department. */
  protected readonly fittingTemplates = computed(() =>
    (safeValue(this.lookups)?.templates ?? []).filter(
      (t) =>
        t.status === 'Active' &&
        templateFitsDepartment(t, this.departmentName(), this.departmentNames()),
    ),
  );

  protected readonly rowsHint = computed(() =>
    this.departmentName()
      ? `Active templates for every department, and those for ${this.departmentName()}.`
      : 'Active templates for every department. Choose an employee to see the department-specific ones.',
  );

  protected readonly assignedTemplateIds = computed(
    () => this.existing()?.items.map((i) => i.templateId) ?? [],
  );

  /** Chosen KPIs; one that stops fitting after the employee changes is dropped. */
  protected readonly rows = linkedSignal<number[], AssignmentRow[]>({
    source: () => this.fittingTemplates().map((t) => t.id),
    computation: (ids, previous) =>
      (previous?.value ?? []).filter((r) => ids.includes(r.templateId)),
  });

  /** Row indexes shift when a row is removed, so row errors are dropped on any row edit. */
  protected clearRowErrors(): void {
    this.errors.update(withoutRowErrors);
    if (!this.saving()) {
      this.formError.set('');
    }
  }

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    const card = this.existing();
    const employeeId = card?.employeeId ?? this.employee()?.id ?? null;
    const periodId = card?.periodId ?? this.periodId();
    const rows = this.rows();

    const errors = validateAssignmentRows(
      rows,
      safeValue(this.lookups)?.templates ?? [],
      card?.totalWeight ?? 0,
    );
    if (periodId === null) errors['periodId'] = 'Choose an evaluation period.';
    if (employeeId === null) errors['employeeId'] = 'Choose an employee from the list.';
    this.errors.set(errors);
    if (Object.keys(errors).length > 0 || employeeId === null || periodId === null) {
      this.formError.set('Please correct the highlighted fields.');
      return;
    }

    this.formError.set('');
    this.saving.set(true);
    this.scorecards.assign({ employeeId, periodId, items: toItemInputs(rows) }).subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (error: unknown) => {
        this.saving.set(false);
        if (isAppError(error)) {
          // A 409 (already assigned, plan past Planning, stale) has no field: the banner says it.
          this.formError.set(error.message);
          this.errors.set(error.fieldErrors ?? {});
        } else {
          this.formError.set('The KPIs could not be assigned. Please try again.');
        }
      },
    });
  }
}
