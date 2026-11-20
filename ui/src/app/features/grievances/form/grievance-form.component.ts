import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router, RouterLink } from '@angular/router';
import {
  Observable,
  concatMap,
  filter,
  finalize,
  forkJoin,
  from,
  last,
  map,
  of,
  switchMap,
} from 'rxjs';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import { isAppError } from '../../../core/models/common.models';
import {
  GRIEVANCE_LIMITS,
  GrievanceCategoryOption,
  GrievanceDraftInput,
  MyGrievanceDetail,
} from '../../../core/models/grievance.models';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { MyGrievanceDataService } from '../../../core/services/data/grievance-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import {
  ANONYMITY_FIXED_NOTE,
  ANONYMOUS_FILE_WARNING,
  ANONYMOUS_HR_DOES_NOT_SEE,
  ANONYMOUS_HR_SEES,
  ANONYMOUS_NOT_ALLOWED,
  ANONYMOUS_RESIDUAL_RISKS,
} from '../shared/grievance-anonymity';
import {
  GrievanceAttachmentListComponent,
  GrievanceFileItem,
} from '../shared/grievance-attachment-list.component';
import { safeValue } from '../../../shared/utilities/resource-value';

/** The form of a grievance draft. `declaration` is checked at submission only. */
export type GrievanceForm = FormGroup<{
  categoryId: FormControl<number | null>;
  subject: FormControl<string>;
  description: FormControl<string>;
  incidentDate: FormControl<string | null>;
  location: FormControl<string>;
  involvedPersonText: FormControl<string>;
  involvedDepartmentId: FormControl<number | null>;
  isAnonymous: FormControl<boolean>;
  declaration: FormControl<boolean>;
}>;

/** The stricter rules of a submission (the minimum lengths and the declaration). */
const SUBMIT_RULES: Record<'subject' | 'description' | 'declaration', ValidatorFn[]> = {
  subject: [Validators.minLength(GRIEVANCE_LIMITS.subjectMin)],
  description: [Validators.required, Validators.minLength(GRIEVANCE_LIMITS.descriptionMin)],
  declaration: [Validators.requiredTrue],
};

/** Where a server field error lands on the form. */
const SERVER_FIELDS: Record<string, keyof GrievanceForm['controls']> = {
  categoryId: 'categoryId',
  subject: 'subject',
  description: 'description',
  incidentDate: 'incidentDate',
  location: 'location',
  involvedPersonText: 'involvedPersonText',
  involvedDepartmentId: 'involvedDepartmentId',
  isAnonymous: 'isAnonymous',
  declarationAccepted: 'declaration',
};

let pendingKey = 0;

/**
 * New grievance and edit draft (`/grievances/my/new`, `/grievances/my/:id/edit`). A draft needs a
 * category and a subject; submitting also needs the minimum lengths and the declaration, and asks
 * for confirmation. The anonymous switch is offered only for categories that accept it, with an
 * honest account of what HR will and will not see. Files are checked here first, kept until the
 * draft exists, then uploaded. Leaving with unsaved edits asks first (`unsavedChangesGuard`).
 */
@Component({
  selector: 'app-grievance-form',
  imports: [
    EmptyStateComponent,
    FieldErrorComponent,
    GrievanceAttachmentListComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header [title]="title()" [description]="description()">
      <a pageActions mat-stroked-button [routerLink]="backLink()">
        <mat-icon>arrow_back</mat-icon>Back
      </a>
    </app-page-header>

    @if (!apiMode) {
      <p class="notice" role="note">
        <mat-icon aria-hidden="true">info</mat-icon>
        Simulation only — this demo stores grievances in your browser and enforces nothing,
        including anonymity.
      </p>
    }

    @if (!linked()) {
      <app-empty-state
        icon="person_off"
        title="No employee record is linked to your account"
        message="Grievances are raised by employees. Ask HR to link your user account to your employee record."
      />
    } @else if (loaded.error()) {
      <app-empty-state
        icon="error"
        title="This grievance could not be opened"
        [message]="loadError()"
      >
        <a mat-flat-button routerLink="/grievances/my">Back to my grievances</a>
      </app-empty-state>
    } @else if (notDraft()) {
      <app-empty-state
        icon="lock"
        title="This grievance has been submitted"
        message="A submitted grievance can no longer be edited. Open it to follow its progress or to send HR a message."
      >
        <a mat-flat-button [routerLink]="['/grievances/my', draftId()]">Open the grievance</a>
      </app-empty-state>
    } @else if (loaded.hasValue() ? loaded.value() : undefined; as data) {
      <form class="form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <section class="card" aria-labelledby="who-title">
          <h2 id="who-title">Who is reporting</h2>
          @if (anonymous()) {
            <p class="who">
              <mat-icon aria-hidden="true">visibility_off</mat-icon>
              Anonymous: HR will not see your name, department or position.
            </p>
          } @else {
            <div class="row">
              <mat-form-field>
                <mat-label>Name</mat-label>
                <input matInput [value]="me().displayName" readonly />
              </mat-form-field>
              <mat-form-field>
                <mat-label>Job title</mat-label>
                <input matInput [value]="me().jobTitle || '—'" readonly />
              </mat-form-field>
            </div>
            <p class="hint">
              Your employee code, department and position are recorded from your employee record
              when you submit.
            </p>
          }
        </section>

        <section class="card" aria-labelledby="what-title">
          <h2 id="what-title">What happened</h2>
          <mat-form-field class="full">
            <mat-label>Category</mat-label>
            <mat-select formControlName="categoryId" required>
              @for (c of data.options.categories; track c.id) {
                <mat-option [value]="c.id" [disabled]="lockedAnonymous() && !c.allowsAnonymous">
                  {{ c.name }}{{ c.allowsAnonymous ? '' : ' (named only)' }}
                </mat-option>
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
              [maxlength]="limits.descriptionMax"
            ></textarea>
            <mat-hint
              >What happened, when and where, and who was involved. At least
              {{ limits.descriptionMin }} characters to submit.</mat-hint
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
                @for (d of data.options.departments; track d.id) {
                  <mat-option [value]="d.id">{{ d.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        </section>

        <section class="card" aria-labelledby="anon-title">
          <h2 id="anon-title">Anonymity</h2>
          @if (draftId() === null) {
            <mat-slide-toggle formControlName="isAnonymous" aria-describedby="anon-help">
              Report anonymously
            </mat-slide-toggle>
          } @else {
            <p class="fixed" data-testid="anonymity-fixed">
              <strong>{{
                anonymous() ? 'You are reporting anonymously.' : 'You are reporting with your name.'
              }}</strong>
              {{ fixedNote }}
            </p>
          }
          <div id="anon-help" class="anon">
            @if (anonymousBlocked()) {
              <p class="hint" role="note">{{ notAllowed }}</p>
            }
            <div class="anon__cols">
              <div>
                <h3>HR will see</h3>
                <ul>
                  @for (line of hrSees; track line) {
                    <li>{{ line }}</li>
                  }
                </ul>
              </div>
              <div>
                <h3>HR will not see</h3>
                <ul>
                  @for (line of hrDoesNotSee; track line) {
                    <li>{{ line }}</li>
                  }
                </ul>
              </div>
            </div>
            <h3>What can still identify you</h3>
            <ul>
              @for (line of risks; track line) {
                <li>{{ line }}</li>
              }
            </ul>
            <p class="hint">
              The choice is fixed when the draft is first saved (Save draft or Submit). You can
              still follow the grievance and answer HR's questions here; HR will not be notified who
              you are.
            </p>
          </div>
        </section>

        <section class="card" aria-labelledby="files-title">
          <h2 id="files-title">Files</h2>
          <app-grievance-attachment-list
            label="Files of this grievance"
            [files]="files()"
            [canAdd]="true"
            [busy]="saving()"
            [error]="fileError()"
            [warning]="anonymous() ? fileWarning : null"
            (add)="addFile($event)"
            (removePending)="removeFile($event)"
            (download)="download($event)"
          />
        </section>

        <section class="card" aria-labelledby="declaration-title">
          <h2 id="declaration-title">Declaration</h2>
          <mat-checkbox formControlName="declaration">
            I confirm that what I have written is true to the best of my knowledge, and I understand
            that HR may need to look into it with the people concerned.
          </mat-checkbox>
          @if (form.controls.declaration.touched && form.controls.declaration.invalid) {
            <p class="error" role="alert">Please confirm the declaration to submit.</p>
          }
        </section>

        @if (formError()) {
          <p class="error" role="alert">{{ formError() }}</p>
        }

        <div class="actions">
          @if (saving()) {
            <mat-progress-bar mode="indeterminate" aria-label="Saving" />
          }
          <button mat-stroked-button type="button" [disabled]="saving()" (click)="saveDraft()">
            <mat-icon>save</mat-icon>Save draft
          </button>
          <button mat-flat-button type="submit" [disabled]="saving()">
            <mat-icon>send</mat-icon>Submit
          </button>
        </div>
      </form>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the form" />
    }
  `,
  styles: `
    .notice {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
      font: var(--mat-sys-body-medium);
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
    h3 {
      margin: 8px 0 4px;
      font: var(--mat-sys-title-small);
    }
    .full {
      width: 100%;
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 0 16px;
    }
    .who {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
    }
    .hint {
      margin: 4px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .fixed {
      margin: 0 0 8px;
    }
    .anon ul {
      margin: 0;
      padding-left: 20px;
      font: var(--mat-sys-body-medium);
    }
    .anon__cols {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 0 24px;
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
export class GrievanceFormComponent implements HasUnsavedChanges {
  /** `:id` route parameter (absent for a new grievance). */
  readonly id = input<string>();

  private readonly grievances = inject(MyGrievanceDataService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiNotificationService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly user = inject(CurrentUserService);
  protected readonly apiMode = inject(SERVER_SESSION);

  protected readonly limits = GRIEVANCE_LIMITS;
  protected readonly today = todayIso();
  protected readonly hrSees = ANONYMOUS_HR_SEES;
  protected readonly hrDoesNotSee = ANONYMOUS_HR_DOES_NOT_SEE;
  protected readonly risks = ANONYMOUS_RESIDUAL_RISKS;
  protected readonly notAllowed = ANONYMOUS_NOT_ALLOWED;
  protected readonly fileWarning = ANONYMOUS_FILE_WARNING;
  protected readonly fixedNote = ANONYMITY_FIXED_NOTE;

  protected readonly me = computed(() => this.user.current());
  protected readonly linked = computed(() => {
    const id = this.me().employeeId;
    return id !== null && id > 0;
  });
  protected readonly draftId = computed(() => {
    const raw = this.id();
    return raw ? Number(raw) : null;
  });

  readonly form: GrievanceForm = new FormGroup({
    categoryId: new FormControl<number | null>(null, Validators.required),
    subject: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        HrmsValidators.notBlank,
        Validators.maxLength(GRIEVANCE_LIMITS.subjectMax),
      ],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(GRIEVANCE_LIMITS.descriptionMax)],
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
    isAnonymous: new FormControl(false, { nonNullable: true }),
    declaration: new FormControl(false, { nonNullable: true }),
  });

  protected readonly loaded = rxResource({
    params: () => (this.linked() ? { id: this.draftId() } : undefined),
    stream: ({ params }) =>
      forkJoin({
        options: this.grievances.options(),
        draft: params.id ? this.grievances.getById(params.id) : of(null),
      }),
  });
  protected readonly loadError = computed(() =>
    this.loaded.error() ? errorMessage(this.loaded.error()) : '',
  );
  protected readonly notDraft = computed(() => {
    const draft = safeValue(this.loaded)?.draft;
    return !!draft && draft.status !== 'Draft';
  });

  private readonly categoryId = toSignal(this.form.controls.categoryId.valueChanges, {
    initialValue: null,
  });
  protected readonly anonymous = toSignal(this.form.controls.isAnonymous.valueChanges, {
    initialValue: false,
  });
  protected readonly category = computed<GrievanceCategoryOption | undefined>(() =>
    safeValue(this.loaded)?.options.categories.find((c) => c.id === this.categoryId()),
  );
  protected readonly anonymousBlocked = computed(
    () => this.category() !== undefined && !this.category()!.allowsAnonymous,
  );
  /** An existing anonymous draft: categories that refuse anonymity cannot be chosen. */
  protected readonly lockedAnonymous = computed(() => this.draftId() !== null && this.anonymous());

  /** The draft's row version once it exists (a new grievance gets one at the first save). */
  private readonly saved = signal<{ id: number; rowVersion: string } | null>(null);
  private readonly existingFiles = signal<GrievanceFileItem[]>([]);
  private readonly pendingFiles = signal<{ item: GrievanceFileItem; file: File }[]>([]);
  protected readonly files = computed(() => [
    ...this.existingFiles(),
    ...this.pendingFiles().map((p) => p.item),
  ]);
  protected readonly fileError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly saving = signal(false);
  /** Set once the work is saved and the screen is left on purpose (the guard lets it go). */
  private leaving = false;

  protected readonly title = computed(() =>
    this.draftId() ? 'Edit grievance draft' : 'New grievance',
  );
  protected readonly description = computed(() =>
    this.draftId()
      ? 'Finish your draft and submit it when it is ready.'
      : 'Tell HR what happened. You can save a draft and come back to it before submitting.',
  );
  protected readonly backLink = computed(() =>
    this.draftId() ? ['/grievances/my', this.draftId()] : ['/grievances/my'],
  );

  constructor() {
    // Fill the form once the draft is loaded.
    effect(() => {
      const draft = safeValue(this.loaded)?.draft;
      if (draft && draft.status === 'Draft') {
        untracked(() => this.fill(draft));
      }
    });
    // Anonymity only where the category accepts it: switched off and locked otherwise. On an
    // existing draft the choice is fixed (the server refuses a change), so it is never touched.
    effect(() => {
      const blocked = this.anonymousBlocked();
      if (this.draftId() !== null) return;
      untracked(() => {
        const control = this.form.controls.isAnonymous;
        if (blocked) {
          if (control.value) {
            control.setValue(false);
            control.markAsDirty();
          }
          control.disable({ emitEvent: false });
        } else {
          control.enable({ emitEvent: false });
        }
      });
    });
  }

  hasUnsavedChanges(): boolean {
    return !this.leaving && (this.form.dirty || this.pendingFiles().length > 0);
  }

  private fill(draft: MyGrievanceDetail): void {
    this.form.reset({
      categoryId: draft.categoryId,
      subject: draft.subject,
      description: draft.description,
      incidentDate: draft.incidentDate,
      location: draft.location ?? '',
      involvedPersonText: draft.involvedPersonText ?? '',
      involvedDepartmentId: draft.involvedDepartmentId,
      isAnonymous: draft.isAnonymous,
      declaration: false,
    });
    this.saved.set({ id: draft.id, rowVersion: draft.rowVersion });
    this.existingFiles.set(
      draft.attachments.map((a) => ({
        key: `file-${a.id}`,
        id: a.id,
        fileName: a.fileName,
        sizeBytes: a.sizeBytes,
        uploadedAtUtc: a.uploadedAtUtc,
        uploadedBy: a.uploadedBy,
      })),
    );
  }

  // ---- Files ------------------------------------------------------------------------------------

  protected addFile(file: File): void {
    this.fileError.set(null);
    this.pendingFiles.update((list) => [
      ...list,
      {
        file,
        item: {
          key: `pending-${++pendingKey}`,
          id: null,
          fileName: file.name,
          sizeBytes: file.size,
          uploadedAtUtc: null,
          uploadedBy: null,
        },
      },
    ]);
  }

  protected removeFile(item: GrievanceFileItem): void {
    this.pendingFiles.update((list) => list.filter((p) => p.item.key !== item.key));
  }

  protected download(item: GrievanceFileItem): void {
    if (item.id === null) return;
    this.grievances.downloadAttachment(item.id).subscribe({
      next: (file) => downloadFile(file),
      error: (e: unknown) => this.toast.error(e),
    });
  }

  // ---- Validation -------------------------------------------------------------------------------

  /** Switches the submit-only rules on (submit) or off (save draft) and checks the form. */
  private validFor(mode: 'draft' | 'submit'): boolean {
    for (const [name, rules] of Object.entries(SUBMIT_RULES)) {
      const control = this.form.get(name) as AbstractControl;
      if (mode === 'submit') {
        control.addValidators(rules);
      } else {
        control.removeValidators(rules);
      }
      control.updateValueAndValidity({ emitEvent: false });
    }
    this.form.markAllAsTouched();
    this.formError.set(null);
    if (this.form.invalid) {
      this.formError.set(
        mode === 'submit'
          ? 'Please complete the highlighted fields before submitting.'
          : 'A draft needs a category and a subject.',
      );
      return false;
    }
    return true;
  }

  private input(): GrievanceDraftInput {
    const v = this.form.getRawValue();
    const blank = (text: string) => (text.trim() === '' ? null : text.trim());
    return {
      categoryId: v.categoryId!,
      subject: v.subject.trim(),
      description: v.description.trim(),
      incidentDate: v.incidentDate || null,
      location: blank(v.location),
      involvedPersonText: blank(v.involvedPersonText),
      involvedDepartmentId: v.involvedDepartmentId,
      isAnonymous: this.anonymousBlocked() ? false : v.isAnonymous,
    };
  }

  /** Creates or updates the draft, then uploads the files chosen meanwhile. */
  private persist(): Observable<{ id: number; rowVersion: string }> {
    const current = this.saved();
    const save$ = current
      ? this.grievances.updateDraft(current.id, { ...this.input(), rowVersion: current.rowVersion })
      : this.grievances.createDraft(this.input());
    return save$.pipe(
      switchMap((detail) => {
        const state = { id: detail.id, rowVersion: detail.rowVersion };
        this.saved.set(state);
        const pending = this.pendingFiles();
        if (pending.length === 0) {
          return of(state);
        }
        return from(pending).pipe(
          concatMap((p) =>
            this.grievances.uploadAttachment(state.id, state.rowVersion, p.file).pipe(
              map((result) => {
                state.rowVersion = result.caseRowVersion;
                this.pendingFiles.update((list) => list.filter((x) => x.item.key !== p.item.key));
                this.existingFiles.update((list) => [
                  ...list,
                  {
                    key: `file-${result.attachment.id}`,
                    id: result.attachment.id,
                    fileName: result.attachment.fileName,
                    sizeBytes: result.attachment.sizeBytes,
                    uploadedAtUtc: result.attachment.uploadedAtUtc,
                    uploadedBy: result.attachment.uploadedBy,
                  },
                ]);
                this.saved.set({ ...state });
                return state;
              }),
            ),
          ),
          last(),
        );
      }),
    );
  }

  /** Puts a server refusal on the field it names, and says it. */
  private fail(error: unknown): void {
    if (isAppError(error) && error.fieldErrors) {
      for (const [field, message] of Object.entries(error.fieldErrors)) {
        if (field === 'file') {
          this.fileError.set(message);
          continue;
        }
        const control = SERVER_FIELDS[field];
        if (control) {
          this.form.controls[control].setErrors({ server: message });
          this.form.controls[control].markAsTouched();
        }
      }
    }
    this.formError.set(errorMessage(error));
    this.toast.error(error);
  }

  // ---- Actions ----------------------------------------------------------------------------------

  protected saveDraft(): void {
    if (this.saving() || !this.validFor('draft')) return;
    const isNew = this.saved() === null;
    this.saving.set(true);
    this.persist()
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (state) => {
          this.form.markAsPristine();
          this.toast.success('Draft saved. Nothing has been sent to HR yet.');
          if (isNew) {
            this.leaving = true;
            void this.router.navigate(['/grievances/my', state.id, 'edit'], { replaceUrl: true });
          }
        },
        error: (e: unknown) => this.fail(e),
      });
  }

  protected submit(): void {
    if (this.saving() || !this.validFor('submit')) return;
    const anonymous = this.input().isAnonymous;
    this.confirm
      .confirm({
        title: 'Submit this grievance?',
        message: anonymous
          ? 'HR will receive it without your name. You cannot edit it after submitting.'
          : 'HR will receive it with your name. You cannot edit it after submitting.',
        confirmText: 'Submit',
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.saving.set(true);
          return this.persist().pipe(
            switchMap((state) =>
              this.grievances.submit(state.id, {
                rowVersion: state.rowVersion,
                declarationAccepted: true,
              }),
            ),
            finalize(() => this.saving.set(false)),
          );
        }),
      )
      .subscribe({
        next: (submitted) => {
          this.leaving = true;
          this.form.markAsPristine();
          this.toast.success(
            submitted.caseNo ? `Grievance ${submitted.caseNo} submitted.` : 'Grievance submitted.',
          );
          void this.router.navigate(['/grievances/my', submitted.id]);
        },
        error: (e: unknown) => this.fail(e),
      });
  }
}
