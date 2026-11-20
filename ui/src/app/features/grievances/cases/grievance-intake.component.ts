import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, finalize, of, switchMap } from 'rxjs';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import { isAppError } from '../../../core/models/common.models';
import {
  GRIEVANCE_INTAKE_CHANNELS,
  GRIEVANCE_INTAKE_CHANNEL_LABELS,
  GRIEVANCE_LIMITS,
  GrievanceCategoryOption,
  GrievanceEmployeeOption,
  GrievanceIntakeChannel,
  GrievanceIntakeRequest,
} from '../../../core/models/grievance.models';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import {
  GrievanceCaseDataService,
  MyGrievanceDataService,
} from '../../../core/services/data/grievance-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import { safeValue } from '../../../shared/utilities/resource-value';

/** The employee picker holds the typed text until an employee is chosen from the list. */
type EmployeeValue = GrievanceEmployeeOption | string | null;

/** Required, and an employee chosen from the list (not just typed text). */
function chosenEmployee(control: AbstractControl<EmployeeValue>): ValidationErrors | null {
  const value = control.value;
  if (value === null || (typeof value === 'string' && value.trim() === '')) {
    return { required: true };
  }
  return typeof value === 'string' ? { rule: 'Choose an employee from the list.' } : null;
}

export type GrievanceIntakeForm = FormGroup<{
  employee: FormControl<EmployeeValue>;
  receivedVia: FormControl<GrievanceIntakeChannel | null>;
  categoryId: FormControl<number | null>;
  subject: FormControl<string>;
  description: FormControl<string>;
  incidentDate: FormControl<string | null>;
  location: FormControl<string>;
  involvedPersonText: FormControl<string>;
  involvedDepartmentId: FormControl<number | null>;
  declaration: FormControl<boolean>;
}>;

const DECLARATION_REQUIRED = 'Please confirm the declaration to record the grievance.';

/** Where a server field error lands on the form. */
const SERVER_FIELDS: Record<string, keyof GrievanceIntakeForm['controls']> = {
  employeeId: 'employee',
  receivedVia: 'receivedVia',
  categoryId: 'categoryId',
  subject: 'subject',
  description: 'description',
  incidentDate: 'incidentDate',
  location: 'location',
  involvedPersonText: 'involvedPersonText',
  involvedDepartmentId: 'involvedDepartmentId',
  declarationAccepted: 'declaration',
};

/**
 * Record grievance (`/grievances/cases/new`, `grievance-management:Create`): HR files a grievance an
 * employee raised in person, by phone, email or letter. The case is submitted at once, named, with
 * that employee as its submitter (it appears under their My Grievances). Never anonymous; files are
 * added from the case afterwards. The server decides every rule; this form only helps.
 */
@Component({
  selector: 'app-grievance-intake',
  imports: [
    EmptyStateComponent,
    FieldErrorComponent,
    MatAutocompleteModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header
      title="Record grievance"
      description="File a grievance an employee raised with HR in person, by phone, email or letter."
    >
      <a pageActions mat-stroked-button routerLink="/grievances/cases">
        <mat-icon>arrow_back</mat-icon>Back
      </a>
    </app-page-header>

    @if (!apiMode) {
      <p class="notice" role="note">
        <mat-icon aria-hidden="true">info</mat-icon>
        Simulation only — this demo stores grievances in your browser and enforces nothing.
      </p>
    }

    @if (options.error()) {
      <app-empty-state icon="error" title="The form could not be opened" [message]="loadError()">
        <a mat-flat-button routerLink="/grievances/cases">Back to case management</a>
      </app-empty-state>
    } @else if (options.hasValue() ? options.value() : undefined; as data) {
      <form class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <p class="info" role="note">
          <mat-icon aria-hidden="true">badge</mat-icon>
          The grievance is recorded as submitted by this employee. They will see it under My
          Grievances, with a note that HR recorded it for them and how it was received.
        </p>

        <section class="card" aria-labelledby="who-title">
          <h2 id="who-title">Who raised it</h2>
          <div class="row">
            <mat-form-field>
              <mat-label>Employee</mat-label>
              <input
                matInput
                formControlName="employee"
                [matAutocomplete]="employeeAuto"
                placeholder="Search by name or code"
                required
              />
              <mat-autocomplete #employeeAuto="matAutocomplete" [displayWith]="display">
                @for (e of lookup(); track e.employeeId) {
                  <mat-option [value]="e">
                    {{ e.displayName }} ({{ e.employeeCode }})
                    @if (e.departmentName) {
                      <span class="muted"> · {{ e.departmentName }}</span>
                    }
                  </mat-option>
                }
              </mat-autocomplete>
              <mat-hint>Type at least 2 characters of a name or employee code.</mat-hint>
              <mat-error
                ><app-field-error [control]="form.controls.employee" label="Employee"
              /></mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>How it was received</mat-label>
              <mat-select formControlName="receivedVia" required>
                @for (c of channels; track c) {
                  <mat-option [value]="c">{{ channelLabels[c] }}</mat-option>
                }
              </mat-select>
              <mat-error
                ><app-field-error [control]="form.controls.receivedVia" label="How it was received"
              /></mat-error>
            </mat-form-field>
          </div>
        </section>

        <section class="card" aria-labelledby="what-title">
          <h2 id="what-title">What happened</h2>
          <mat-form-field class="full">
            <mat-label>Category</mat-label>
            <mat-select formControlName="categoryId" required>
              @for (c of data.categories; track c.id) {
                <mat-option [value]="c.id">{{ c.name }}</mat-option>
              }
            </mat-select>
            @if (category(); as c) {
              <mat-hint>{{ c.description }}</mat-hint>
            }
            <mat-error
              ><app-field-error [control]="form.controls.categoryId" label="Category"
            /></mat-error>
          </mat-form-field>

          <mat-form-field class="full">
            <mat-label>Subject</mat-label>
            <input matInput formControlName="subject" required [maxlength]="limits.subjectMax" />
            <mat-hint align="end"
              >{{ form.controls.subject.value.length }} / {{ limits.subjectMax }}</mat-hint
            >
            <mat-error
              ><app-field-error [control]="form.controls.subject" label="Subject"
            /></mat-error>
          </mat-form-field>

          <mat-form-field class="full">
            <mat-label>Description</mat-label>
            <textarea
              matInput
              formControlName="description"
              rows="6"
              required
              [maxlength]="limits.descriptionMax"
            ></textarea>
            <mat-hint
              >The employee's account: what happened, when and where, and who was involved. At least
              {{ limits.descriptionMin }} characters.</mat-hint
            >
            <mat-hint align="end"
              >{{ form.controls.description.value.length }} / {{ limits.descriptionMax }}</mat-hint
            >
            <mat-error
              ><app-field-error [control]="form.controls.description" label="Description"
            /></mat-error>
          </mat-form-field>

          <div class="row">
            <mat-form-field>
              <mat-label>Incident date</mat-label>
              <input
                matInput
                formControlName="incidentDate"
                [matDatepicker]="incidentPicker"
                [max]="today"
              />
              <mat-datepicker-toggle matIconSuffix [for]="incidentPicker" />
              <mat-datepicker #incidentPicker />
              <mat-hint>Optional, not after today.</mat-hint>
              <mat-error
                ><app-field-error [control]="form.controls.incidentDate" label="Incident date"
              /></mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Location</mat-label>
              <input matInput formControlName="location" [maxlength]="limits.shortTextMax" />
              <mat-error
                ><app-field-error [control]="form.controls.location" label="Location"
              /></mat-error>
            </mat-form-field>
          </div>

          <div class="row">
            <mat-form-field>
              <mat-label>Person involved</mat-label>
              <input
                matInput
                formControlName="involvedPersonText"
                [maxlength]="limits.shortTextMax"
              />
              <mat-hint>Optional: a name or a role.</mat-hint>
              <mat-error
                ><app-field-error
                  [control]="form.controls.involvedPersonText"
                  label="Person involved"
              /></mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Department involved</mat-label>
              <mat-select formControlName="involvedDepartmentId">
                <mat-option [value]="null">None</mat-option>
                @for (d of data.departments; track d.id) {
                  <mat-option [value]="d.id">{{ d.name }}</mat-option>
                }
              </mat-select>
              <mat-error
                ><app-field-error
                  [control]="form.controls.involvedDepartmentId"
                  label="Department involved"
              /></mat-error>
            </mat-form-field>
          </div>
          <p class="hint">Files can be added from the case once it is recorded.</p>
        </section>

        <section class="card" aria-labelledby="declaration-title">
          <h2 id="declaration-title">Declaration</h2>
          <mat-checkbox formControlName="declaration">
            I confirm that this is the employee's own account, given to HR, and that the employee
            agreed that HR records it as their grievance.
          </mat-checkbox>
          @if (form.controls.declaration.touched && form.controls.declaration.invalid) {
            <p class="error" role="alert">{{ declarationError() }}</p>
          }
        </section>

        @if (formError()) {
          <p class="error" role="alert">{{ formError() }}</p>
        }

        <div class="actions">
          @if (saving()) {
            <mat-progress-bar mode="indeterminate" aria-label="Recording" />
          }
          <a mat-stroked-button routerLink="/grievances/cases">Cancel</a>
          <button mat-flat-button type="submit" [disabled]="saving()">
            <mat-icon>send</mat-icon>Record grievance
          </button>
        </div>
      </form>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the form" />
    }
  `,
  styles: `
    .notice,
    .info {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      font: var(--mat-sys-body-medium);
    }
    .notice {
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .info {
      margin: 0;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: 880px;
    }
    .card {
      padding: 16px 20px;
      border-radius: var(--app-radius-lg, 12px);
      background: var(--mat-sys-surface-container-low);
    }
    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-medium);
    }
    .full {
      width: 100%;
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 0 16px;
    }
    .hint,
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .hint {
      margin: 4px 0 0;
      font: var(--mat-sys-body-small);
    }
    .error {
      margin: 4px 0 0;
      color: var(--mat-sys-error);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      align-items: center;
      gap: 12px;
    }
    .actions mat-progress-bar {
      flex: 1 1 100%;
    }
  `,
})
export class GrievanceIntakeComponent implements HasUnsavedChanges {
  private readonly cases = inject(GrievanceCaseDataService);
  private readonly mine = inject(MyGrievanceDataService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiNotificationService);
  protected readonly apiMode = inject(SERVER_SESSION);

  protected readonly limits = GRIEVANCE_LIMITS;
  protected readonly today = todayIso();
  protected readonly channels = GRIEVANCE_INTAKE_CHANNELS;
  protected readonly channelLabels = GRIEVANCE_INTAKE_CHANNEL_LABELS;

  readonly form: GrievanceIntakeForm = new FormGroup({
    employee: new FormControl<EmployeeValue>('', chosenEmployee),
    receivedVia: new FormControl<GrievanceIntakeChannel | null>(null, Validators.required),
    categoryId: new FormControl<number | null>(null, Validators.required),
    subject: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        HrmsValidators.notBlank,
        Validators.minLength(GRIEVANCE_LIMITS.subjectMin),
        Validators.maxLength(GRIEVANCE_LIMITS.subjectMax),
      ],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        HrmsValidators.notBlank,
        Validators.minLength(GRIEVANCE_LIMITS.descriptionMin),
        Validators.maxLength(GRIEVANCE_LIMITS.descriptionMax),
      ],
    }),
    incidentDate: new FormControl<string | null>(null, HrmsValidators.notInFuture),
    location: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(GRIEVANCE_LIMITS.shortTextMax)],
    }),
    involvedPersonText: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(GRIEVANCE_LIMITS.shortTextMax)],
    }),
    involvedDepartmentId: new FormControl<number | null>(null),
    declaration: new FormControl(false, { nonNullable: true, validators: Validators.requiredTrue }),
  });

  /** Active categories and departments (`GET /options`). */
  protected readonly options = rxResource({ stream: () => this.mine.options() });
  protected readonly loadError = computed(() =>
    this.options.error() ? errorMessage(this.options.error()) : '',
  );

  private readonly categoryId = toSignal(this.form.controls.categoryId.valueChanges, {
    initialValue: null,
  });
  protected readonly category = computed<GrievanceCategoryOption | undefined>(() =>
    safeValue(this.options)?.categories.find((c) => c.id === this.categoryId()),
  );

  /** Employees matching the typed text (a continuous stream, so a signal, not a resource). */
  protected readonly lookup = toSignal(
    this.form.controls.employee.valueChanges.pipe(
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

  protected readonly display = (value: EmployeeValue): string =>
    typeof value === 'string' ? value : value ? `${value.displayName} (${value.employeeCode})` : '';

  protected readonly formError = signal<string | null>(null);
  protected readonly saving = signal(false);
  /** The server's wording for the declaration, when it refused it. */
  protected readonly declarationError = signal(DECLARATION_REQUIRED);
  /** Set once the case is recorded (the guard lets the screen go). */
  private leaving = false;

  hasUnsavedChanges(): boolean {
    return !this.leaving && this.form.dirty;
  }

  private request(): GrievanceIntakeRequest {
    const v = this.form.getRawValue();
    const blank = (text: string) => (text.trim() === '' ? null : text.trim());
    return {
      employeeId: (v.employee as GrievanceEmployeeOption).employeeId,
      categoryId: v.categoryId!,
      subject: v.subject.trim(),
      description: v.description.trim(),
      incidentDate: v.incidentDate || null,
      location: blank(v.location),
      involvedPersonText: blank(v.involvedPersonText),
      involvedDepartmentId: v.involvedDepartmentId,
      receivedVia: v.receivedVia!,
      declarationAccepted: v.declaration,
    };
  }

  /** Puts a server refusal on the field it names, and says it. */
  private fail(error: unknown): void {
    if (isAppError(error) && error.fieldErrors) {
      for (const [field, message] of Object.entries(error.fieldErrors)) {
        const name = SERVER_FIELDS[field];
        if (name) {
          if (name === 'declaration') {
            this.declarationError.set(message);
          }
          this.form.controls[name].setErrors({ server: message });
          this.form.controls[name].markAsTouched();
        }
      }
    }
    this.formError.set(errorMessage(error));
    this.toast.error(error);
  }

  protected submit(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    this.formError.set(null);
    this.declarationError.set(DECLARATION_REQUIRED);
    if (this.form.invalid) {
      this.formError.set('Please complete the highlighted fields before recording.');
      return;
    }
    this.saving.set(true);
    this.cases
      .recordForEmployee(this.request())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (recorded) => {
          this.leaving = true;
          this.form.markAsPristine();
          this.toast.success(`Grievance ${recorded.caseNo} recorded.`);
          void this.router.navigate(['/grievances/cases', recorded.id]);
        },
        error: (e: unknown) => this.fail(e),
      });
  }
}
