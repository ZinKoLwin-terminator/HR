import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import {
  GRIEVANCE_LIMITS,
  GrievanceAssignmentRole,
  GrievanceCaseDetail,
} from '../../../core/models/grievance.models';
import { GrievanceCaseDataService } from '../../../core/services/data/grievance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';

export interface GrievanceAssignDialogData {
  detail: GrievanceCaseDetail;
}

/** Closes with the updated case, or `{ reload: true }` after a conflict. */
export type GrievanceAssignDialogResult = GrievanceCaseDetail | { reload: true };

/**
 * Case Assignment: choose an investigator (with their open case count) and Lead or Member. A reason
 * is required when the Lead of a case already under way is replaced. The server refuses a conflict
 * of interest with one generic message, shown on the investigator field; it never says why.
 */
@Component({
  selector: 'app-grievance-assign-dialog',
  imports: [
    FieldErrorComponent,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Assign an investigator</h2>
    <form [formGroup]="form" (ngSubmit)="confirm()" novalidate>
      <mat-dialog-content class="content">
        <p class="message">
          {{ data.detail.caseNo }}: the investigator sees the case, its messages and its case-team
          files.
        </p>
        @if (investigators.isLoading()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading investigators" />
        }
        <mat-form-field class="full">
          <mat-label>Investigator</mat-label>
          <mat-select formControlName="investigatorEmployeeId" required>
            @for (
              o of (investigators.hasValue() ? investigators.value() : undefined) ?? [];
              track o.employeeId
            ) {
              <mat-option [value]="o.employeeId">
                {{ o.displayName }} ({{ o.employeeCode }}) · {{ o.activeCaseCount }} open
                {{ o.activeCaseCount === 1 ? 'case' : 'cases' }}
              </mat-option>
            }
          </mat-select>
          <mat-hint
            >Everyone who may investigate is listed; a conflict of interest is refused on
            saving.</mat-hint
          >
          <mat-error>
            <app-field-error
              [control]="form.controls.investigatorEmployeeId"
              label="Investigator"
            />
          </mat-error>
        </mat-form-field>

        <mat-radio-group formControlName="role" aria-label="Role on the case" class="roles">
          <mat-radio-button value="Lead">Lead investigator</mat-radio-button>
          <mat-radio-button value="Member">Member</mat-radio-button>
        </mat-radio-group>

        @if (replacingLead()) {
          <p class="note" role="note">
            {{ data.detail.leadInvestigatorName }} leads this case now and will be replaced. Say
            why.
          </p>
        }
        <mat-form-field class="full">
          <mat-label>{{
            replacingLead() ? 'Reason for replacing the lead' : 'Reason (optional)'
          }}</mat-label>
          <textarea matInput formControlName="reason" rows="3" [maxlength]="reasonMax"></textarea>
          <mat-error><app-field-error [control]="form.controls.reason" label="Reason" /></mat-error>
        </mat-form-field>

        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" aria-label="Assigning" />
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" [disabled]="busy()" (click)="ref.close()">Cancel</button>
        @if (conflict()) {
          <button mat-stroked-button type="button" (click)="ref.close({ reload: true })">
            <mat-icon>refresh</mat-icon>Reload the case
          </button>
        }
        <button mat-flat-button type="submit" [disabled]="busy()">Assign</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: min(460px, 80vw);
    }
    .message,
    .note,
    .error {
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
    .roles {
      display: flex;
      gap: 16px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class GrievanceAssignDialogComponent {
  protected readonly data = inject<GrievanceAssignDialogData>(MAT_DIALOG_DATA);
  protected readonly ref =
    inject<MatDialogRef<GrievanceAssignDialogComponent, GrievanceAssignDialogResult>>(MatDialogRef);
  private readonly cases = inject(GrievanceCaseDataService);

  protected readonly reasonMax = GRIEVANCE_LIMITS.reasonMax;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly conflict = signal(false);
  protected readonly investigators = rxResource({ stream: () => this.cases.investigators() });

  readonly form = new FormGroup({
    investigatorEmployeeId: new FormControl<number | null>(null, Validators.required),
    role: new FormControl<GrievanceAssignmentRole>(
      this.data.detail.leadInvestigatorEmployeeId === null ? 'Lead' : 'Member',
      { nonNullable: true },
    ),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(GRIEVANCE_LIMITS.reasonMax)],
    }),
  });

  private readonly role = toSignal(this.form.controls.role.valueChanges, {
    initialValue: this.form.controls.role.value,
  });
  /** A Lead replaced on a case under way keeps the status and needs a reason. */
  protected readonly replacingLead = computed(
    () =>
      this.role() === 'Lead' &&
      this.data.detail.leadInvestigatorEmployeeId !== null &&
      ['Assigned', 'UnderInvestigation', 'AwaitingEmployeeResponse'].includes(
        this.data.detail.status,
      ),
  );

  confirm(): void {
    if (this.busy()) return;
    const reason = this.form.controls.reason.value.trim();
    if (this.replacingLead() && reason === '') {
      this.form.controls.reason.setErrors({ required: true });
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.error.set(null);
    this.conflict.set(false);
    this.busy.set(true);
    const v = this.form.getRawValue();
    this.cases
      .assign(this.data.detail.id, {
        rowVersion: this.data.detail.rowVersion,
        investigatorEmployeeId: v.investigatorEmployeeId!,
        role: v.role,
        reason: reason === '' ? null : reason,
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (detail) => this.ref.close(detail),
        error: (e: unknown) => this.show(e),
      });
  }

  private show(error: unknown): void {
    if (isAppError(error) && error.code === 'Conflict') {
      this.conflict.set(true);
    }
    const fields = isAppError(error) ? (error.fieldErrors ?? {}) : {};
    let placed = false;
    for (const [field, message] of Object.entries(fields)) {
      const control =
        field === 'investigatorEmployeeId'
          ? this.form.controls.investigatorEmployeeId
          : field === 'reason'
            ? this.form.controls.reason
            : field === 'role'
              ? this.form.controls.role
              : null;
      if (control) {
        control.setErrors({ server: message });
        control.markAsTouched();
        placed = true;
      }
    }
    if (!placed) {
      this.error.set(errorMessage(error));
    }
  }
}
