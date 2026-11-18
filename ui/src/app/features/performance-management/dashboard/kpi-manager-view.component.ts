import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { KpiManagerDashboard } from '../../../core/models/kpi-insight.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { KpiBar, KpiBarChartComponent } from './kpi-bar-chart.component';
import { scoreText } from './kpi-dashboard-views';

/**
 * The manager's dashboard for one period: the scorecards of their direct reports and those naming
 * them reviewer (never their own). Team size, pending reviews, approved or finalized, the team
 * average (Finalized only), the status breakdown and an employee comparison, all as the server
 * computed them.
 */
@Component({
  selector: 'app-kpi-manager-view',
  imports: [
    EmptyStateComponent,
    KpiBarChartComponent,
    RouterLink,
    StatCardComponent,
    StatusBadgeComponent,
  ],
  template: `
    @let d = dashboard();
    @if (d.teamSize === 0) {
      <app-empty-state
        icon="groups"
        title="No team scorecards"
        [message]="
          d.period
            ? 'None of your direct reports has a scorecard for ' + d.period.name + '.'
            : 'Your direct reports have no KPI scorecards yet.'
        "
      />
    } @else {
      <section class="stats" aria-label="Team figures">
        <app-stat-card
          label="Team size"
          [value]="d.teamSize"
          icon="groups"
          hint="Scorecards this period"
        />
        <app-stat-card
          label="Pending reviews"
          [value]="d.pendingReviews"
          icon="rate_review"
          tone="warning"
          hint="Waiting for you"
          link="/performance/reviews"
        />
        <app-stat-card
          label="Approved or finalized"
          [value]="d.approvedOrFinalized"
          icon="task_alt"
          tone="success"
          hint="Approved plus Finalized"
        />
        <app-stat-card
          label="Team average"
          [value]="score(d.averageScore)"
          icon="speed"
          tone="neutral"
          hint="Finalized scorecards only"
        />
      </section>

      <div class="grid">
        <app-kpi-bar-chart
          title="Status breakdown"
          subtitle="Team scorecards by status"
          valueHeader="Scorecards"
          labelHeader="Status"
          [counts]="true"
          [bars]="statusBars()"
          [summary]="statusSummary()"
          [slot]="1"
        />
        <app-kpi-bar-chart
          title="Employee comparison"
          subtitle="Overall score, by employee code"
          valueHeader="Overall score"
          labelHeader="Employee"
          [extraHeaders]="['Name', 'Status']"
          [bars]="comparisonBars()"
          [summary]="comparisonSummary()"
          [slot]="0"
        />
      </div>

      <section class="card" aria-labelledby="team-heading">
        <div class="card__header"><h2 id="team-heading">Team scorecards</h2></div>
        @if (d.employeesTruncated) {
          <p class="note" data-testid="team-truncated">
            Showing the first {{ d.employees.length }} of {{ d.employeeCount }} team scorecards, by
            employee code. The figures above count all of them; see
            <a routerLink="/performance/reports">KPI Reports</a> for the full list.
          </p>
        }
        <div class="scroll" role="region" aria-label="Team scorecards table" tabindex="0">
          <table class="simple-table">
            <caption class="cdk-visually-hidden">
              Team scorecards, by employee code
            </caption>
            <thead>
              <tr>
                <th scope="col">Code</th>
                <th scope="col">Employee</th>
                <th scope="col" class="hide-sm">Department</th>
                <th scope="col">Status</th>
                <th scope="col" class="num">Score</th>
              </tr>
            </thead>
            <tbody>
              @for (e of d.employees; track e.scorecardId) {
                <tr>
                  <th scope="row">
                    <a [routerLink]="['/performance/reviews', e.scorecardId]">{{
                      e.employeeCode
                    }}</a>
                  </th>
                  <td>{{ e.employeeName }}</td>
                  <td class="hide-sm">{{ e.departmentName }}</td>
                  <td><app-status-badge [status]="e.status" /></td>
                  <td class="num">{{ score(e.overallScore, e.ratingLabel) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
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
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 16px;
    }
    .scroll {
      overflow-x: auto;
    }
    .note {
      margin: 0 0 12px;
      font: var(--mat-sys-body-medium);
    }
    a {
      font-weight: 600;
      color: var(--mat-sys-primary);
    }
    @media (max-width: 599.98px) {
      .hide-sm {
        display: none;
      }
    }
    @media (max-width: 479.98px) {
      .stats,
      .grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class KpiManagerViewComponent {
  readonly dashboard = input.required<KpiManagerDashboard>();

  protected readonly score = scoreText;

  protected readonly statusBars = computed<KpiBar[]>(() =>
    this.dashboard().byStatus.map((s) => ({ label: s.status, value: s.count })),
  );

  protected readonly comparisonBars = computed<KpiBar[]>(() =>
    this.dashboard().employees.map((e) => ({
      label: e.employeeCode,
      value: e.overallScore,
      text: scoreText(e.overallScore, e.ratingLabel),
      extra: [e.employeeName, e.status],
    })),
  );

  protected readonly statusSummary = computed(() => {
    const present = this.dashboard().byStatus.filter((s) => s.count > 0);
    return `Bar chart of the ${this.dashboard().teamSize} team scorecard(s) by status: ${present
      .map((s) => `${s.count} ${s.status}`)
      .join(', ')}.`;
  });

  /** Text alternative of the comparison chart. The list is by employee code, so the highest is looked up. */
  protected readonly comparisonSummary = computed(() => {
    const d = this.dashboard();
    const scored = d.employees.filter((e) => e.overallScore !== null);
    const top = scored.reduce<(typeof scored)[number] | null>(
      (best, e) => (best === null || e.overallScore! > best.overallScore! ? e : best),
      null,
    );
    const listed = d.employeesTruncated
      ? ` (the first ${d.employees.length} of ${d.employeeCount} by employee code)`
      : '';
    return top
      ? `Bar chart of overall scores for ${scored.length} scored team member(s)${listed}. Highest: ${top.employeeCode} with ${scoreText(top.overallScore)}. Switch to table view for every value.`
      : 'No team member has an overall score yet.';
  });
}
