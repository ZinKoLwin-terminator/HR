import { Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { GRIEVANCE_LIMITS, GrievanceCaseDetail } from '../../../core/models/grievance.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import {
  GrievanceCaseDataService,
  GrievanceCategoryDataService,
} from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import {
  GrievanceWorkflowAction,
  grievanceAvailableActions,
  grievanceProposerBlocked,
  grievanceStateOf,
  grievanceViewerRights,
} from '../../../core/utilities/grievance-workflow';
import { addDays, todayIso } from '../../../shared/utilities/date.util';
import {
  GrievanceAssignDialogComponent,
  GrievanceAssignDialogData,
  GrievanceAssignDialogResult,
} from './grievance-assign-dialog.component';
import {
  GrievanceCommandDialogComponent,
  GrievanceCommandDialogData,
  GrievanceCommandDialogResult,
  GrievanceCommandValues,
} from './grievance-command-dialog.component';
import { safeValue } from '../../../shared/utilities/resource-value';

/** Where the bar is shown: each screen offers the commands of its tier. */
export type GrievanceWorkspace = 'management' | 'investigation' | 'review';

/** The commands a bar can offer, in the order shown. Messages and unassign live elsewhere. */
const BAR_ACTIONS: readonly GrievanceWorkflowAction[] = [
  'acknowledge',
  'startReview',
  'assignLead',
  'startInvestigation',
  'requestInfo',
  'proposeResolution',
  'approveResolution',
  'rejectResolution',
  'reject',
  'close',
  'reopen',
  'escalate',
  'archive',
];

/** The commands each workspace offers (the rights decide among them). */
const WORKSPACE_ACTIONS: Record<GrievanceWorkspace, readonly GrievanceWorkflowAction[]> = {
  management: BAR_ACTIONS,
  investigation: ['startInvestigation', 'requestInfo', 'proposeResolution', 'escalate'],
  review: ['approveResolution', 'rejectResolution'],
};

const LABELS: Record<string, { label: string; icon: string }> = {
  acknowledge: { label: 'Acknowledge', icon: 'mark_email_read' },
  startReview: { label: 'Start review', icon: 'rate_review' },
  assignLead: { label: 'Assign investigator', icon: 'person_add' },
  startInvestigation: { label: 'Start investigation', icon: 'manage_search' },
  requestInfo: { label: 'Request information', icon: 'help' },
  proposeResolution: { label: 'Propose resolution', icon: 'lightbulb' },
  approveResolution: { label: 'Approve resolution', icon: 'task_alt' },
  rejectResolution: { label: 'Send proposal back', icon: 'undo' },
  reject: { label: 'Reject', icon: 'block' },
  close: { label: 'Close', icon: 'inventory_2' },
  reopen: { label: 'Reopen', icon: 'replay' },
  escalate: { label: 'Escalate', icon: 'priority_high' },
  archive: { label: 'Archive', icon: 'archive' },
};

/**
 * The commands the signed-in case-team member may run on a case, as buttons: the list comes from
 * `grievanceAvailableActions` (never hard-coded per screen). Each opens a dialog that confirms,
 * collects its inputs and runs the command. Emits the updated case, or a reload request after a
 * conflict. Explains the two quiet refusals: legal hold (archive) and the proposer rule.
 */
@Component({
  selector: 'app-grievance-case-actions',
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (actions().length > 0 || legalHoldBlocksArchive() || proposerBlocked()) {
      <div class="bar" role="group" aria-label="Case actions">
        @for (a of actions(); track a) {
          <button
            [class.primary]="$first"
            [attr.data-action]="a"
            mat-stroked-button
            type="button"
            (click)="run(a)"
          >
            <mat-icon>{{ labels[a].icon }}</mat-icon
            >{{ labels[a].label }}
          </button>
        }
        @if (legalHoldBlocksArchive()) {
          <button mat-stroked-button type="button" disabled aria-describedby="legal-hold-note">
            <mat-icon>archive</mat-icon>Archive
          </button>
          <span class="note" id="legal-hold-note">
            Under legal hold: the case cannot be archived until the hold is lifted in Triage.
          </span>
        }
        @if (proposerBlocked()) {
          <span class="note" role="note">
            You proposed this resolution, so another reviewer has to approve or send it back.
          </span>
        }
      </div>
    }
  `,
  styles: `
    .bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-bottom: 16px;
    }
    .note {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GrievanceCaseActionsComponent {
  readonly detail = input.required<GrievanceCaseDetail>();
  readonly workspace = input<GrievanceWorkspace>('management');
  readonly changed = output<GrievanceCaseDetail>();
  readonly reload = output<void>();

  private readonly cases = inject(GrievanceCaseDataService);
  private readonly categories = inject(GrievanceCategoryDataService);
  private readonly permissions = inject(PermissionService);
  private readonly user = inject(CurrentUserService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(UiNotificationService);

  protected readonly labels = LABELS;

  private readonly myEmployeeId = computed(() => {
    const id = this.user.current().employeeId;
    return id !== null && id > 0 ? id : null;
  });
  protected readonly rights = computed(() =>
    grievanceViewerRights(this.detail(), (k) => this.permissions.can(k), this.myEmployeeId()),
  );
  private readonly state = computed(() => grievanceStateOf(this.detail()));
  protected readonly actions = computed(() => {
    const available = new Set(grievanceAvailableActions(this.state(), this.rights()));
    // Adding a Member is the same dialog as a Lead.
    if (available.has('assignMember')) available.add('assignLead');
    const offered = WORKSPACE_ACTIONS[this.workspace()];
    return BAR_ACTIONS.filter((a) => offered.includes(a) && available.has(a));
  });
  protected readonly legalHoldBlocksArchive = computed(() => {
    const d = this.detail();
    return (
      this.workspace() === 'management' &&
      this.rights().managementDelete &&
      d.legalHold &&
      d.archivedAtUtc === null &&
      (d.status === 'Closed' || d.status === 'Rejected')
    );
  });
  protected readonly proposerBlocked = computed(
    () =>
      this.workspace() !== 'investigation' && grievanceProposerBlocked(this.state(), this.rights()),
  );

  /** The categories' target days, for the acknowledgement default (management only). */
  private readonly categoryList = rxResource({
    params: () => (this.workspace() === 'management' ? {} : undefined),
    stream: () => this.categories.getAll(),
  });

  run(action: GrievanceWorkflowAction): void {
    if (action === 'assignLead' || action === 'assignMember') {
      this.assign();
      return;
    }
    const data = this.dialogFor(action);
    if (!data) return;
    this.dialog
      .open<
        GrievanceCommandDialogComponent,
        GrievanceCommandDialogData,
        GrievanceCommandDialogResult
      >(GrievanceCommandDialogComponent, {
        data,
        width: '520px',
        maxWidth: '95vw',
        role: data.destructive ? 'alertdialog' : 'dialog',
      })
      .afterClosed()
      .subscribe((result) => this.after(result, `${LABELS[action].label}: done.`));
  }

  private assign(): void {
    this.dialog
      .open<GrievanceAssignDialogComponent, GrievanceAssignDialogData, GrievanceAssignDialogResult>(
        GrievanceAssignDialogComponent,
        { data: { detail: this.detail() }, width: '560px', maxWidth: '95vw' },
      )
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        if ('reload' in result) {
          this.reload.emit();
          return;
        }
        this.toast.success('Investigator assigned.');
        this.changed.emit(result);
      });
  }

  private after(result: GrievanceCommandDialogResult | undefined, message: string): void {
    if (!result) return;
    if ('reload' in result) {
      this.reload.emit();
      return;
    }
    this.toast.success(message);
    this.changed.emit(result.done as GrievanceCaseDetail);
  }

  /** The dialog of each command: its confirmation text, inputs and call. */
  private dialogFor(action: GrievanceWorkflowAction): GrievanceCommandDialogData | null {
    const d = this.detail();
    const rv = d.rowVersion;
    const id = d.id;
    const reason = (label: string, hint?: string) => ({
      field: 'reason',
      label,
      required: true,
      max: GRIEVANCE_LIMITS.reasonMax,
      hint,
    });
    const comment = {
      field: 'comment',
      label: 'Comment (optional)',
      required: false,
      max: GRIEVANCE_LIMITS.reasonMax,
    };
    const call =
      (fn: (v: GrievanceCommandValues) => Observable<GrievanceCaseDetail>) =>
      (v: GrievanceCommandValues) =>
        fn(v);
    switch (action) {
      case 'acknowledge': {
        const days =
          safeValue(this.categoryList)?.find((c) => c.id === d.categoryId)?.defaultTargetDays ??
          null;
        return {
          title: `Acknowledge ${d.caseNo}?`,
          message:
            'Set the priority and the target date. The employee is told the case was received.',
          confirmText: 'Acknowledge',
          priority: { initial: 'Medium' },
          targetDate: {
            initial: days === null ? null : addDays(todayIso(), days),
            hint:
              days === null
                ? 'Left empty, the category’s default applies.'
                : `The category’s default: ${days} days from today.`,
          },
          text: comment,
          run: call((v) =>
            this.cases.acknowledge(id, {
              rowVersion: rv,
              priority: v.priority!,
              targetResolutionDate: v.targetDate,
              comment: v.text,
            }),
          ),
        };
      }
      case 'startReview':
        return this.simple(
          'Start the review?',
          'HR reviews the case before deciding how to handle it.',
          'Start review',
          (v) => this.cases.startReview(id, { rowVersion: rv, comment: v.text }),
        );
      case 'startInvestigation':
        return this.simple(
          'Start the investigation?',
          'The case moves to Under Investigation.',
          'Start investigation',
          (v) => this.cases.startInvestigation(id, { rowVersion: rv, comment: v.text }),
        );
      case 'close':
        return this.simple(
          'Close the case?',
          'Closing is final: only a manager who is also a reviewer can reopen it.',
          'Close case',
          (v) => this.cases.close(id, { rowVersion: rv, comment: v.text }),
        );
      case 'archive':
        return {
          ...this.simple(
            'Archive the case?',
            'An archived case is read-only and leaves the case lists (find it with the Archived filter).',
            'Archive',
            (v) => this.cases.archive(id, { rowVersion: rv, comment: v.text }),
          ),
          destructive: true,
        };
      case 'reject':
        return {
          title: `Reject ${d.caseNo}?`,
          message: 'The employee sees the reason. A rejected case can be reopened later.',
          confirmText: 'Reject',
          destructive: true,
          text: reason('Reason (shown to the employee)'),
          run: call((v) => this.cases.reject(id, { rowVersion: rv, reason: v.text! })),
        };
      case 'reopen':
        return {
          title: `Reopen ${d.caseNo}?`,
          message: 'The case goes back to HR for review. The employee sees the reason.',
          confirmText: 'Reopen',
          text: reason('Reason (shown to the employee)'),
          run: call((v) => this.cases.reopen(id, { rowVersion: rv, reason: v.text! })),
        };
      case 'requestInfo':
        return {
          title: 'Request information from the employee',
          message:
            'The case waits for the employee; their answer moves it back to where it is now.',
          confirmText: 'Send request',
          text: {
            field: 'message',
            label: 'Question for the employee',
            required: true,
            max: GRIEVANCE_LIMITS.infoRequestMax,
          },
          run: call((v) => this.cases.requestInfo(id, { rowVersion: rv, message: v.text! })),
        };
      case 'proposeResolution':
        return {
          title: 'Propose a resolution',
          message: 'A reviewer approves or sends back the proposal. You cannot approve your own.',
          confirmText: 'Propose',
          text: {
            field: 'summary',
            label: 'Resolution summary',
            required: true,
            max: GRIEVANCE_LIMITS.summaryMax,
            hint: 'What was found and what will be done. It may be shared with the employee.',
          },
          run: call((v) => this.cases.proposeResolution(id, { rowVersion: rv, summary: v.text! })),
        };
      case 'approveResolution':
        return {
          title: 'Approve the resolution?',
          message: 'The case is resolved with the proposed summary.',
          confirmText: 'Approve',
          visibleToSubmitter: true,
          text: comment,
          run: call((v) =>
            this.cases.approveResolution(id, {
              rowVersion: rv,
              visibleToSubmitter: v.visibleToSubmitter,
              comment: v.text,
            }),
          ),
        };
      case 'rejectResolution':
        return {
          title: 'Send the proposal back?',
          message: d.leadInvestigatorEmployeeId
            ? 'The case returns to the investigation.'
            : 'The case returns to HR review.',
          confirmText: 'Send back',
          text: reason('What needs to change'),
          run: call((v) => this.cases.rejectResolution(id, { rowVersion: rv, reason: v.text! })),
        };
      case 'escalate':
        return {
          title: `Escalate ${d.caseNo}?`,
          message: 'The reviewers are alerted. A case is escalated once and stays escalated.',
          confirmText: 'Escalate',
          text: reason('Reason'),
          isImmediate: true,
          run: call((v) =>
            this.cases.escalate(id, {
              rowVersion: rv,
              reason: v.text!,
              isImmediate: v.isImmediate,
            }),
          ),
        };
      default:
        return null;
    }
  }

  private simple(
    title: string,
    message: string,
    confirmText: string,
    fn: (v: GrievanceCommandValues) => Observable<GrievanceCaseDetail>,
  ): GrievanceCommandDialogData {
    return {
      title,
      message,
      confirmText,
      text: {
        field: 'comment',
        label: 'Comment (optional)',
        required: false,
        max: GRIEVANCE_LIMITS.reasonMax,
      },
      run: fn,
    };
  }
}
