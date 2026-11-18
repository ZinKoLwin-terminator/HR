import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { isAppError } from '../../../core/models/common.models';
import {
  GRIEVANCE_ATTACHMENT_VISIBILITY_LABELS,
  GRIEVANCE_LIMITS,
  GRIEVANCE_NOT_DISCLOSED_LABEL,
  GrievanceAssignment,
  GrievanceAttachmentVisibility,
  GrievanceCaseDetail,
  GrievanceCorrectiveActionUpdate,
} from '../../../core/models/grievance.models';
import { ADMIN_ROLE_NAME } from '../../../core/models/settings.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import {
  GrievanceCaseDataService,
  MyGrievanceDataService,
} from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import {
  grievanceAvailableActions,
  grievanceStateOf,
  grievanceViewerRights,
  isGrievanceOpen,
} from '../../../core/utilities/grievance-workflow';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import {
  GrievanceAttachmentListComponent,
  GrievanceFileItem,
} from '../shared/grievance-attachment-list.component';
import {
  GrievanceCaseActionsComponent,
  GrievanceWorkspace,
} from '../shared/grievance-case-actions.component';
import {
  GrievanceCommandDialogComponent,
  GrievanceCommandDialogData,
  GrievanceCommandDialogResult,
} from '../shared/grievance-command-dialog.component';
import {
  GrievanceMessageThreadComponent,
  teamThreadMessages,
} from '../shared/grievance-message-thread.component';
import { GrievancePriorityComponent } from '../shared/grievance-priority.component';
import { GrievanceStatusBadgeComponent } from '../shared/grievance-status-badge.component';
import {
  GrievanceTimelineComponent,
  teamTimelineItems,
} from '../shared/grievance-timeline.component';
import {
  GrievanceActionDraft,
  GrievanceCorrectiveActionsComponent,
} from './grievance-corrective-actions.component';
import { GrievanceNoteDraft, GrievanceNotesComponent } from './grievance-notes.component';
import { GrievanceTriagePanelComponent } from './grievance-triage-panel.component';
import { safeValue } from '../shared/resource-value';

const BACK: Record<GrievanceWorkspace, { link: string; label: string }> = {
  management: { link: '/grievances/cases', label: 'Case management' },
  investigation: { link: '/grievances/investigations', label: 'My investigations' },
  review: { link: '/grievances/reviews', label: 'Reviews' },
};

/**
 * One grievance case for the case team, in three workspaces chosen by the route:
 *  - management (`/grievances/cases/:id`): every section, triage, assignment, corrective actions,
 *    and for administrators the access log;
 *  - investigation (`/grievances/investigations/:id`): the investigation record, files (case team or
 *    shared), messages and the investigator's commands; no corrective actions or restricted files;
 *  - review (`/grievances/reviews/:id`): findings, recommendations and the proposal to decide on.
 * The server already trims the sections by tier; the commands offered come from
 * `grievanceAvailableActions`. The submitter of an anonymous case is never shown.
 */
@Component({
  selector: 'app-grievance-case-detail',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    FormsModule,
    GrievanceAttachmentListComponent,
    GrievanceCaseActionsComponent,
    GrievanceCorrectiveActionsComponent,
    GrievanceMessageThreadComponent,
    GrievanceNotesComponent,
    GrievancePriorityComponent,
    GrievanceStatusBadgeComponent,
    GrievanceTimelineComponent,
    GrievanceTriagePanelComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    PageHeaderComponent,
    RouterLink,
  ],
  template: `
    @if (detail.error() && !grievance()) {
      <app-page-header title="Grievance case" />
      <app-empty-state icon="error" title="This case could not be opened" [message]="errorText()">
        <a mat-flat-button [routerLink]="back().link">Back to {{ back().label.toLowerCase() }}</a>
      </app-empty-state>
    } @else if (grievance(); as g) {
      <app-page-header [title]="g.caseNo" [description]="g.subject">
        <a pageActions mat-stroked-button [routerLink]="back().link">
          <mat-icon>arrow_back</mat-icon>{{ back().label }}
        </a>
      </app-page-header>

      <div class="summary" aria-label="Case summary">
        <app-grievance-status-badge [status]="g.status" />
        <app-grievance-priority [priority]="g.priority" />
        <span
          >Target:
          {{ g.targetResolutionDate ? (g.targetResolutionDate | appDate) : 'not set' }}</span
        >
        @if (g.isOverdue) {
          <span class="chip chip--alert">Overdue</span>
        }
        @if (g.isEscalated) {
          <span class="chip chip--alert"><span aria-hidden="true">▲</span> Escalated</span>
        }
        @if (g.isAnonymous) {
          <span class="chip">Anonymous</span>
        }
        @if (g.legalHold) {
          <span class="chip">Legal hold</span>
        }
        @if (g.archivedAtUtc) {
          <span class="chip">Archived {{ g.archivedAtUtc | appDate }}</span>
        }
      </div>

      <app-grievance-case-actions
        [detail]="g"
        [workspace]="workspace()"
        (changed)="replace($event)"
        (reload)="reloadAll()"
      />
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" aria-label="Working" />
      }

      <div class="layout">
        <div class="side">
          <section class="card" aria-labelledby="submitter-title">
            <h2 id="submitter-title">Submitter</h2>
            @if (g.submitter; as s) {
              <dl class="facts">
                <div>
                  <dt>Name</dt>
                  <dd>{{ s.displayName }} ({{ s.employeeCode }})</dd>
                </div>
                <div>
                  <dt>Department</dt>
                  <dd>{{ s.departmentName ?? '—' }}</dd>
                </div>
                <div>
                  <dt>Position</dt>
                  <dd>{{ s.positionName ?? '—' }}</dd>
                </div>
                <div>
                  <dt>Branch</dt>
                  <dd>{{ s.branchName ?? '—' }}</dd>
                </div>
              </dl>
              <p class="hint">As recorded when the grievance was submitted.</p>
            } @else {
              <p>
                <strong>{{ g.submitterLabel }}</strong> · Department {{ notDisclosed }}
              </p>
              <p class="hint">
                The employee reported this anonymously. Their identity is not stored on the case and
                is not available to anyone on the case team; messages reach them through the
                grievance itself.
              </p>
            }
          </section>

          <section class="card" aria-labelledby="facts-title">
            <h2 id="facts-title">Facts</h2>
            <dl class="facts">
              <div>
                <dt>Category</dt>
                <dd>{{ g.categoryName }}</dd>
              </div>
              <div>
                <dt>Submitted</dt>
                <dd>{{ g.submittedAtUtc | appDate }}</dd>
              </div>
              <div>
                <dt>Incident date</dt>
                <dd>{{ g.incidentDate ? (g.incidentDate | appDate) : '—' }}</dd>
              </div>
              <div>
                <dt>Location</dt>
                <dd>{{ g.location ?? '—' }}</dd>
              </div>
              <div>
                <dt>Person involved</dt>
                <dd>{{ g.involvedPersonText ?? '—' }}</dd>
              </div>
              <div>
                <dt>Department involved</dt>
                <dd>{{ g.involvedDepartmentName ?? '—' }}</dd>
              </div>
              <div>
                <dt>Subject employee</dt>
                <dd>{{ g.subjectEmployeeName ?? '—' }}</dd>
              </div>
              <div>
                <dt>Lead investigator</dt>
                <dd>{{ g.leadInvestigatorName ?? '—' }}</dd>
              </div>
              @if (g.retentionUntil) {
                <div>
                  <dt>Keep until</dt>
                  <dd>{{ g.retentionUntil | appDate }}</dd>
                </div>
              }
            </dl>
          </section>

          @if (workspace() === 'management') {
            <section class="card" aria-labelledby="assign-title">
              <h2 id="assign-title">Investigators</h2>
              @if (g.assignments.length === 0) {
                <p class="hint">Nobody is assigned yet.</p>
              } @else {
                <ul class="assignments">
                  @for (a of g.assignments; track a.id) {
                    <li [class.ended]="a.unassignedAtUtc">
                      <span>
                        <strong>{{ a.investigatorName }}</strong> · {{ a.role }}
                        @if (a.unassignedAtUtc) {
                          · ended {{ a.unassignedAtUtc | appDate }}
                        }
                      </span>
                      @if (!a.unassignedAtUtc && a.role === 'Member' && canUnassign()) {
                        <button mat-button type="button" (click)="unassign(g, a)">Remove</button>
                      }
                    </li>
                  }
                </ul>
              }
            </section>
          }
        </div>

        <div class="main">
          <section class="card" aria-labelledby="description-title">
            <h2 id="description-title">Description</h2>
            <p class="text">{{ g.description }}</p>
            @if (g.rejectionReason) {
              <h3>Rejection reason</h3>
              <p class="text">{{ g.rejectionReason }}</p>
            }
            @if (g.resolutionSummary) {
              <h3>Resolution</h3>
              <p class="text">{{ g.resolutionSummary }}</p>
              <p class="hint">
                {{
                  g.resolutionVisibleToSubmitter
                    ? 'Shared with the employee.'
                    : 'Not shared with the employee.'
                }}
              </p>
            }
          </section>

          @if (showTriage()) {
            <section class="card" aria-labelledby="triage-title">
              <h2 id="triage-title">Triage</h2>
              <app-grievance-triage-panel
                [detail]="g"
                [departments]="departments()"
                (changed)="replace($event)"
                (reload)="reloadAll()"
              />
            </section>
          }

          <mat-tab-group
            class="tabs"
            animationDuration="0ms"
            [selectedIndex]="workspace() === 'review' ? 1 : 0"
          >
            @if (workspace() !== 'review') {
              <mat-tab label="Messages">
                <div class="tab">
                  <app-grievance-message-thread
                    [messages]="messages()"
                    [canReply]="canMessage()"
                    [busy]="busy()"
                    replyLabel="Message to the employee"
                    (send)="sendMessage(g, $event)"
                  />
                </div>
              </mat-tab>
            }
            <mat-tab [label]="workspace() === 'review' ? 'Findings' : 'Investigation notes'">
              <div class="tab">
                <app-grievance-notes
                  [notes]="g.notes"
                  [canAdd]="canAddNote()"
                  [busy]="busy()"
                  [error]="noteError()"
                  (add)="addNote(g, $event)"
                />
              </div>
            </mat-tab>
            <mat-tab label="Proposal and escalations">
              <div class="tab">
                <h3>Resolution proposal</h3>
                @if (g.proposal; as p) {
                  <p class="text">{{ p.summary }}</p>
                  <p class="hint">
                    Proposed by {{ p.proposedByName }} on {{ p.proposedAtUtc | appDate }} ·
                    {{ p.decision }}
                    @if (p.decidedByName) {
                      by {{ p.decidedByName }} on {{ p.decidedAtUtc | appDate }}
                    }
                  </p>
                  @if (p.decisionComment) {
                    <p class="text">{{ p.decisionComment }}</p>
                  }
                } @else {
                  <p class="hint">No resolution has been proposed.</p>
                }
                <h3>Escalations</h3>
                @if (g.escalations.length === 0) {
                  <p class="hint">Not escalated.</p>
                } @else {
                  <ul class="plain">
                    @for (e of g.escalations; track e.id) {
                      <li>
                        <strong>{{ e.isImmediate ? 'Immediate' : 'Escalated' }}</strong> ·
                        {{ e.escalatedByName }} · {{ e.escalatedAtUtc | appDate }}
                        @if (e.reason) {
                          : {{ e.reason }}
                        }
                      </li>
                    }
                  </ul>
                }
              </div>
            </mat-tab>
            @if (workspace() !== 'review') {
              <mat-tab label="Files">
                <div class="tab">
                  @if (canUpload()) {
                    <mat-form-field class="visibility" subscriptSizing="dynamic">
                      <mat-label>Who may see the next file</mat-label>
                      <mat-select
                        [ngModel]="uploadVisibility()"
                        (ngModelChange)="uploadVisibility.set($event)"
                      >
                        @for (v of visibilities(); track v) {
                          <mat-option [value]="v">{{ visibilityLabels[v] }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                  <app-grievance-attachment-list
                    label="Case files"
                    [files]="files()"
                    [canAdd]="canUpload()"
                    [busy]="busy()"
                    [error]="fileError()"
                    uploader="case-team"
                    [ownCount]="teamFileCount()"
                    (add)="upload(g, $event)"
                    (download)="download($event)"
                  />
                </div>
              </mat-tab>
            }
            @if (workspace() === 'management' && isManager()) {
              <mat-tab label="Corrective actions">
                <div class="tab">
                  <app-grievance-corrective-actions
                    [actions]="g.correctiveActions"
                    [canEdit]="canEditActions()"
                    [busy]="busy()"
                    [error]="actionError()"
                    (add)="addAction(g, $event)"
                    (update)="updateAction($event.actionId, $event.changes)"
                  />
                </div>
              </mat-tab>
            }
            <mat-tab label="History">
              <div class="tab">
                @if (history.error()) {
                  <p class="hint">The history could not be loaded.</p>
                } @else {
                  <app-grievance-timeline [items]="timeline()" label="Case history" />
                }
              </div>
            </mat-tab>
            @if (showAudit()) {
              <mat-tab label="Access log">
                <div class="tab">
                  @if (audit.hasValue() ? audit.value() : undefined; as log) {
                    @if (log.items.length === 0) {
                      <p class="hint">No access recorded.</p>
                    } @else {
                      <table class="audit">
                        <caption class="cdk-visually-hidden">
                          Access log of
                          {{
                            g.caseNo
                          }}
                        </caption>
                        <thead>
                          <tr>
                            <th scope="col">When</th>
                            <th scope="col">Action</th>
                            <th scope="col">Who</th>
                            <th scope="col">Role</th>
                            <th scope="col">Detail</th>
                          </tr>
                        </thead>
                        <tbody>
                          @for (a of log.items; track a.id) {
                            <tr>
                              <td>{{ a.occurredAtUtc | appDate }}</td>
                              <td>{{ a.action }}</td>
                              <td>{{ a.actorName ?? '—' }}</td>
                              <td>{{ a.actorRole }}</td>
                              <td>{{ a.detail ?? '' }}</td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    }
                  } @else if (audit.error()) {
                    <p class="hint">The access log could not be loaded.</p>
                  }
                </div>
              </mat-tab>
            }
          </mat-tab-group>
        </div>
      </div>
    } @else {
      <app-page-header title="Grievance case" />
      <mat-progress-bar mode="indeterminate" aria-label="Loading the case" />
    }
  `,
  styles: `
    .summary {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      margin-bottom: 12px;
    }
    .chip {
      padding: 0 8px;
      border-radius: 999px;
      background: var(--mat-sys-surface-container-high);
      font: var(--mat-sys-label-medium);
    }
    .chip--alert {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(260px, 1fr) minmax(0, 2fr);
      gap: 16px;
      align-items: start;
    }
    @media (max-width: 1100px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }
    .side,
    .main {
      display: flex;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
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
      margin: 12px 0 4px;
      font: var(--mat-sys-title-small);
    }
    .facts {
      display: grid;
      gap: 6px;
      margin: 0;
    }
    .facts dt {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .facts dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .text {
      margin: 0;
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
    .hint {
      margin: 4px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .assignments,
    .plain {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .assignments li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .assignments .ended {
      color: var(--mat-sys-on-surface-variant);
    }
    .tabs {
      border-radius: var(--app-radius-lg, 12px);
      background: var(--mat-sys-surface-container-low);
    }
    .tab {
      padding: 16px 20px;
    }
    .visibility {
      width: 280px;
      margin-bottom: 8px;
    }
    .audit {
      width: 100%;
      border-collapse: collapse;
      font: var(--mat-sys-body-small);
    }
    .audit th,
    .audit td {
      padding: 4px 8px;
      text-align: start;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
  `,
})
export class GrievanceCaseDetailComponent {
  /** `:id` route parameter and the route's `workspace`. */
  readonly id = input.required<string>();
  readonly workspace = input<GrievanceWorkspace>('management');

  private readonly cases = inject(GrievanceCaseDataService);
  private readonly mine = inject(MyGrievanceDataService);
  private readonly permissions = inject(PermissionService);
  private readonly user = inject(CurrentUserService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(UiNotificationService);
  private readonly thread = viewChild(GrievanceMessageThreadComponent);
  private readonly notesPanel = viewChild(GrievanceNotesComponent);
  private readonly actionsPanel = viewChild(GrievanceCorrectiveActionsComponent);

  protected readonly visibilityLabels = GRIEVANCE_ATTACHMENT_VISIBILITY_LABELS;
  protected readonly notDisclosed = GRIEVANCE_NOT_DISCLOSED_LABEL.toLowerCase();
  protected readonly back = computed(() => BACK[this.workspace()]);

  protected readonly detail = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) => this.cases.getById(params.id),
  });
  protected readonly grievance = signal<GrievanceCaseDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly noteError = signal<string | null>(null);
  protected readonly fileError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly uploadVisibility = signal<GrievanceAttachmentVisibility>('CaseTeam');

  protected readonly history = rxResource({
    params: () =>
      this.grievance() ? { id: this.grievance()!.id, v: this.grievance()!.rowVersion } : undefined,
    stream: ({ params }) => this.cases.history(params.id),
  });
  protected readonly isAdmin = computed(() => this.user.current().roles.includes(ADMIN_ROLE_NAME));
  protected readonly isManager = computed(
    () => this.grievance()?.viewerRoles.includes('Management') ?? false,
  );
  protected readonly showAudit = computed(
    () => this.workspace() === 'management' && this.isManager() && this.isAdmin(),
  );
  protected readonly audit = rxResource({
    params: () =>
      this.showAudit() ? { id: this.grievance()!.id, v: this.grievance()!.rowVersion } : undefined,
    stream: ({ params }) => this.cases.audit(params.id, { pageIndex: 0, pageSize: 100 }),
  });
  /** Departments for triage (from the grievance options every role may read). */
  private readonly options = rxResource({
    params: () => (this.workspace() === 'management' ? {} : undefined),
    stream: () => this.mine.options().pipe(catchError(() => of(null))),
  });
  protected readonly departments = computed(() => safeValue(this.options)?.departments ?? []);

  private readonly rights = computed(() => {
    const g = this.grievance();
    const id = this.user.current().employeeId;
    return g
      ? grievanceViewerRights(g, (k) => this.permissions.can(k), id !== null && id > 0 ? id : null)
      : null;
  });
  private readonly available = computed(() => {
    const g = this.grievance();
    const rights = this.rights();
    return g && rights
      ? new Set(grievanceAvailableActions(grievanceStateOf(g), rights))
      : new Set();
  });
  /** Files go on an open case only (the API's `EnsureFilesOpen`): not Resolved, Rejected or Closed. */
  private readonly filesOpen = computed(() => {
    const g = this.grievance();
    return !!g && g.archivedAtUtc === null && isGrievanceOpen(g.status);
  });

  protected readonly canMessage = computed(() => this.available().has('postTeamMessage'));
  protected readonly canUnassign = computed(() => this.available().has('unassign'));
  protected readonly canAddNote = computed(() => {
    const g = this.grievance();
    const r = this.rights();
    return (
      this.workspace() !== 'review' &&
      !!g &&
      !!r &&
      g.archivedAtUtc === null &&
      isGrievanceOpen(g.status) &&
      (r.managementEdit || r.investigatorEdit)
    );
  });
  protected readonly canUpload = computed(() => {
    const r = this.rights();
    return this.filesOpen() && !!r && (r.managementEdit || r.investigatorEdit);
  });
  /** Restricted files are management's; the investigation workspace offers case team or shared. */
  protected readonly visibilities = computed<GrievanceAttachmentVisibility[]>(() =>
    this.workspace() === 'management' && this.rights()?.managementEdit
      ? ['CaseTeam', 'Shared', 'Restricted']
      : ['CaseTeam', 'Shared'],
  );
  protected readonly canEditActions = computed(
    () => !!this.rights()?.managementEdit && this.grievance()?.archivedAtUtc === null,
  );
  protected readonly showTriage = computed(() => {
    const g = this.grievance();
    return (
      this.workspace() === 'management' &&
      !!g &&
      !!this.rights()?.managementEdit &&
      g.archivedAtUtc === null
    );
  });

  protected readonly messages = computed(() =>
    teamThreadMessages(this.grievance()?.messages ?? []),
  );
  protected readonly timeline = computed(() => teamTimelineItems(safeValue(this.history) ?? []));
  /** The case team's files on the case (its per-case quota, apart from the submitter's). */
  protected readonly teamFileCount = computed(
    () =>
      (this.grievance()?.attachments ?? []).filter((a) => a.uploadedByRole !== 'Submitter').length,
  );
  protected readonly files = computed<GrievanceFileItem[]>(() =>
    (this.grievance()?.attachments ?? []).map((a) => ({
      key: `file-${a.id}`,
      id: a.id,
      fileName: a.fileName,
      sizeBytes: a.sizeBytes,
      uploadedAtUtc: a.uploadedAtUtc,
      uploadedBy: a.uploadedByRole,
      visibility: a.visibility,
    })),
  );
  protected readonly errorText = computed(() =>
    this.detail.error() ? errorMessage(this.detail.error()) : '',
  );

  constructor() {
    effect(() => {
      const loaded = safeValue(this.detail);
      if (loaded) untracked(() => this.grievance.set(loaded));
    });
  }

  protected replace(updated: GrievanceCaseDetail): void {
    this.grievance.set(updated);
  }

  protected reloadAll(): void {
    this.detail.reload();
  }

  /** A refused write: field-level text where it belongs, a reload offer on a conflict. */
  private refused(error: unknown, target: (message: string) => void): void {
    target(errorMessage(error));
    this.toast.error(error);
    if (isAppError(error) && error.code === 'Conflict') {
      this.detail.reload();
    }
  }

  protected sendMessage(g: GrievanceCaseDetail, body: string): void {
    this.busy.set(true);
    this.cases
      .sendMessage(g.id, { rowVersion: g.rowVersion, body })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (updated) => {
          this.grievance.set(updated);
          this.thread()?.clear();
          this.toast.success('Message sent to the employee.');
        },
        error: (e: unknown) => this.refused(e, () => undefined),
      });
  }

  protected addNote(g: GrievanceCaseDetail, note: GrievanceNoteDraft): void {
    this.noteError.set(null);
    this.busy.set(true);
    this.cases
      .addNote(g.id, { ...note, rowVersion: g.rowVersion })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (updated) => {
          this.grievance.set(updated);
          this.notesPanel()?.reset();
          this.toast.success('Added to the investigation record.');
        },
        error: (e: unknown) => this.refused(e, (m) => this.noteError.set(m)),
      });
  }

  protected upload(g: GrievanceCaseDetail, file: File): void {
    this.fileError.set(null);
    this.busy.set(true);
    this.cases
      .uploadAttachment(g.id, g.rowVersion, this.uploadVisibility(), file)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (result) => {
          this.grievance.update((current) =>
            current
              ? {
                  ...current,
                  rowVersion: result.caseRowVersion,
                  attachments: [...current.attachments, result.attachment],
                }
              : current,
          );
          this.toast.success(`${result.attachment.fileName} added.`);
        },
        error: (e: unknown) => this.refused(e, (m) => this.fileError.set(m)),
      });
  }

  protected download(item: GrievanceFileItem): void {
    if (item.id === null) return;
    this.cases.downloadAttachment(item.id).subscribe({
      next: (file) => downloadFile(file),
      error: (e: unknown) => this.toast.error(e),
    });
  }

  protected addAction(g: GrievanceCaseDetail, draft: GrievanceActionDraft): void {
    this.actionError.set(null);
    this.busy.set(true);
    this.cases
      .addCorrectiveAction(g.id, { ...draft, rowVersion: g.rowVersion })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (action) => {
          this.grievance.update((current) =>
            current
              ? { ...current, correctiveActions: [...current.correctiveActions, action] }
              : current,
          );
          this.actionsPanel()?.saved(null);
          this.toast.success('Corrective action added.');
        },
        error: (e: unknown) => this.refused(e, (m) => this.actionError.set(m)),
      });
  }

  protected updateAction(actionId: number, changes: GrievanceCorrectiveActionUpdate): void {
    this.actionError.set(null);
    this.busy.set(true);
    this.cases
      .updateCorrectiveAction(actionId, changes)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (action) => {
          this.grievance.update((current) =>
            current
              ? {
                  ...current,
                  correctiveActions: current.correctiveActions.map((a) =>
                    a.id === action.id ? action : a,
                  ),
                }
              : current,
          );
          this.actionsPanel()?.saved(actionId);
          this.toast.success('Corrective action updated.');
        },
        error: (e: unknown) => this.refused(e, (m) => this.actionError.set(m)),
      });
  }

  protected unassign(g: GrievanceCaseDetail, assignment: GrievanceAssignment): void {
    this.dialog
      .open<
        GrievanceCommandDialogComponent,
        GrievanceCommandDialogData,
        GrievanceCommandDialogResult
      >(GrievanceCommandDialogComponent, {
        width: '520px',
        maxWidth: '95vw',
        data: {
          title: `Remove ${assignment.investigatorName}?`,
          message: 'They no longer see the case. The assignment stays in the case record.',
          confirmText: 'Remove',
          destructive: true,
          text: {
            field: 'reason',
            label: 'Reason (optional)',
            required: false,
            max: GRIEVANCE_LIMITS.reasonMax,
          },
          run: (v) =>
            this.cases.unassign(g.id, {
              rowVersion: g.rowVersion,
              assignmentId: assignment.id,
              reason: v.text,
            }),
        },
      })
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        if ('reload' in result) {
          this.detail.reload();
          return;
        }
        this.grievance.set(result.done as GrievanceCaseDetail);
        this.toast.success('Investigator removed.');
      });
  }
}
