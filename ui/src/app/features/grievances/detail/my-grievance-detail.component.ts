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
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { filter, finalize, switchMap } from 'rxjs';
import { MyGrievanceDetail } from '../../../core/models/grievance.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { MyGrievanceDataService } from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { downloadFile } from '../../../core/utilities/file-download';
import { grievanceSubmitterOptions } from '../../../core/utilities/grievance-workflow';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import {
  GrievanceAttachmentListComponent,
  GrievanceFileItem,
} from '../shared/grievance-attachment-list.component';
import {
  GrievanceMessageThreadComponent,
  myThreadMessages,
} from '../shared/grievance-message-thread.component';
import { ANONYMOUS_FILE_WARNING } from '../shared/grievance-anonymity';
import { GrievanceStatusBadgeComponent } from '../shared/grievance-status-badge.component';
import {
  GrievanceTimelineComponent,
  myTimelineItems,
} from '../shared/grievance-timeline.component';
import { safeValue } from '../../../shared/utilities/resource-value';

/**
 * One of the employee's own grievances (`/grievances/my/:id`): what they reported, the status
 * timeline, the resolution when HR shares it, the conversation with HR (the reply box becomes
 * "Respond to HR's request" while HR waits for an answer) and the shared files. Opening it clears
 * the unread badge. A draft offers Edit and Delete. What may be offered comes from
 * `grievanceSubmitterOptions` (UX only; the server decides).
 */
@Component({
  selector: 'app-my-grievance-detail',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    GrievanceAttachmentListComponent,
    GrievanceMessageThreadComponent,
    GrievanceStatusBadgeComponent,
    GrievanceTimelineComponent,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    RouterLink,
  ],
  template: `
    @if (detail.error() && !grievance()) {
      <app-page-header title="Grievance" />
      <app-empty-state
        icon="error"
        title="This grievance could not be opened"
        [message]="errorText()"
      >
        <a mat-flat-button routerLink="/grievances/my">Back to my grievances</a>
      </app-empty-state>
    } @else if (grievance(); as g) {
      <app-page-header [title]="g.subject" [description]="headline()">
        <a pageActions mat-stroked-button routerLink="/grievances/my">
          <mat-icon>arrow_back</mat-icon>Back
        </a>
        <ng-container pageActions>
          @if (options().canEditDraft && canCreate()) {
            <a mat-stroked-button [routerLink]="['/grievances/my', g.id, 'edit']">
              <mat-icon>edit</mat-icon>Edit draft
            </a>
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="deleteDraft(g)">
              <mat-icon>delete</mat-icon>Delete draft
            </button>
          }
        </ng-container>
      </app-page-header>

      @if (busy()) {
        <mat-progress-bar mode="indeterminate" aria-label="Working" />
      }

      <div class="layout">
        <div class="main">
          <section class="card" aria-labelledby="about-title">
            <div class="card__head">
              <h2 id="about-title">Your grievance</h2>
              <app-grievance-status-badge [status]="g.status" />
            </div>
            <dl class="facts">
              <div>
                <dt>Case number</dt>
                <dd>{{ g.caseNo ?? 'Not submitted yet' }}</dd>
              </div>
              <div>
                <dt>Category</dt>
                <dd>{{ g.categoryName }}</dd>
              </div>
              <div>
                <dt>Reported</dt>
                <dd>{{ g.isAnonymous ? 'Anonymously' : 'With your name' }}</dd>
              </div>
              <div>
                <dt>Submitted</dt>
                <dd>{{ g.submittedAtUtc ? (g.submittedAtUtc | appDate) : '—' }}</dd>
              </div>
              @if (g.targetResolutionDate) {
                <div>
                  <dt>Target date</dt>
                  <dd>{{ g.targetResolutionDate | appDate }}</dd>
                </div>
              }
              @if (g.incidentDate) {
                <div>
                  <dt>Incident date</dt>
                  <dd>{{ g.incidentDate | appDate }}</dd>
                </div>
              }
              @if (g.location) {
                <div>
                  <dt>Location</dt>
                  <dd>{{ g.location }}</dd>
                </div>
              }
              @if (g.involvedPersonText) {
                <div>
                  <dt>Person involved</dt>
                  <dd>{{ g.involvedPersonText }}</dd>
                </div>
              }
              @if (g.involvedDepartmentName) {
                <div>
                  <dt>Department involved</dt>
                  <dd>{{ g.involvedDepartmentName }}</dd>
                </div>
              }
            </dl>
            <h3>Description</h3>
            <p class="text">{{ g.description || '—' }}</p>
          </section>

          @if (g.rejectionReason) {
            <section class="card card--outcome" aria-labelledby="rejected-title">
              <h2 id="rejected-title">Why HR did not take it further</h2>
              <p class="text">{{ g.rejectionReason }}</p>
            </section>
          }
          @if (g.resolutionSummary) {
            <section class="card card--outcome" aria-labelledby="resolution-title">
              <h2 id="resolution-title">Resolution</h2>
              <p class="text">{{ g.resolutionSummary }}</p>
            </section>
          }

          @if (g.status !== 'Draft') {
            <section class="card" aria-labelledby="messages-title">
              <h2 id="messages-title">Messages with HR</h2>
              <app-grievance-message-thread
                [messages]="messages()"
                [canReply]="options().canMessage && canCreate()"
                [respondMode]="options().isRespondMode"
                [busy]="busy()"
                replyLabel="Message to HR"
                (send)="send(g, $event)"
              />
            </section>
          }

          <section class="card" aria-labelledby="files-title">
            <h2 id="files-title">Files</h2>
            <p class="hint">Files you and HR share on this grievance.</p>
            <app-grievance-attachment-list
              label="Shared files"
              [files]="files()"
              [canAdd]="options().canUpload && canCreate()"
              [busy]="busy()"
              [error]="fileError()"
              [ownCount]="myFileCount()"
              [warning]="g.isAnonymous ? fileWarning : null"
              (add)="upload(g, $event)"
              (download)="download($event)"
            />
          </section>
        </div>

        <aside class="card side" aria-labelledby="timeline-title">
          <h2 id="timeline-title">Timeline</h2>
          <app-grievance-timeline [items]="timeline()" />
        </aside>
      </div>
    } @else {
      <app-page-header title="Grievance" />
      <mat-progress-bar mode="indeterminate" aria-label="Loading the grievance" />
    }
  `,
  styles: `
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(260px, 1fr);
      gap: 16px;
      align-items: start;
    }
    @media (max-width: 960px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }
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
    .card--outcome {
      box-shadow: inset 4px 0 0 var(--mat-sys-primary);
    }
    .card__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-medium);
    }
    .card__head h2 {
      margin: 0;
    }
    h3 {
      margin: 12px 0 4px;
      font: var(--mat-sys-title-small);
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 8px 16px;
      margin: 12px 0 0;
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
      margin: 0 0 8px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class MyGrievanceDetailComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly grievances = inject(MyGrievanceDataService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiNotificationService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly permissions = inject(PermissionService);
  private readonly thread = viewChild(GrievanceMessageThreadComponent);

  protected readonly detail = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) => this.grievances.getById(params.id),
  });
  /** The latest copy: the loaded one, then each write's answer. */
  protected readonly grievance = signal<MyGrievanceDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly fileError = signal<string | null>(null);
  private seenFor: number | null = null;

  protected readonly canCreate = computed(() => this.permissions.can('grievance:Create'));
  protected readonly options = computed(() => {
    const g = this.grievance();
    return grievanceSubmitterOptions(g?.status ?? 'Closed');
  });
  protected readonly errorText = computed(() =>
    this.detail.error() ? errorMessage(this.detail.error()) : '',
  );
  protected readonly headline = computed(() => {
    const g = this.grievance();
    if (!g) return '';
    return g.caseNo ? `${g.caseNo} · ${g.categoryName}` : `Draft · ${g.categoryName}`;
  });
  protected readonly timeline = computed(() => myTimelineItems(this.grievance()?.timeline ?? []));
  protected readonly messages = computed(() => myThreadMessages(this.grievance()?.messages ?? []));
  protected readonly fileWarning = ANONYMOUS_FILE_WARNING;
  /** The submitter's own files on the case (their per-case quota). */
  protected readonly myFileCount = computed(
    () => (this.grievance()?.attachments ?? []).filter((a) => a.uploadedBy === 'You').length,
  );
  protected readonly files = computed<GrievanceFileItem[]>(() =>
    (this.grievance()?.attachments ?? []).map((a) => ({
      key: `file-${a.id}`,
      id: a.id,
      fileName: a.fileName,
      sizeBytes: a.sizeBytes,
      uploadedAtUtc: a.uploadedAtUtc,
      uploadedBy: a.uploadedBy,
    })),
  );

  constructor() {
    effect(() => {
      const loaded = safeValue(this.detail);
      if (loaded) {
        untracked(() => {
          this.grievance.set(loaded);
          this.markSeen(loaded);
        });
      }
    });
  }

  /** Opening the grievance clears its unread badge (once per visit). */
  private markSeen(g: MyGrievanceDetail): void {
    if (!g.hasUnreadUpdate || this.seenFor === g.id) return;
    this.seenFor = g.id;
    this.grievances.markSeen(g.id).subscribe({
      next: () =>
        this.grievance.update((current) =>
          current ? { ...current, hasUnreadUpdate: false } : current,
        ),
      // Not worth a toast: the badge simply stays until the next visit.
      error: () => undefined,
    });
  }

  protected send(g: MyGrievanceDetail, body: string): void {
    const responding = this.options().isRespondMode;
    this.busy.set(true);
    this.grievances
      .sendMessage(g.id, { rowVersion: g.rowVersion, body })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (updated) => {
          this.grievance.set(updated);
          this.thread()?.clear();
          this.toast.success(responding ? 'Your answer was sent to HR.' : 'Message sent.');
        },
        error: (e: unknown) => {
          this.toast.error(e);
          // A stale answer (HR changed the case meanwhile): reload so the next try is current.
          this.detail.reload();
        },
      });
  }

  protected upload(g: MyGrievanceDetail, file: File): void {
    this.fileError.set(null);
    this.busy.set(true);
    this.grievances
      .uploadAttachment(g.id, g.rowVersion, file)
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
        error: (e: unknown) => {
          this.fileError.set(errorMessage(e));
          this.toast.error(e);
        },
      });
  }

  protected download(item: GrievanceFileItem): void {
    if (item.id === null) return;
    this.grievances.downloadAttachment(item.id).subscribe({
      next: (file) => downloadFile(file),
      error: (e: unknown) => this.toast.error(e),
    });
  }

  protected deleteDraft(g: MyGrievanceDetail): void {
    this.confirm
      .confirm({
        title: 'Delete this draft?',
        message: `"${g.subject}" and its files will be removed. Nothing was sent to HR.`,
        confirmText: 'Delete draft',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busy.set(true);
          return this.grievances
            .deleteDraft(g.id, g.rowVersion)
            .pipe(finalize(() => this.busy.set(false)));
        }),
      )
      .subscribe({
        next: () => {
          this.toast.success('Draft deleted.');
          void this.router.navigate(['/grievances/my']);
        },
        error: (e: unknown) => this.toast.error(e),
      });
  }
}
