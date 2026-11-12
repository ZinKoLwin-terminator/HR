import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Observable, filter, forkJoin } from 'rxjs';
import {
  BLACKLIST_MATCH_LABEL,
  BLACKLIST_OVERRIDE_MAX_LENGTH,
} from '../../../core/models/blacklist.models';
import {
  APPLICATION_STAGES,
  Application,
  ApplicationStage,
  STAGE_TRANSITIONS,
  candidateName,
  isBlacklistHireBlock,
} from '../../../core/models/recruitment.models';
import { ADMIN_ROLE_NAME } from '../../../core/models/settings.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../../core/services/data/recruitment-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { EntityFormDialogComponent } from '../../../shared/components/crud/entity-form-dialog.component';
import { EntityFormDialogData } from '../../../shared/components/crud/crud.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { todayIso } from '../../../shared/utilities/date.util';

interface Card {
  application: Application;
  name: string;
  jobTitle: string;
  detail: string;
  interviews: string;
}

/** Kanban-style view of applications by hiring stage (Applied → Screening → Interview → Selected / Rejected). */
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';

@Component({
  selector: 'app-recruitment-pipeline',
  imports: [
    FilterFieldComponent,
    FilterPopupComponent,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    EmptyStateComponent,
    StatusBadgeComponent,
  ],
  template: `
    <app-page-header
      title="Recruitment pipeline"
      description="Applications by hiring stage. Move candidates forward from each card's menu."
    >
      <ng-container pageSearch>
        <app-filter-popup [activeCount]="jobId() === null ? 0 : 1" (cleared)="jobId.set(null)">
          <app-filter-field label="Job opening">
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                placeholder="All openings"
                aria-label="Job opening"
                [value]="jobId()"
                (selectionChange)="jobId.set($event.value)"
              >
                <mat-option [value]="null">All openings</mat-option>
                @for (j of jobOptions(); track j.id) {
                  <mat-option [value]="j.id">{{ j.title }} ({{ j.code }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </app-filter-field>
        </app-filter-popup>
      </ng-container>
      @if (permissions.can('recruitment:Create')) {
        <button
          pageActions
          mat-flat-button
          type="button"
          [disabled]="!data.value()"
          (click)="addApplication()"
        >
          <mat-icon>person_add</mat-icon>Add application
        </button>
      }
    </app-page-header>

    @if (data.error()) {
      <app-empty-state icon="error" title="The pipeline could not be loaded">
        <button mat-flat-button type="button" (click)="data.reload()">Try again</button>
      </app-empty-state>
    } @else if (data.value()) {
      <div class="board">
        @for (stage of stages; track stage) {
          <section class="column" [attr.aria-labelledby]="'stage-' + stage">
            <h2 class="column__title" [id]="'stage-' + stage">
              {{ stage }} <span class="count">{{ columns()[stage].length }}</span>
            </h2>
            <ul class="cards">
              @for (card of columns()[stage]; track card.application.id) {
                <li class="card">
                  <div class="card__head">
                    <strong>{{ card.name }}</strong>
                    @if (permissions.can('recruitment:Edit')) {
                      <button
                        mat-icon-button
                        type="button"
                        [matMenuTriggerFor]="menu"
                        [attr.aria-label]="'Actions for ' + card.name"
                      >
                        <mat-icon>more_vert</mat-icon>
                      </button>
                      <mat-menu #menu="matMenu">
                        @for (next of forwardStages(card.application); track next) {
                          <button mat-menu-item type="button" (click)="move(card, next)">
                            <mat-icon>arrow_forward</mat-icon>Move to {{ next }}
                          </button>
                        }
                        @if (canReject(card.application)) {
                          <button mat-menu-item type="button" (click)="reject(card)">
                            <mat-icon>block</mat-icon>Reject…
                          </button>
                        }
                        @if (
                          card.application.stage === 'Applied' &&
                          permissions.can('recruitment:Delete')
                        ) {
                          <button mat-menu-item type="button" (click)="remove(card)">
                            <mat-icon>delete</mat-icon>Remove application
                          </button>
                        }
                        @if (
                          !forwardStages(card.application).length && !canReject(card.application)
                        ) {
                          <button mat-menu-item type="button" disabled>No actions available</button>
                        }
                      </mat-menu>
                    }
                  </div>
                  @if (card.application.blacklistMatch) {
                    <div class="card__flag">
                      <app-status-badge [status]="blacklistLabel" />
                    </div>
                  }
                  @if (!jobId()) {
                    <div class="card__job">{{ card.jobTitle }}</div>
                  }
                  <div class="card__meta">{{ card.detail }}</div>
                  <div class="card__meta">{{ card.interviews }}</div>
                  @if (card.application.rejectionReason) {
                    <div class="card__reason">{{ card.application.rejectionReason }}</div>
                  }
                </li>
              } @empty {
                <li class="empty">No applications</li>
              }
            </ul>
          </section>
        }
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading pipeline" />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .toolbar {
      display: flex;
      gap: 12px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }
    .board {
      display: grid;
      grid-auto-flow: column;
      grid-auto-columns: minmax(250px, 1fr);
      gap: 12px;
      overflow-x: auto;
      padding-bottom: 8px;
    }
    .column {
      background: var(--mat-sys-surface-container-low);
      border-radius: var(--app-radius-md);
      padding: 12px;
      min-width: 0;
    }
    .column__title {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 0 0 10px;
      font: var(--mat-sys-title-small);
    }
    .count {
      font: var(--mat-sys-label-medium);
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--mat-sys-surface-container-highest);
    }
    .cards {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .card {
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      padding: 10px 12px;
    }
    .card__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 4px;
    }
    .card__flag {
      margin-top: 6px;
    }
    .card__job {
      font: var(--mat-sys-label-large);
      margin-top: 2px;
    }
    .card__meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin-top: 2px;
    }
    .card__reason {
      margin-top: 6px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-error);
    }
    .empty {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      padding: 8px 4px;
    }
  `,
})
export class RecruitmentPipelineComponent {
  protected readonly permissions = inject(PermissionService);
  private readonly applicationsApi = inject(ApplicationDataService);
  private readonly candidatesApi = inject(CandidateDataService);
  private readonly jobsApi = inject(JobOpeningDataService);
  private readonly interviewsApi = inject(InterviewDataService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);

  private readonly currentUser = inject(CurrentUserService);

  protected readonly stages = APPLICATION_STAGES;
  protected readonly blacklistLabel = BLACKLIST_MATCH_LABEL;
  /** Only an HR Administrator may approve the hire of a do-not-rehire match (the server checks too). */
  protected readonly isAdministrator = computed(() =>
    this.currentUser.current().roles.includes(ADMIN_ROLE_NAME),
  );
  protected readonly jobId = signal<number | null>(null);

  protected readonly data = rxResource({
    stream: () =>
      forkJoin({
        applications: this.applicationsApi.getAll(),
        candidates: this.candidatesApi.getAll(),
        jobs: this.jobsApi.getAll(),
        interviews: this.interviewsApi.getAll(),
      }),
  });

  protected readonly jobOptions = computed(() =>
    [...(this.data.value()?.jobs ?? [])]
      .filter((j) => j.status !== 'Draft')
      .sort((a, b) => a.title.localeCompare(b.title)),
  );

  protected readonly columns = computed<Record<ApplicationStage, Card[]>>(() => {
    const d = this.data.value();
    const result: Record<ApplicationStage, Card[]> = {
      Applied: [],
      Screening: [],
      Interview: [],
      Selected: [],
      Rejected: [],
    };
    if (!d) {
      return result;
    }
    const today = todayIso();
    const people = new Map(d.candidates.map((c) => [c.id, c]));
    const jobs = new Map(d.jobs.map((j) => [j.id, j]));
    const filterJob = this.jobId();
    for (const application of d.applications) {
      if (filterJob !== null && application.jobOpeningId !== filterJob) continue;
      const candidate = people.get(application.candidateId);
      const mine = d.interviews.filter((i) => i.applicationId === application.id);
      const upcoming = mine
        .filter((i) => i.status === 'Scheduled' && i.date >= today)
        .sort((a, b) => a.date.localeCompare(b.date))[0];
      const done = mine.filter((i) => i.status === 'Completed').length;
      result[application.stage].push({
        application,
        name: candidate ? candidateName(candidate) : `Candidate #${application.candidateId}`,
        jobTitle: jobs.get(application.jobOpeningId)?.title ?? '—',
        detail: `Applied ${application.appliedOn}${candidate ? ` · ${candidate.experienceYears} yr experience` : ''}`,
        interviews: upcoming
          ? `Next: ${upcoming.round} on ${upcoming.date}`
          : done
            ? `${done} interview(s) completed`
            : 'No interviews yet',
      });
    }
    for (const stage of APPLICATION_STAGES) {
      result[stage].sort((a, b) => a.name.localeCompare(b.name));
    }
    return result;
  });

  protected forwardStages(a: Application): ApplicationStage[] {
    return STAGE_TRANSITIONS[a.stage].filter((s) => s !== 'Rejected');
  }

  protected canReject(a: Application): boolean {
    return STAGE_TRANSITIONS[a.stage].includes('Rejected');
  }

  private run(request: Observable<unknown>, message: string): void {
    request.subscribe({
      next: () => {
        this.toast.success(message);
        this.data.reload();
      },
      error: (error: unknown) => this.toast.error(error),
    });
  }

  protected move(card: Card, stage: ApplicationStage): void {
    this.applicationsApi.moveStage(card.application.id, stage).subscribe({
      next: () => {
        this.toast.success(`${card.name} moved to ${stage}.`);
        this.data.reload();
      },
      error: (error: unknown) => {
        // Selecting a do-not-rehire match needs an HR Administrator's approval with a reason.
        // Anyone else just sees the server's message.
        if (stage === 'Selected' && isBlacklistHireBlock(error) && this.isAdministrator()) {
          this.approveDespiteMatch(card);
        } else {
          this.toast.error(error);
        }
      },
    });
  }

  private approveDespiteMatch(card: Card): void {
    this.confirm
      .prompt({
        title: 'Approve hire of a do-not-rehire match?',
        message: `${card.name} matches the do-not-rehire list. As an HR Administrator you can approve the hire with a reason. The approval is logged.`,
        label: 'Approval reason',
        required: true,
        confirmText: 'Approve hire',
        destructive: true,
        maxLength: BLACKLIST_OVERRIDE_MAX_LENGTH,
      })
      .pipe(filter((reason): reason is string => reason !== null))
      .subscribe((reason) =>
        this.run(
          this.applicationsApi.moveStage(card.application.id, 'Selected', '', reason),
          `${card.name} moved to Selected with HR approval.`,
        ),
      );
  }

  protected reject(card: Card): void {
    this.confirm
      .prompt({
        title: 'Reject application?',
        message: `${card.name} will be moved to Rejected and any scheduled interviews cancelled.`,
        label: 'Reason',
        required: true,
        confirmText: 'Reject',
        destructive: true,
        maxLength: 200,
      })
      .pipe(filter((reason): reason is string => reason !== null))
      .subscribe((reason) =>
        this.run(
          this.applicationsApi.moveStage(card.application.id, 'Rejected', reason),
          `${card.name} rejected.`,
        ),
      );
  }

  protected remove(card: Card): void {
    this.confirm
      .confirmDelete('application', card.name)
      .pipe(filter(Boolean))
      .subscribe(() =>
        this.run(
          this.applicationsApi.delete(card.application.id),
          `Application of ${card.name} removed.`,
        ),
      );
  }

  protected addApplication(): void {
    const d = this.data.value();
    if (!d) return;
    const data: EntityFormDialogData<Application> = {
      title: 'Add application',
      fields: [
        {
          key: 'jobOpeningId',
          label: 'Job opening',
          type: 'select',
          required: true,
          fullWidth: true,
          hint: 'Only open job openings accept applications',
          options: () =>
            d.jobs
              .filter((j) => j.status === 'Open')
              .map((j) => ({ value: j.id, label: `${j.title} (${j.code})` }))
              .sort((a, b) => a.label.localeCompare(b.label)),
        },
        {
          key: 'candidateId',
          label: 'Candidate',
          type: 'select',
          required: true,
          fullWidth: true,
          options: () =>
            d.candidates
              .map((c) => ({ value: c.id, label: `${candidateName(c)} (${c.email})` }))
              .sort((a, b) => a.label.localeCompare(b.label)),
        },
        { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 300, fullWidth: true },
      ],
      value: { appliedOn: todayIso() },
      id: null,
      submitLabel: 'Create application',
      save: (values) =>
        this.applicationsApi.create({
          ...(values as unknown as Omit<Application, 'id'>),
          stage: 'Applied',
          rejectionReason: null,
          appliedOn: todayIso(),
          notes: String(values['notes'] ?? ''),
        }),
    };
    this.dialog
      .open<EntityFormDialogComponent, EntityFormDialogData<Application>, Application>(
        EntityFormDialogComponent,
        { data, width: '560px', maxWidth: '96vw', disableClose: true, autoFocus: 'first-tabbable' },
      )
      .afterClosed()
      .pipe(filter((saved): saved is Application => !!saved))
      .subscribe(() => {
        this.toast.success('Application added.');
        this.data.reload();
      });
  }
}
