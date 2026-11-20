import {
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { catchError, debounceTime, finalize, of, switchMap } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import {
  GRIEVANCE_PRIORITIES,
  GrievanceCaseDetail,
  GrievanceEmployeeOption,
  GrievancePriority,
} from '../../../core/models/grievance.models';
import {
  GrievanceCaseDataService,
  GrievanceCategoryDataService,
} from '../../../core/services/data/grievance-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { isGrievanceOpen } from '../../../core/utilities/grievance-workflow';
import { todayIso } from '../../../shared/utilities/date.util';
import { safeValue } from '../../../shared/utilities/resource-value';

/** Where a server field error lands on the triage form. */
const FIELDS = [
  'categoryId',
  'priority',
  'targetResolutionDate',
  'subjectEmployeeId',
  'involvedDepartmentId',
  'legalHold',
] as const;

/**
 * Triage (management Edit, open cases): category, priority, target date, the subject employee (the
 * person the grievance is about: they are kept out of every case-team tier of the case), the involved
 * department and legal hold. Raising the priority to Critical escalates the case at once.
 */
@Component({
  selector: 'app-grievance-triage-panel',
  imports: [
    FieldErrorComponent,
    MatAutocompleteModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div class="row">
        <mat-form-field>
          <mat-label>Category</mat-label>
          <mat-select formControlName="categoryId">
            @for (c of categoryOptions(); track c.id) {
              <mat-option [value]="c.id" [disabled]="c.disabled">{{ c.name }}</mat-option>
            }
          </mat-select>
          <mat-error
            ><app-field-error [control]="form.controls.categoryId" label="Category"
          /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            @if (detail().status === 'Submitted') {
              <mat-option [value]="null">Not set</mat-option>
            }
            @for (p of priorities; track p) {
              <mat-option [value]="p">{{ p }}</mat-option>
            }
          </mat-select>
          @if (form.controls.priority.value === 'Critical' && !detail().isEscalated) {
            <mat-hint>Saving escalates the case to the reviewers at once.</mat-hint>
          }
          <mat-error
            ><app-field-error [control]="form.controls.priority" label="Priority"
          /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Target resolution date</mat-label>
          <input
            matInput
            formControlName="targetResolutionDate"
            [matDatepicker]="picker"
            [min]="today"
          />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-error
            ><app-field-error [control]="form.controls.targetResolutionDate" label="Target date"
          /></mat-error>
        </mat-form-field>
      </div>
      <div class="row">
        <mat-form-field>
          <mat-label>Subject employee</mat-label>
          <input
            matInput
            [formControl]="subjectSearch"
            [matAutocomplete]="subjectAuto"
            placeholder="Search by name or code"
          />
          <mat-autocomplete
            #subjectAuto="matAutocomplete"
            [displayWith]="display"
            (optionSelected)="pickSubject($event.option.value)"
          >
            @for (e of lookup(); track e.employeeId) {
              <mat-option [value]="e">{{ e.displayName }} ({{ e.employeeCode }})</mat-option>
            }
          </mat-autocomplete>
          @if (form.controls.subjectEmployeeId.value !== null) {
            <button
              matIconSuffix
              mat-icon-button
              type="button"
              aria-label="Clear the subject employee"
              (click)="clearSubject()"
            >
              <mat-icon>close</mat-icon>
            </button>
          }
          <mat-hint>The person the grievance is about: they never see the case.</mat-hint>
          @if (form.controls.subjectEmployeeId.errors?.['server']; as message) {
            <mat-hint class="error">{{ message }}</mat-hint>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Department involved</mat-label>
          <mat-select formControlName="involvedDepartmentId">
            <mat-option [value]="null">None</mat-option>
            @for (d of departments(); track d.id) {
              <mat-option [value]="d.id">{{ d.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      @if (holdOnly()) {
        <p class="hint">This case is no longer open: only the legal hold can change.</p>
      }
      <mat-slide-toggle formControlName="legalHold">
        Legal hold (the case cannot be archived while it is on)
      </mat-slide-toggle>
      @if (error()) {
        <p class="error" role="alert">
          {{ error() }}
          @if (conflict()) {
            <button mat-button type="button" (click)="reload.emit()">Reload the case</button>
          }
        </p>
      }
      <div class="actions">
        <button mat-flat-button type="submit" [disabled]="busy() || form.pristine">
          <mat-icon>save</mat-icon>Save triage
        </button>
      </div>
    </form>
  `,
  styles: `
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 0 12px;
    }
    .error {
      margin: 8px 0 0;
      color: var(--mat-sys-error);
    }
    .hint {
      margin: 0 0 8px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 8px;
    }
  `,
})
export class GrievanceTriagePanelComponent {
  readonly detail = input.required<GrievanceCaseDetail>();
  readonly departments = input<{ id: number; name: string }[]>([]);
  readonly changed = output<GrievanceCaseDetail>();
  readonly reload = output<void>();

  private readonly cases = inject(GrievanceCaseDataService);
  private readonly categories = inject(GrievanceCategoryDataService);
  private readonly toast = inject(UiNotificationService);

  protected readonly priorities = GRIEVANCE_PRIORITIES;
  protected readonly today = todayIso();
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly conflict = signal(false);

  readonly form = new FormGroup({
    categoryId: new FormControl<number>(0, { nonNullable: true, validators: Validators.required }),
    priority: new FormControl<GrievancePriority | null>(null),
    targetResolutionDate: new FormControl<string | null>(null),
    subjectEmployeeId: new FormControl<number | null>(null),
    involvedDepartmentId: new FormControl<number | null>(null),
    legalHold: new FormControl(false, { nonNullable: true }),
  });
  protected readonly subjectSearch = new FormControl<string | GrievanceEmployeeOption>('', {
    nonNullable: true,
  });

  private readonly categoryList = rxResource({ stream: () => this.categories.getAll() });
  /** Active categories (an anonymous case only those that accept anonymity), plus the current one. */
  protected readonly categoryOptions = computed(() => {
    const d = this.detail();
    return (safeValue(this.categoryList) ?? [])
      .filter((c) => c.status === 'Active' || c.id === d.categoryId)
      .map((c) => ({
        id: c.id,
        name: c.status === 'Active' ? c.name : `${c.name} (inactive)`,
        disabled: c.id !== d.categoryId && d.isAnonymous && !c.allowsAnonymous,
      }));
  });

  /** Employees matching the subject search (a continuous stream, so a signal, not a resource). */
  protected readonly lookup = toSignal(
    this.subjectSearch.valueChanges.pipe(
      debounceTime(250),
      switchMap((value) =>
        typeof value === 'string' && value.trim().length >= 2
          ? this.cases
              .employeeOptions(value.trim())
              .pipe(catchError(() => of([] as GrievanceEmployeeOption[])))
          : of([] as GrievanceEmployeeOption[]),
      ),
    ),
    { initialValue: [] as GrievanceEmployeeOption[] },
  );

  protected readonly display = (value: string | GrievanceEmployeeOption | null): string =>
    typeof value === 'string' ? value : value ? `${value.displayName} (${value.employeeCode})` : '';

  constructor() {
    effect(() => {
      const d = this.detail();
      untracked(() => this.fill(d));
    });
  }

  private fill(d: GrievanceCaseDetail): void {
    this.form.reset({
      categoryId: d.categoryId,
      priority: d.priority,
      targetResolutionDate: d.targetResolutionDate,
      subjectEmployeeId: d.subjectEmployeeId,
      involvedDepartmentId: d.involvedDepartmentId,
      legalHold: d.legalHold,
    });
    this.subjectSearch.setValue(d.subjectEmployeeName ?? '', { emitEvent: false });
    // A case that is no longer open keeps its triage: only the legal hold may change.
    const locked = !isGrievanceOpen(d.status);
    for (const name of FIELDS) {
      if (name === 'legalHold') continue;
      if (locked) this.form.controls[name].disable({ emitEvent: false });
      else this.form.controls[name].enable({ emitEvent: false });
    }
    if (locked) this.subjectSearch.disable({ emitEvent: false });
    else this.subjectSearch.enable({ emitEvent: false });
    this.error.set(null);
    this.conflict.set(false);
  }

  /** The case is no longer open: only the legal hold is offered. */
  protected readonly holdOnly = computed(() => !isGrievanceOpen(this.detail().status));

  protected pickSubject(employee: GrievanceEmployeeOption): void {
    this.form.controls.subjectEmployeeId.setValue(employee.employeeId);
    this.form.controls.subjectEmployeeId.markAsDirty();
  }

  protected clearSubject(): void {
    this.form.controls.subjectEmployeeId.setValue(null);
    this.form.controls.subjectEmployeeId.markAsDirty();
    this.subjectSearch.setValue('', { emitEvent: false });
  }

  protected save(): void {
    if (this.busy() || this.form.invalid) return;
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.conflict.set(false);
    this.cases
      .triage(this.detail().id, {
        rowVersion: this.detail().rowVersion,
        categoryId: v.categoryId,
        priority: v.priority,
        targetResolutionDate: v.targetResolutionDate || null,
        subjectEmployeeId: v.subjectEmployeeId,
        involvedDepartmentId: v.involvedDepartmentId,
        legalHold: v.legalHold,
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (detail) => {
          this.toast.success(
            detail.isEscalated && !this.detail().isEscalated
              ? 'Triage saved. The case was escalated as Critical.'
              : 'Triage saved.',
          );
          this.changed.emit(detail);
        },
        error: (e: unknown) => {
          if (isAppError(e) && e.code === 'Conflict') this.conflict.set(true);
          let placed = false;
          for (const [field, message] of Object.entries(
            isAppError(e) ? (e.fieldErrors ?? {}) : {},
          )) {
            const name = FIELDS.find((f) => f === field);
            if (name) {
              this.form.controls[name].setErrors({ server: message });
              this.form.controls[name].markAsTouched();
              placed = true;
            }
          }
          if (!placed) this.error.set(errorMessage(e));
        },
      });
  }
}
