import { Component, computed, input } from '@angular/core';
import { KpiHrDashboard } from '../../../core/models/kpi-insight.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { KpiBar, KpiBarChartComponent } from './kpi-bar-chart.component';
import { fixed2, scoreText } from './kpi-dashboard-views';
import { KpiPeopleTableComponent } from './kpi-people-table.component';

/**
 * HR's dashboard for one period (every scorecard, optionally of one company): the headline figures,
 * department performance, top performers, the employees requiring attention with the threshold the
 * server used, and the rating distribution. Displayed exactly as the server computed them.
 */
@Component({
  selector: 'app-kpi-hr-view',
  imports: [EmptyStateComponent, KpiBarChartComponent, KpiPeopleTableComponent, StatCardComponent],
  template: `
    @let d = dashboard();
    @if (!d.period) {
      <app-empty-state
        icon="insights"
        title="No evaluation period yet"
        message="The HR overview fills in once an evaluation period is active."
      />
    } @else {
      <section class="stats" aria-label="Key figures">
        <app-stat-card
          label="Employees with KPIs"
          [value]="d.employeesWithKpis"
          icon="badge"
          hint="Scorecards with a KPI"
        />
        <app-stat-card
          label="KPI assignments"
          [value]="d.totalAssignments"
          icon="assignment"
          tone="neutral"
          hint="KPIs of the period"
        />
        <app-stat-card
          label="Completed evaluations"
          [value]="d.completedEvaluations"
          icon="task_alt"
          tone="success"
          hint="Finalized"
        />
        <app-stat-card
          label="Pending submissions"
          [value]="d.pendingSubmissions"
          icon="pending_actions"
          tone="warning"
          hint="Draft or Returned"
        />
        <app-stat-card
          label="Pending approvals"
          [value]="d.pendingApprovals"
          icon="rate_review"
          tone="warning"
          hint="In review or Approved"
        />
        <app-stat-card
          label="Average score"
          [value]="score(d.averageScore)"
          icon="speed"
          hint="Finalized scorecards only"
        />
        <app-stat-card
          label="Completion rate"
          [value]="fixed(d.completionRatePct) + '%'"
          icon="donut_large"
          tone="success"
          hint="Finalized of those past Planning"
        />
      </section>

      <div class="grid">
        <app-kpi-bar-chart
          title="Department performance"
          subtitle="Average score of Finalized scorecards"
          valueHeader="Average score"
          labelHeader="Department"
          [extraHeaders]="['Scorecards', 'Finalized']"
          [bars]="departmentBars()"
          [summary]="departmentSummary()"
          [slot]="0"
        />
        <app-kpi-bar-chart
          title="Rating distribution"
          subtitle="Finalized scorecards per rating"
          valueHeader="Scorecards"
          labelHeader="Rating"
          [counts]="true"
          [bars]="ratingBars()"
          [summary]="ratingSummary()"
          [slot]="2"
        />
      </div>

      <div class="grid">
        <section class="card" aria-labelledby="top-heading">
          <div class="card__header">
            <h2 id="top-heading">Top performers</h2>
          </div>
          <app-kpi-people-table
            [rows]="d.topPerformers"
            caption="Top performers, highest score first"
            none="No finalized scorecard yet."
          />
        </section>
        <section class="card" aria-labelledby="attention-heading">
          <div class="card__header">
            <h2 id="attention-heading">Employees requiring attention</h2>
          </div>
          <p class="note" data-testid="attention-threshold">{{ attentionNote() }}</p>
          <app-kpi-people-table
            [rows]="d.requiringAttention"
            caption="Employees requiring attention, lowest score first"
            none="Nobody is below the threshold."
          />
        </section>
      </div>
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
    .note {
      margin: 0 0 12px;
      font: var(--mat-sys-body-medium);
    }
    @media (max-width: 479.98px) {
      .stats {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class KpiHrViewComponent {
  readonly dashboard = input.required<KpiHrDashboard>();

  protected readonly score = scoreText;
  protected readonly fixed = fixed2;
  /** What the attention list means, with the threshold the server used. */
  protected readonly attentionNote = computed(() => {
    const d = this.dashboard();
    const listed =
      d.requiringAttentionCount > d.requiringAttention.length
        ? `; the lowest ${d.requiringAttention.length} are listed`
        : '';
    return `Finalized scorecards below ${fixed2(d.attentionThreshold)} (the lowest active rating band that starts at 60 or above, or 60 when there is none): ${d.requiringAttentionCount}${listed}.`;
  });

  protected readonly departmentBars = computed<KpiBar[]>(() =>
    this.dashboard().departments.map((d) => ({
      label: d.departmentName,
      value: d.averageScore,
      text: scoreText(d.averageScore),
      extra: [String(d.scorecards), String(d.finalized)],
    })),
  );

  protected readonly ratingBars = computed<KpiBar[]>(() =>
    this.dashboard().ratingDistribution.map((r) => ({
      label: r.ratingLabel ?? 'Below every band',
      value: r.count,
    })),
  );

  protected readonly departmentSummary = computed(() => {
    const scored = this.dashboard().departments.filter((d) => d.averageScore !== null);
    return scored.length
      ? `Bar chart of the average score per department: ${scored
          .map((d) => `${d.departmentName} ${scoreText(d.averageScore)}`)
          .join(', ')}. Departments without a finalized scorecard have no bar.`
      : 'No department has a finalized scorecard yet.';
  });

  protected readonly ratingSummary = computed(() => {
    const rows = this.dashboard().ratingDistribution;
    return rows.length
      ? `Bar chart of finalized scorecards per rating: ${rows
          .map((r) => `${r.count} ${r.ratingLabel ?? 'below every band'}`)
          .join(', ')}.`
      : 'No finalized scorecard yet.';
  });
}
