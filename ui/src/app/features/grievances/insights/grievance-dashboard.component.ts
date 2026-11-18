import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { BarController, BarElement, CategoryScale, Legend, LinearScale, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';
import {
  GRIEVANCE_HISTORY_ACTION_LABELS,
  GRIEVANCE_STATUS_LABELS,
} from '../../../core/models/grievance.models';
import { GrievanceInsightDataService } from '../../../core/services/data/grievance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { RelativeTimePipe } from '../../../shared/pipes/relative-time.pipe';
import { GrievanceBarChartComponent } from './grievance-bar-chart.component';
import {
  categoryBars,
  daysText,
  departmentBars,
  percentText,
  priorityBars,
  statusBars,
} from './grievance-insight-bars';
import { GrievanceTrendChartComponent } from './grievance-trend-chart.component';
import { safeValue } from '../shared/resource-value';

/**
 * HR's grievance dashboard (`grievance-management:View`): the headline counts, resolution rate and
 * average resolution days, charts by category, department (anonymous cases "Not disclosed", small
 * departments merged into "Other / suppressed"), priority, status and month, and the latest steps by
 * case number only. Every figure is the server's and is the same for every viewer.
 */
@Component({
  selector: 'app-grievance-dashboard',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    GrievanceBarChartComponent,
    GrievanceTrendChartComponent,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    RelativeTimePipe,
    RouterLink,
    StatCardComponent,
  ],
  // Chart.js parts are registered here so they load with this lazy route only.
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Legend, Tooltip],
    }),
  ],
  template: `
    <app-page-header
      title="Grievance dashboard"
      description="Every submitted grievance at a glance. Department groups under three cases are merged so no one can be singled out."
    >
      <a pageActions mat-stroked-button routerLink="/grievances/reports">
        <mat-icon>query_stats</mat-icon>Reports
      </a>
    </app-page-header>

    @if (dashboard.error() && !dashboard.hasValue()) {
      <app-empty-state
        icon="error"
        title="The dashboard could not be loaded"
        [message]="errorText()"
      >
        <button mat-flat-button type="button" (click)="dashboard.reload()">Try again</button>
      </app-empty-state>
    } @else if (dashboard.hasValue() ? dashboard.value() : undefined; as d) {
      <section class="stats" aria-label="Key figures">
        <app-stat-card label="Total" [value]="d.total" icon="gavel" hint="Every submitted case" />
        <app-stat-card
          label="New"
          [value]="d.new"
          icon="fiber_new"
          tone="warning"
          hint="Waiting for acknowledgement"
          link="/grievances/cases"
        />
        <app-stat-card
          label="Under review"
          [value]="d.underReview"
          icon="rate_review"
          hint="Acknowledged, in review or reopened"
        />
        <app-stat-card
          label="Under investigation"
          [value]="d.underInvestigation"
          icon="manage_search"
          hint="Assigned or investigating"
        />
        <app-stat-card
          label="Awaiting the employee"
          [value]="d.awaitingEmployeeResponse"
          icon="help"
          tone="neutral"
        />
        <app-stat-card
          label="Pending resolution"
          [value]="d.pendingResolution"
          icon="lightbulb"
          tone="warning"
          hint="Proposals to decide"
        />
        <app-stat-card label="Resolved" [value]="d.resolved" icon="task_alt" tone="success" />
        <app-stat-card label="Closed" [value]="d.closed" icon="inventory_2" tone="neutral" />
        <app-stat-card
          label="Overdue"
          [value]="d.overdue"
          icon="schedule"
          [tone]="d.overdue > 0 ? 'danger' : 'neutral'"
          hint="Open past the target date"
        />
        <app-stat-card
          label="Escalated"
          [value]="d.escalated"
          icon="priority_high"
          [tone]="d.escalated > 0 ? 'danger' : 'neutral'"
          hint="Open and escalated"
        />
        <app-stat-card
          label="Resolution rate"
          [value]="percent(d.resolutionRatePct)"
          icon="donut_large"
          tone="success"
          hint="Resolved or closed, rejected cases left out"
        />
        <app-stat-card
          label="Average resolution"
          [value]="days(d.averageResolutionDays)"
          icon="timer"
          hint="Submission to resolution"
        />
      </section>

      <div class="grid">
        <app-grievance-trend-chart [months]="d.monthlyTrend" />
        <app-grievance-bar-chart
          title="By category"
          subtitle="Cases per category"
          labelHeader="Category"
          [bars]="byCategory()"
          [slot]="1"
        />
        <app-grievance-bar-chart
          title="By department"
          subtitle="At submission; anonymous cases not disclosed, groups under 3 merged"
          labelHeader="Department"
          [bars]="byDepartment()"
          [slot]="3"
        />
        <app-grievance-bar-chart
          title="By priority"
          labelHeader="Priority"
          [bars]="byPriority()"
          [slot]="4"
        />
        <app-grievance-bar-chart
          title="By status"
          labelHeader="Status"
          [bars]="byStatus()"
          [slot]="5"
        />
        <section class="card" aria-labelledby="recent-heading">
          <div class="card__header"><h2 id="recent-heading">Recent activity</h2></div>
          @if (d.recentActivity.length === 0) {
            <p class="muted">Nothing has happened yet.</p>
          } @else {
            <ul class="recent">
              @for (a of d.recentActivity; track $index) {
                <li>
                  <a [routerLink]="['/grievances/cases', a.caseId]">{{ a.caseNo }}</a>
                  {{ actionLabels[a.action] }} · {{ statusLabels[a.toStatus] }}
                  <time
                    class="muted"
                    [attr.datetime]="a.occurredAtUtc"
                    [title]="a.occurredAtUtc | appDate"
                    >{{ a.occurredAtUtc | relativeTime }}</time
                  >
                </li>
              }
            </ul>
          }
        </section>
      </div>
      <p class="muted">Figures as of {{ d.asOfDate | appDate }}.</p>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the dashboard" />
    }
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
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 16px;
    }
    .recent {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .recent a {
      font-weight: 600;
      color: var(--mat-sys-primary);
    }
    .muted {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GrievanceDashboardComponent {
  private readonly insights = inject(GrievanceInsightDataService);

  protected readonly actionLabels = GRIEVANCE_HISTORY_ACTION_LABELS;
  protected readonly statusLabels = GRIEVANCE_STATUS_LABELS;
  protected readonly percent = percentText;
  protected readonly days = daysText;

  protected readonly dashboard = rxResource({ stream: () => this.insights.hrDashboard() });
  protected readonly byCategory = computed(() =>
    categoryBars(safeValue(this.dashboard)?.byCategory ?? []),
  );
  protected readonly byDepartment = computed(() =>
    departmentBars(safeValue(this.dashboard)?.byDepartment ?? []),
  );
  protected readonly byPriority = computed(() =>
    priorityBars(safeValue(this.dashboard)?.byPriority ?? []),
  );
  protected readonly byStatus = computed(() =>
    statusBars(safeValue(this.dashboard)?.byStatus ?? []),
  );
  protected readonly errorText = computed(() =>
    this.dashboard.error() ? errorMessage(this.dashboard.error()) : '',
  );
}
