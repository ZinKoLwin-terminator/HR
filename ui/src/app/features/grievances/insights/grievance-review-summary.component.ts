import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { BarController, BarElement, CategoryScale, LinearScale, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';
import { catchError, of } from 'rxjs';
import { GrievanceSummaryFilter } from '../../../core/models/grievance-insight.models';
import { GRIEVANCE_PRIORITIES } from '../../../core/models/grievance.models';
import {
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../../core/services/data/grievance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { GrievanceBarChartComponent } from './grievance-bar-chart.component';
import {
  categoryBars,
  daysText,
  departmentBars,
  percentText,
  priorityBars,
  statusBars,
} from './grievance-insight-bars';
import { safeValue } from '../../../shared/utilities/resource-value';

/**
 * The reviewers' summary (`grievance-review:View`): the review queue in figures (pending proposals,
 * escalations, overdue, the caller's own decisions) and the grievance summary over every case, with
 * its filters (submission dates, category, priority; no department filter, which could single
 * someone out). Aggregates only: no case list, no names.
 */
@Component({
  selector: 'app-grievance-review-summary',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    GrievanceBarChartComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
    StatCardComponent,
  ],
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Tooltip],
    }),
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header
      title="Review summary"
      description="The review queue in figures, and the grievance summary for the period you choose."
    >
      <a pageActions mat-stroked-button routerLink="/grievances/reviews">
        <mat-icon>fact_check</mat-icon>Open reviews
      </a>
    </app-page-header>

    @if (review.error() && !review.hasValue()) {
      <app-empty-state
        icon="error"
        title="The review figures could not be loaded"
        [message]="error(review.error())"
      >
        <button mat-flat-button type="button" (click)="review.reload()">Try again</button>
      </app-empty-state>
    } @else if (review.hasValue() ? review.value() : undefined; as r) {
      <section class="stats" aria-label="Review queue">
        <app-stat-card
          label="Proposals to decide"
          [value]="r.pendingProposals"
          icon="lightbulb"
          [tone]="r.pendingProposals > 0 ? 'warning' : 'neutral'"
          link="/grievances/reviews"
        />
        <app-stat-card
          label="Escalated"
          [value]="r.escalated"
          icon="priority_high"
          [tone]="r.escalated > 0 ? 'danger' : 'neutral'"
        />
        <app-stat-card
          label="Immediate escalations"
          [value]="r.immediateEscalations"
          icon="crisis_alert"
          [tone]="r.immediateEscalations > 0 ? 'danger' : 'neutral'"
        />
        <app-stat-card
          label="Overdue in the queue"
          [value]="r.overdue"
          icon="schedule"
          tone="neutral"
        />
        <app-stat-card
          label="Oldest proposal"
          [value]="r.oldestPendingDays === null ? '—' : r.oldestPendingDays + ' days'"
          icon="hourglass_bottom"
          tone="neutral"
        />
        <app-stat-card
          label="My decisions"
          [value]="r.decidedByMe.approved + ' approved · ' + r.decidedByMe.rejected + ' sent back'"
          icon="how_to_reg"
          tone="neutral"
        />
      </section>
      <div class="grid">
        <app-grievance-bar-chart
          title="Queue by priority"
          labelHeader="Priority"
          [bars]="queuePriority()"
          [slot]="4"
        />
        <app-grievance-bar-chart
          title="Queue by category"
          labelHeader="Category"
          [bars]="queueCategory()"
          [slot]="1"
        />
      </div>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the review figures" />
    }

    <section class="card" aria-labelledby="summary-heading">
      <div class="card__header"><h2 id="summary-heading">Grievance summary</h2></div>
      <div class="filters" role="group" aria-label="Summary filters">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Submitted from</mat-label>
          <input
            matInput
            [matDatepicker]="fromPicker"
            [value]="filters().submittedFrom ?? null"
            (dateChange)="set('submittedFrom', $event.value)"
          />
          <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
          <mat-datepicker #fromPicker />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Submitted to</mat-label>
          <input
            matInput
            [matDatepicker]="toPicker"
            [value]="filters().submittedTo ?? null"
            (dateChange)="set('submittedTo', $event.value)"
          />
          <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
          <mat-datepicker #toPicker />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Category</mat-label>
          <mat-select
            [value]="filters().categoryId ?? null"
            (selectionChange)="set('categoryId', $event.value)"
          >
            <mat-option [value]="null">All</mat-option>
            @for (
              c of (options.hasValue() ? options.value() : undefined)?.categories ?? [];
              track c.id
            ) {
              <mat-option [value]="c.id">{{ c.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Priority</mat-label>
          <mat-select
            [value]="filters().priority ?? null"
            (selectionChange)="set('priority', $event.value)"
          >
            <mat-option [value]="null">All</mat-option>
            @for (p of priorities; track p) {
              <mat-option [value]="p">{{ p }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <button mat-button type="button" (click)="filters.set({})">Clear</button>
      </div>
      @if (summary.error() && !summary.hasValue()) {
        <p class="muted" role="alert">{{ error(summary.error()) }}</p>
      } @else if (summary.hasValue() ? summary.value() : undefined; as s) {
        <section class="stats" aria-label="Summary figures">
          <app-stat-card label="Cases" [value]="s.total" icon="gavel" />
          <app-stat-card label="Open" [value]="s.open" icon="pending_actions" />
          <app-stat-card
            label="Resolved or closed"
            [value]="s.resolved + s.closed"
            icon="task_alt"
            tone="success"
          />
          <app-stat-card label="Rejected" [value]="s.rejected" icon="block" tone="neutral" />
          <app-stat-card
            label="Overdue"
            [value]="s.overdue"
            icon="schedule"
            [tone]="s.overdue > 0 ? 'danger' : 'neutral'"
          />
          <app-stat-card
            label="Resolution rate"
            [value]="percent(s.resolutionRatePct)"
            icon="donut_large"
            tone="success"
          />
          <app-stat-card
            label="Average resolution"
            [value]="days(s.averageResolutionDays)"
            icon="timer"
          />
        </section>
        <div class="grid">
          <app-grievance-bar-chart
            title="By status"
            labelHeader="Status"
            [bars]="summaryStatus()"
            [slot]="5"
          />
          <app-grievance-bar-chart
            title="By department"
            subtitle="Anonymous cases not disclosed, groups under 3 merged"
            labelHeader="Department"
            [bars]="summaryDepartment()"
            [slot]="3"
          />
        </div>
        <p class="muted">Figures as of {{ s.asOfDate | appDate }}.</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 12px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 16px;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      margin-bottom: 12px;
    }
    .muted {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GrievanceReviewSummaryComponent {
  private readonly insights = inject(GrievanceInsightDataService);
  private readonly mine = inject(MyGrievanceDataService);

  protected readonly priorities = GRIEVANCE_PRIORITIES;
  protected readonly percent = percentText;
  protected readonly days = daysText;
  protected readonly filters = signal<Partial<GrievanceSummaryFilter>>({});

  protected readonly review = rxResource({ stream: () => this.insights.reviewDashboard() });
  protected readonly summary = rxResource({
    params: () => this.filters(),
    stream: ({ params }) => this.insights.summary(params),
  });
  /** Category choices (every role holds `grievance:View`); empty when refused. */
  protected readonly options = rxResource({
    stream: () => this.mine.options().pipe(catchError(() => of(null))),
  });

  protected readonly queuePriority = computed(() =>
    priorityBars(safeValue(this.review)?.byPriority ?? []),
  );
  protected readonly queueCategory = computed(() =>
    categoryBars(safeValue(this.review)?.byCategory ?? []),
  );
  protected readonly summaryStatus = computed(() =>
    statusBars(safeValue(this.summary)?.byStatus ?? []),
  );
  protected readonly summaryDepartment = computed(() =>
    departmentBars(safeValue(this.summary)?.byDepartment ?? []),
  );

  protected set<K extends keyof GrievanceSummaryFilter>(
    key: K,
    value: GrievanceSummaryFilter[K] | null,
  ): void {
    this.filters.update((current) => {
      const next = { ...current };
      if (value === null || value === undefined || value === '') delete next[key];
      else next[key] = value;
      return next;
    });
  }

  protected error(e: unknown): string {
    return errorMessage(e);
  }
}
