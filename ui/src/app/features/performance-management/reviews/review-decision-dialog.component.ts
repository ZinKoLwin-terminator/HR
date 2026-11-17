import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Observable } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import { ScorecardDetail } from '../../../core/models/kpi.models';

export const REASON_MAX = 1000;
/** The server's wording for a blank return or reject reason. */
export const REASON_REQUIRED = 'A reason is required.';

export interface ReviewDecisionData {
  title: string;
  message: string;
  /** Label of the text box: "Reason" (required) or "Comment (optional)". */
  label: string;
  required: boolean;
  confirmText: string;
  destructive?: boolean;
  /** Sends the decision; the answer is the scorecard with its new row version. */
  send: (comment: string | null) => Observable<ScorecardDetail>;
}

/** Closed with the scorecard on success, or with the error the page should show (409, 403...). */
export type ReviewDecisionResult =
  { ok: true; card: ScorecardDetail } | { ok: false; error: unknown };

const notBlank = (control: AbstractControl<string>): ValidationErrors | null =>
  control.value.trim() === '' ? { blank: true } : null;

/**
 * Confirms a review decision (approve, return, reject, finalize) with its comment and sends it.
 * Return and reject need a reason of at most 1000 characters; the server's "A reason is required."
 * (400 on `comment`) is shown on the field. Any other failure closes the dialog and is shown by the
 * page (a conflict with Reload, a 403 as a message).
 */
@Component({
  selector: 'app-review-decision-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <!-- No [formGroup] here, so Angular would not stop the browser's native submit: handle it ourselves. -->
    <form (submit)="submit($event)" novalidate>
      <mat-dialog-content>
        <p class="message">{{ data.message }}</p>
        <mat-form-field class="full">
          <mat-label>{{ data.label }}</mat-label>
          <textarea
            matInput
            rows="4"
            [formControl]="text"
            [attr.maxlength]="max"
            [attr.aria-required]="data.required"
            cdkFocusInitial
          ></textarea>
          <mat-hint align="end">{{ text.value.length }} / {{ max }}</mat-hint>
          <mat-error>{{ error() }}</mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" [disabled]="sending()" (click)="cancel()">Cancel</button>
        <button
          mat-flat-button
          type="submit"
          [class.danger-button]="data.destructive"
          [disabled]="sending()"
        >
          @if (sending()) {
            <mat-progress-spinner mode="indeterminate" diameter="18" aria-label="Sending" />
          }
          {{ data.confirmText }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .message {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .full {
      width: 100%;
    }
    mat-progress-spinner {
      display: inline-block;
      margin-inline-end: 6px;
      vertical-align: middle;
    }
  `,
})
export class ReviewDecisionDialogComponent {
  protected readonly data = inject<ReviewDecisionData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ReviewDecisionDialogComponent, ReviewDecisionResult>>(MatDialogRef);

  protected readonly max = REASON_MAX;
  protected readonly sending = signal(false);
  protected readonly text = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(REASON_MAX), ...(this.data.required ? [notBlank] : [])],
  });

  protected error(): string {
    const errors = this.text.errors ?? {};
    if (errors['server']) return String(errors['server']);
    if (errors['blank']) return REASON_REQUIRED;
    if (errors['maxlength']) return `At most ${REASON_MAX} characters.`;
    return '';
  }

  protected cancel(): void {
    this.dialogRef.close();
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.sending()) return;
    this.text.markAsTouched();
    if (this.text.invalid) return;
    const comment = this.text.value.trim();
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.data.send(comment === '' ? null : comment).subscribe({
      next: (card) => this.dialogRef.close({ ok: true, card }),
      error: (error: unknown) => {
        this.sending.set(false);
        this.dialogRef.disableClose = false;
        const onComment = isAppError(error) ? error.fieldErrors?.['comment'] : undefined;
        if (onComment) {
          this.text.setErrors({ server: onComment });
          this.text.markAsTouched();
        } else {
          this.dialogRef.close({ ok: false, error });
        }
      },
    });
  }
}
