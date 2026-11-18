import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Observable, finalize } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { GRIEVANCE_PRIORITIES, GrievancePriority } from '../../../core/models/grievance.models';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';

/** What the dialog collected. Fields it did not show keep their defaults. */
export interface GrievanceCommandValues {
  text: string | null;
  priority: GrievancePriority | null;
  targetDate: string | null;
  isImmediate: boolean;
  visibleToSubmitter: boolean;
}

/**
 * One state-changing command: the confirmation and its inputs in one dialog. The dialog runs the
 * command (`run`), so a server refusal stays in the dialog: field errors land on their field, a
 * Conflict (status changed, stale data) offers "Reload".
 */
export interface GrievanceCommandDialogData {
  title: string;
  message: string;
  confirmText: string;
  destructive?: boolean;
  /** A text input: its server field name is `field` (reason, comment, message or summary). */
  text?: {
    field: string;
    label: string;
    required: boolean;
    max: number;
    hint?: string;
  };
  /** Priority select, with the initial choice. */
  priority?: { initial: GrievancePriority | null };
  /** Target date (not before today), with the initial value and a hint. */
  targetDate?: { initial: string | null; hint: string };
  /** "Immediate escalation" checkbox. */
  isImmediate?: boolean;
  /** "Share the resolution with the employee" switch (on by default). */
  visibleToSubmitter?: boolean;
  /** Extra explanation shown above the inputs. */
  note?: string;
  run: (values: GrievanceCommandValues) => Observable<unknown>;
}

/** What the dialog closes with: the command's answer, or a request to reload the case. */
export type GrievanceCommandDialogResult = { done: unknown } | { reload: true };

@Component({
  selector: 'app-grievance-command-dialog',
  imports: [
    FieldErrorComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="confirm()" novalidate>
      <mat-dialog-content class="content">
        <p class="message">{{ data.message }}</p>
        @if (data.note) {
          <p class="note" role="note">{{ data.note }}</p>
        }
        @if (data.priority) {
          <mat-form-field class="full">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority" required>
              @for (p of priorities; track p) {
                <mat-option [value]="p">{{ p }}</mat-option>
              }
            </mat-select>
            @if (form.controls.priority.value === 'Critical') {
              <mat-hint>A Critical case is escalated to the reviewers at once.</mat-hint>
            }
            <mat-error
              ><app-field-error [control]="form.controls.priority" label="Priority"
            /></mat-error>
          </mat-form-field>
        }
        @if (data.targetDate) {
          <mat-form-field class="full">
            <mat-label>Target resolution date</mat-label>
            <input
              matInput
              formControlName="targetDate"
              [matDatepicker]="targetPicker"
              [min]="today"
            />
            <mat-datepicker-toggle matIconSuffix [for]="targetPicker" />
            <mat-datepicker #targetPicker />
            <mat-hint>{{ data.targetDate.hint }}</mat-hint>
            <mat-error
              ><app-field-error [control]="form.controls.targetDate" label="Target date"
            /></mat-error>
          </mat-form-field>
        }
        @if (data.text; as t) {
          <mat-form-field class="full">
            <mat-label>{{ t.label }}</mat-label>
            <textarea
              matInput
              formControlName="text"
              rows="4"
              [maxlength]="t.max"
              [required]="t.required"
            ></textarea>
            @if (t.hint) {
              <mat-hint>{{ t.hint }}</mat-hint>
            }
            <mat-hint align="end">{{ form.controls.text.value.length }} / {{ t.max }}</mat-hint>
            <mat-error
              ><app-field-error [control]="form.controls.text" [label]="t.label"
            /></mat-error>
          </mat-form-field>
        }
        @if (data.isImmediate) {
          <mat-checkbox formControlName="isImmediate">
            Immediate: the reviewers must act at once
          </mat-checkbox>
        }
        @if (data.visibleToSubmitter) {
          <mat-slide-toggle formControlName="visibleToSubmitter">
            Share the resolution summary with the employee
          </mat-slide-toggle>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" aria-label="Working" />
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" [disabled]="busy()" (click)="ref.close()">Cancel</button>
        @if (conflict()) {
          <button mat-stroked-button type="button" (click)="ref.close({ reload: true })">
            <mat-icon>refresh</mat-icon>Reload the case
          </button>
        }
        <button
          mat-flat-button
          type="submit"
          [class.destructive]="data.destructive"
          [disabled]="busy()"
        >
          {{ data.confirmText }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: min(420px, 80vw);
    }
    .message,
    .note {
      margin: 0;
    }
    .note {
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .full {
      width: 100%;
    }
    .error {
      margin: 0;
      color: var(--mat-sys-error);
    }
    .destructive {
      --mdc-filled-button-container-color: var(--mat-sys-error);
      --mat-button-filled-container-color: var(--mat-sys-error);
    }
  `,
})
export class GrievanceCommandDialogComponent {
  protected readonly data = inject<GrievanceCommandDialogData>(MAT_DIALOG_DATA);
  protected readonly ref =
    inject<MatDialogRef<GrievanceCommandDialogComponent, GrievanceCommandDialogResult>>(
      MatDialogRef,
    );
  protected readonly priorities = GRIEVANCE_PRIORITIES;
  protected readonly today = todayIso();
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly conflict = signal(false);

  readonly form = new FormGroup({
    text: new FormControl('', {
      nonNullable: true,
      validators: [
        ...(this.data.text?.required ? [Validators.required, HrmsValidators.notBlank] : []),
        Validators.maxLength(this.data.text?.max ?? 4000),
      ],
    }),
    priority: new FormControl<GrievancePriority | null>(
      this.data.priority?.initial ?? null,
      this.data.priority ? Validators.required : [],
    ),
    targetDate: new FormControl<string | null>(this.data.targetDate?.initial ?? null, (c) =>
      c.value && c.value < todayIso() ? { rule: 'The target date cannot be in the past.' } : null,
    ),
    isImmediate: new FormControl(false, { nonNullable: true }),
    visibleToSubmitter: new FormControl(true, { nonNullable: true }),
  });

  /** Validates, runs the command, and closes on success. */
  confirm(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const text = v.text.trim();
    this.error.set(null);
    this.conflict.set(false);
    this.busy.set(true);
    this.data
      .run({
        text: text === '' ? null : text,
        priority: v.priority,
        targetDate: v.targetDate || null,
        isImmediate: v.isImmediate,
        visibleToSubmitter: v.visibleToSubmitter,
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (done) => this.ref.close({ done }),
        error: (e: unknown) => this.show(e),
      });
  }

  private show(error: unknown): void {
    if (isAppError(error) && error.code === 'Conflict') {
      this.conflict.set(true);
    }
    let placed = false;
    if (isAppError(error) && error.fieldErrors) {
      for (const [field, message] of Object.entries(error.fieldErrors)) {
        const control =
          field === this.data.text?.field
            ? this.form.controls.text
            : field === 'priority'
              ? this.form.controls.priority
              : field === 'targetResolutionDate'
                ? this.form.controls.targetDate
                : null;
        if (control) {
          control.setErrors({ server: message });
          placed = true;
        }
      }
    }
    if (!placed) {
      this.error.set(errorMessage(error));
    }
  }
}
