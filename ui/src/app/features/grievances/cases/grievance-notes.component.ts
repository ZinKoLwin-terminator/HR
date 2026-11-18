import { Component, computed, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  GRIEVANCE_LIMITS,
  GRIEVANCE_NOTE_KINDS,
  GRIEVANCE_NOTE_KIND_LABELS,
  GrievanceNote,
  GrievanceNoteKind,
  GrievanceNoteRequest,
} from '../../../core/models/grievance.models';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';

/** A note to add, without the row version (the parent adds it). */
export type GrievanceNoteDraft = Omit<GrievanceNoteRequest, 'rowVersion'>;

/**
 * The investigation record: notes, interviews, follow-ups, findings and recommendations (append-only,
 * oldest first), and with `canAdd` a form to add one. A reviewer only receives findings and
 * recommendations from the server. Interview date and interviewee belong to Interview notes.
 */
@Component({
  selector: 'app-grievance-notes',
  imports: [
    AppDatePipe,
    FieldErrorComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    @if (notes().length === 0) {
      <p class="muted">No notes yet.</p>
    } @else {
      <ul class="notes">
        @for (n of notes(); track n.id) {
          <li class="note" [attr.data-kind]="n.kind">
            <p class="head">
              <strong>{{ kindLabels[n.kind] }}</strong> · {{ n.authorName }} ·
              {{ n.createdAtUtc | appDate }}
              @if (n.kind === 'Interview' && (n.intervieweeText || n.interviewDate)) {
                · {{ n.intervieweeText ?? 'Interview' }}
                @if (n.interviewDate) {
                  on {{ n.interviewDate | appDate }}
                }
              }
            </p>
            <p class="body">{{ n.body }}</p>
          </li>
        }
      </ul>
    }

    @if (canAdd()) {
      <form class="add" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h3>Add to the record</h3>
        <div class="row">
          <mat-form-field>
            <mat-label>Kind</mat-label>
            <mat-select formControlName="kind">
              @for (k of kinds(); track k) {
                <mat-option [value]="k">{{ kindLabels[k] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (interview()) {
            <mat-form-field>
              <mat-label>Interviewee</mat-label>
              <input matInput formControlName="intervieweeText" [maxlength]="limits.shortTextMax" />
              <mat-hint>A role or initials rather than a full name.</mat-hint>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Interview date</mat-label>
              <input
                matInput
                formControlName="interviewDate"
                [matDatepicker]="picker"
                [max]="today"
              />
              <mat-datepicker-toggle matIconSuffix [for]="picker" />
              <mat-datepicker #picker />
              <mat-error
                ><app-field-error [control]="form.controls.interviewDate" label="Interview date"
              /></mat-error>
            </mat-form-field>
          }
        </div>
        <mat-form-field class="full">
          <mat-label>Text</mat-label>
          <textarea
            matInput
            formControlName="body"
            rows="4"
            [maxlength]="limits.noteMax"
          ></textarea>
          <mat-error><app-field-error [control]="form.controls.body" label="Text" /></mat-error>
        </mat-form-field>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="busy()">
            <mat-icon>note_add</mat-icon>Add note
          </button>
        </div>
      </form>
    }
  `,
  styles: `
    .notes {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0 0 16px;
      padding: 0;
      list-style: none;
    }
    .note {
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface-container);
    }
    .note[data-kind='Finding'],
    .note[data-kind='Recommendation'] {
      box-shadow: inset 3px 0 0 var(--mat-sys-primary);
    }
    .head,
    .body,
    .muted,
    .error {
      margin: 0;
    }
    .head,
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .body {
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
    h3 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-small);
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 0 12px;
    }
    .full {
      width: 100%;
    }
    .error {
      color: var(--mat-sys-error);
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class GrievanceNotesComponent {
  readonly notes = input.required<GrievanceNote[]>();
  readonly canAdd = input(false);
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly add = output<GrievanceNoteDraft>();

  protected readonly kindLabels = GRIEVANCE_NOTE_KIND_LABELS;
  protected readonly kinds = signal<readonly GrievanceNoteKind[]>(GRIEVANCE_NOTE_KINDS);
  protected readonly limits = GRIEVANCE_LIMITS;
  protected readonly today = todayIso();

  readonly form = new FormGroup({
    kind: new FormControl<GrievanceNoteKind>('Note', { nonNullable: true }),
    body: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        HrmsValidators.notBlank,
        Validators.maxLength(GRIEVANCE_LIMITS.noteMax),
      ],
    }),
    interviewDate: new FormControl<string | null>(null, HrmsValidators.notInFuture),
    intervieweeText: new FormControl('', { nonNullable: true }),
  });
  private readonly kind = toSignal(this.form.controls.kind.valueChanges, { initialValue: 'Note' });
  protected readonly interview = computed(() => this.kind() === 'Interview');

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;
    const v = this.form.getRawValue();
    const interview = v.kind === 'Interview';
    this.add.emit({
      kind: v.kind,
      body: v.body.trim(),
      interviewDate: interview ? v.interviewDate || null : null,
      intervieweeText: interview ? v.intervieweeText.trim() || null : null,
    });
  }

  /** Clears the form after the parent saved the note. */
  reset(): void {
    this.form.reset({ kind: this.form.controls.kind.value });
  }
}
