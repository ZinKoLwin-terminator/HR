import { Component, input, output, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  GRIEVANCE_CORRECTIVE_ACTION_STATUSES,
  GRIEVANCE_LIMITS,
  GrievanceCorrectiveAction,
  GrievanceCorrectiveActionInput,
  GrievanceCorrectiveActionStatus,
  GrievanceCorrectiveActionUpdate,
} from '../../../core/models/grievance.models';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';

/** A new action, without the case's row version (the parent adds it). */
export type GrievanceActionDraft = Omit<GrievanceCorrectiveActionInput, 'rowVersion'>;

/**
 * HR's corrective actions on a case (management only): the list, a status change per action (an
 * Implemented action needs its completion date) and a form to add one. Each action has its own row
 * version, which the parent sends with the update.
 */
@Component({
  selector: 'app-grievance-corrective-actions',
  imports: [
    AppDatePipe,
    FieldErrorComponent,
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    StatusBadgeComponent,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    @if (actions().length === 0) {
      <p class="muted">No corrective action yet.</p>
    } @else {
      <ul class="actions-list">
        @for (a of actions(); track a.id) {
          <li class="item">
            <div class="item__head">
              <strong>{{ a.actionType }}</strong>
              <app-status-badge [status]="a.status" />
            </div>
            <p class="body">{{ a.description }}</p>
            <p class="meta">
              {{ a.responsibleText ?? 'No one named' }} · due
              {{ a.dueDate ? (a.dueDate | appDate) : '—' }}
              @if (a.completedOn) {
                · completed {{ a.completedOn | appDate }}
              }
            </p>
            @if (canEdit()) {
              <div class="update">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Status</mat-label>
                  <mat-select
                    [ngModel]="edits()[a.id]?.status ?? a.status"
                    (ngModelChange)="edit(a, { status: $event })"
                  >
                    @for (s of statuses; track s) {
                      <mat-option [value]="s">{{ s }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Completed on</mat-label>
                  <input
                    matInput
                    [matDatepicker]="donePicker"
                    [max]="today"
                    [ngModel]="edits()[a.id]?.completedOn ?? a.completedOn"
                    (dateChange)="edit(a, { completedOn: $event.value })"
                  />
                  <mat-datepicker-toggle matIconSuffix [for]="donePicker" />
                  <mat-datepicker #donePicker />
                </mat-form-field>
                <button
                  mat-stroked-button
                  type="button"
                  [disabled]="busy() || !edits()[a.id]"
                  (click)="save(a)"
                >
                  Save
                </button>
              </div>
            }
          </li>
        }
      </ul>
    }

    @if (canEdit()) {
      <form class="add" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h3>Add a corrective action</h3>
        <div class="row">
          <mat-form-field>
            <mat-label>Type</mat-label>
            <input matInput formControlName="actionType" maxlength="100" />
            <mat-hint>For example Training, Policy change, Mediation.</mat-hint>
            <mat-error
              ><app-field-error [control]="form.controls.actionType" label="Type"
            /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Responsible</mat-label>
            <input matInput formControlName="responsibleText" [maxlength]="limits.shortTextMax" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Due date</mat-label>
            <input matInput formControlName="dueDate" [matDatepicker]="duePicker" />
            <mat-datepicker-toggle matIconSuffix [for]="duePicker" />
            <mat-datepicker #duePicker />
          </mat-form-field>
        </div>
        <mat-form-field class="full">
          <mat-label>Description</mat-label>
          <textarea
            matInput
            formControlName="description"
            rows="3"
            [maxlength]="limits.reasonMax"
          ></textarea>
          <mat-error
            ><app-field-error [control]="form.controls.description" label="Description"
          /></mat-error>
        </mat-form-field>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
        <div class="form-actions">
          <button mat-flat-button type="submit" [disabled]="busy()">
            <mat-icon>add_task</mat-icon>Add action
          </button>
        </div>
      </form>
    }
  `,
  styles: `
    .actions-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin: 0 0 16px;
      padding: 0;
      list-style: none;
    }
    .item {
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface-container);
    }
    .item__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .body,
    .meta,
    .muted,
    .error {
      margin: 0;
    }
    .meta,
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .update {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-top: 8px;
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
    .form-actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class GrievanceCorrectiveActionsComponent {
  readonly actions = input.required<GrievanceCorrectiveAction[]>();
  readonly canEdit = input(false);
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly add = output<GrievanceActionDraft>();
  readonly update = output<{ actionId: number; changes: GrievanceCorrectiveActionUpdate }>();

  protected readonly statuses = GRIEVANCE_CORRECTIVE_ACTION_STATUSES;
  protected readonly limits = GRIEVANCE_LIMITS;
  protected readonly today = todayIso();
  protected readonly edits = signal<
    Record<number, { status: GrievanceCorrectiveActionStatus; completedOn: string | null }>
  >({});

  readonly form = new FormGroup({
    actionType: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, HrmsValidators.notBlank, Validators.maxLength(100)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        HrmsValidators.notBlank,
        Validators.maxLength(GRIEVANCE_LIMITS.reasonMax),
      ],
    }),
    responsibleText: new FormControl('', { nonNullable: true }),
    dueDate: new FormControl<string | null>(null),
  });

  protected edit(
    action: GrievanceCorrectiveAction,
    change: Partial<{ status: GrievanceCorrectiveActionStatus; completedOn: string | null }>,
  ): void {
    this.edits.update((all) => ({
      ...all,
      [action.id]: {
        status: all[action.id]?.status ?? action.status,
        completedOn: all[action.id]?.completedOn ?? action.completedOn,
        ...change,
      },
    }));
  }

  protected save(action: GrievanceCorrectiveAction): void {
    const pending = this.edits()[action.id];
    if (!pending) return;
    this.update.emit({
      actionId: action.id,
      changes: {
        rowVersion: action.rowVersion,
        actionType: action.actionType,
        description: action.description,
        responsibleText: action.responsibleText,
        dueDate: action.dueDate,
        completedOn: pending.completedOn,
        status: pending.status,
      },
    });
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;
    const v = this.form.getRawValue();
    this.add.emit({
      actionType: v.actionType.trim(),
      description: v.description.trim(),
      responsibleText: v.responsibleText.trim() || null,
      dueDate: v.dueDate || null,
    });
  }

  /** Clears the add form, or the pending edit of one action, after the parent saved. */
  saved(actionId: number | null): void {
    if (actionId === null) {
      this.form.reset();
    } else {
      this.edits.update((all) => {
        const next = { ...all };
        delete next[actionId];
        return next;
      });
    }
  }
}
