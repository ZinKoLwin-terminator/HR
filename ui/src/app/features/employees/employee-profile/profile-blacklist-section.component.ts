import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { filter } from 'rxjs';
import {
  BLACKLIST_EDIT_PERMISSION,
  BLACKLIST_REASON_MAX_LENGTH,
  BLACKLIST_VIEW_PERMISSION,
  BlacklistEntry,
} from '../../../core/models/blacklist.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { BlacklistDataService } from '../../../core/services/data/blacklist-data.service';
import { PermissionService } from '../../../core/services/permission.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';

/**
 * Do-not-rehire section of a FORMER employee's profile (one with an exit date).
 *
 *  - Needs `employees:ViewBlacklist` to see the entry in force (and its reason), and
 *    `employees:EditBlacklist` to add or lift. Without either, nothing is rendered and nothing is
 *    requested. UX only: the server enforces both keys.
 *  - "Add to do-not-rehire list" is offered only when the employee has an exit date and no entry in
 *    force. A holder of EditBlacklist without ViewBlacklist cannot read entries, so for them the
 *    button is offered on any former employee and the server answers 409 if one exists.
 *  - The reason is shown here and on the Do-not-rehire list screen only. It is not printed.
 */
@Component({
  selector: 'app-profile-blacklist-section',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    @if (visible()) {
      <section class="card blacklist no-print" aria-labelledby="blacklist-heading">
        <div class="blacklist__head">
          <mat-icon aria-hidden="true">block</mat-icon>
          <h2 id="blacklist-heading">Do-not-rehire list</h2>
        </div>

        @if (canView() && entry.isLoading() && !active()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading do-not-rehire status" />
        } @else if (canView() && entry.error()) {
          <p class="blacklist__error" role="alert">{{ errorText() }}</p>
          <button mat-stroked-button type="button" (click)="entry.reload()">Try again</button>
        } @else if (active(); as e) {
          <p class="blacklist__status">
            On the do-not-rehire list since {{ e.blacklistedOn | date }}, added by
            {{ e.blacklistedBy }}.
          </p>
          <dl class="blacklist__reason">
            <dt>Reason</dt>
            <dd>{{ e.reason }}</dd>
          </dl>
          @if (canEdit()) {
            <div class="blacklist__actions">
              <button mat-stroked-button type="button" (click)="lift(e)">
                <mat-icon>lock_open</mat-icon>Lift entry
              </button>
            </div>
          }
        } @else {
          @if (canView()) {
            <p class="blacklist__status">Not on the do-not-rehire list.</p>
          } @else {
            <p class="blacklist__status">
              You can add this former employee to the do-not-rehire list.
            </p>
          }
          @if (canAdd()) {
            <div class="blacklist__actions">
              <button
                mat-stroked-button
                type="button"
                [attr.aria-label]="'Add ' + employeeName() + ' to the do-not-rehire list'"
                (click)="add()"
              >
                <mat-icon>person_off</mat-icon>Add to do-not-rehire list
              </button>
            </div>
          }
        }
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .blacklist {
      margin-bottom: 12px;
      padding: 12px 18px;
    }
    .blacklist__head {
      display: flex;
      align-items: center;
      gap: 8px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-medium);
      }
    }
    .blacklist__status {
      margin: 8px 0 0;
    }
    .blacklist__error {
      margin: 8px 0;
      color: var(--mat-sys-error);
    }
    .blacklist__reason {
      margin: 8px 0 0;
      dt {
        font: var(--mat-sys-label-medium);
        color: var(--mat-sys-on-surface-variant);
      }
      dd {
        margin: 2px 0 0;
        overflow-wrap: anywhere;
      }
    }
    .blacklist__actions {
      margin-top: 10px;
    }
  `,
})
export class ProfileBlacklistSectionComponent {
  private readonly api = inject(BlacklistDataService);
  private readonly permissions = inject(PermissionService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);

  readonly employeeId = input.required<number>();
  readonly employeeName = input.required<string>();
  /** ISO date; the employee is a former employee when set. */
  readonly exitDate = input<string | null>(null);

  protected readonly canView = computed(() => this.permissions.can(BLACKLIST_VIEW_PERMISSION));
  protected readonly canEdit = computed(() => this.permissions.can(BLACKLIST_EDIT_PERMISSION));
  protected readonly isFormer = computed(() => !!this.exitDate());
  protected readonly visible = computed(
    () => this.isFormer() && (this.canView() || this.canEdit()),
  );

  protected readonly entry = rxResource({
    params: () =>
      this.visible() && this.canView() ? { employeeId: this.employeeId() } : undefined,
    stream: ({ params }) => this.api.getActiveForEmployee(params.employeeId),
  });
  protected readonly active = computed<BlacklistEntry | null>(() => this.entry.value() ?? null);
  protected readonly errorText = computed(() =>
    this.entry.error() ? errorMessage(this.entry.error()) : '',
  );

  /** Former employee, may edit, and no entry in force (when the user can see entries). */
  protected readonly canAdd = computed(
    () => this.isFormer() && this.canEdit() && !(this.canView() && this.active()),
  );

  protected add(): void {
    this.confirm
      .prompt({
        title: 'Add to do-not-rehire list?',
        message: `${this.employeeName()} will be flagged when they appear as a recruitment candidate. Only people with access to this list can read the reason.`,
        label: 'Reason',
        required: true,
        confirmText: 'Add to list',
        destructive: true,
        maxLength: BLACKLIST_REASON_MAX_LENGTH,
      })
      .pipe(filter((reason): reason is string => reason !== null))
      .subscribe((reason) =>
        this.api.add(this.employeeId(), reason).subscribe({
          next: () => {
            this.toast.success(`${this.employeeName()} was added to the do-not-rehire list.`);
            this.entry.reload();
          },
          error: (error: unknown) => this.toast.error(error),
        }),
      );
  }

  protected lift(entry: BlacklistEntry): void {
    this.confirm
      .prompt({
        title: 'Lift do-not-rehire entry?',
        message: `${this.employeeName()} will no longer be flagged. The entry stays in the list as history.`,
        label: 'Reason for lifting',
        required: true,
        confirmText: 'Lift entry',
        maxLength: BLACKLIST_REASON_MAX_LENGTH,
      })
      .pipe(filter((reason): reason is string => reason !== null))
      .subscribe((reason) =>
        this.api.lift(entry.id, reason, entry.rowVersion).subscribe({
          next: () => {
            this.toast.success(`The do-not-rehire entry for ${this.employeeName()} was lifted.`);
            this.entry.reload();
          },
          error: (error: unknown) => this.toast.error(error),
        }),
      );
  }
}
