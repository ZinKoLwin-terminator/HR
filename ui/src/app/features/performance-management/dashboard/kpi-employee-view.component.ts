import { Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { KpiEmployeeDashboard } from '../../../core/models/kpi-insight.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { KpiBar, KpiBarChartComponent } from './kpi-bar-chart.component';
import { fixed2, scoreText, withUnit } from './kpi-dashboard-views';

/**
 * The employee's own KPI dashboard for one period: the pending-submission banner (overdue when the
 * due date has passed), the overall score and rating, the KPIs with target, actual, achievement and
 * weight, the manager's feedback, and the earlier periods as a chart and a list. Every figure is the
 * server's; nothing is calculated here.
 */
@Component({
  selector: 'app-kpi-employee-view',
  imports: [
    AppDatePipe,
    EmptyStateComponent,
    KpiBarChartComponent,
    MatButtonModule,
    MatIconModule,
    RouterLink,
    StatCardComponent,
    StatusBadgeComponent,
  ],
  template: `
    @let d = dashboard();
    @if (!d.period) {
      <app-empty-state
        icon="trending_up"
        title="No KPI scorecards yet"
        message="Your dashboard fills in once HR has finalized your KPI plan for an evaluation period."
      />
    } @else {
      @if (d.pending.isPending && d.scorecard) {
        <div
          class="banner"
          [class.banner--overdue]="d.pending.isOverdue"
          role="status"
          data-testid="pending-banner"
        >
          <mat-icon aria-hidden="true">{{
            d.pending.isOverdue ? 'warning' : 'pending_actions'
          }}</mat-icon>
          <p>
            @if (d.pending.isOverdue) {
              <strong>Overdue:</strong> your results for {{ d.period.name }} were due on
              {{ d.pending.dueDate | appDate }}. You can still submit; it will be marked Late.
            } @else {
              <strong>Submission pending:</strong> enter your results for {{ d.period.name }} and
              submit them{{ d.pending.dueDate ? ' by ' : ''
              }}{{ d.pending.dueDate ? (d.pending.dueDate | appDate) : '' }}.
            }
          </p>
          <a mat-stroked-button [routerLink]="['/performance/my-kpis', d.scorecard.id]"
            >Open my scorecard</a
          >
        </div>
      }

      @if (d.scorecard; as card) {
        <section class="stats" aria-label="My score">
          <app-stat-card
            label="Overall score"
            [value]="score(card.overallScore)"
            icon="speed"
            [hint]="card.ratingLabel ?? 'Not scored yet'"
          />
          <div class="status card">
            <span class="label">Status</span>
            <app-status-badge [status]="card.status" />
            <span class="meta">
              Reviewer: {{ card.reviewerName ?? 'HR' }}
              @if (card.submittedOn) {
                · Submitted {{ card.submittedOn | appDate }}
              }
              @if (card.finalizedOn) {
                · Finalized {{ card.finalizedOn | appDate }}
              }
            </span>
          </div>
        </section>

        <section class="card" aria-labelledby="my-kpis-heading">
          <div class="card__header">
            <h2 id="my-kpis-heading">My KPIs</h2>
            <a mat-button [routerLink]="['/performance/my-kpis', card.id]">Open scorecard</a>
          </div>
          <div class="scroll" role="region" aria-label="My KPIs table" tabindex="0">
            <table class="simple-table">
              <caption class="cdk-visually-hidden">
                My KPIs for
                {{
                  d.period.name
                }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">KPI</th>
                  <th scope="col" class="num">Target</th>
                  <th scope="col" class="num">Actual</th>
                  <th scope="col" class="num">Achievement %</th>
                  <th scope="col" class="num">Weight %</th>
                  <th scope="col" class="num">Weighted score</th>
                </tr>
              </thead>
              <tbody>
                @for (item of d.items; track item.id) {
                  <tr>
                    <th scope="row">
                      <span class="code">{{ item.kpiCode }}</span> {{ item.kpiName }}
                    </th>
                    <td class="num">{{ unit(item.target, item.unit) }}</td>
                    <td class="num">{{ unit(item.actualValue, item.unit) }}</td>
                    <td class="num">{{ fixed(item.achievementPct) }}</td>
                    <td class="num">{{ fixed(item.weight) }}</td>
                    <td class="num">{{ fixed(item.weightedScore) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="card" aria-labelledby="feedback-heading">
          <div class="card__header"><h2 id="feedback-heading">Manager feedback</h2></div>
          @let fb = d.feedback;
          @if (fb && (fb.managerRating !== null || fb.managerComment)) {
            <p data-testid="manager-rating">
              <strong>Overall rating:</strong>
              {{ fb.managerRating === null ? '—' : fb.managerRating + ' of 5' }}
            </p>
            <p class="comment">{{ fb.managerComment ?? 'No comment.' }}</p>
          } @else {
            <p class="muted">No feedback yet. It appears here once your reviewer has rated you.</p>
          }
        </section>
      } @else {
        <app-empty-state
          icon="event_busy"
          title="No scorecard for this period"
          [message]="'You have no KPI scorecard for ' + d.period.name + '. Choose another period.'"
        />
      }

      <div class="history">
        @if (d.history.length) {
          <app-kpi-bar-chart
            title="Score history"
            subtitle="Earlier periods, newest first"
            valueHeader="Overall score"
            labelHeader="Period"
            [extraHeaders]="['Status']"
            [bars]="historyBars()"
            [summary]="historySummary()"
            [slot]="0"
          />
          <section class="card" aria-labelledby="history-heading">
            <div class="card__header"><h2 id="history-heading">Earlier scorecards</h2></div>
            <ul class="history-list">
              @for (h of d.history; track h.scorecardId) {
                <li>
                  <a [routerLink]="['/performance/my-kpis', h.scorecardId]">{{ h.periodName }}</a>
                  <span class="muted">{{ h.periodType }}</span>
                  <app-status-badge [status]="h.status" />
                  <span class="num">{{ score(h.overallScore, h.ratingLabel) }}</span>
                </li>
              }
            </ul>
          </section>
        } @else {
          <p class="muted">No earlier periods to compare with yet.</p>
        }
      </div>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px 14px;
      padding: 12px 16px;
      border-radius: var(--app-radius-md);
      border: 1px solid var(--app-on-warning-container);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .banner p {
      flex: 1 1 240px;
      margin: 0;
    }
    .banner--overdue {
      border-color: var(--mat-sys-error);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
    }
    .status {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 14px 16px;
    }
    .label {
      font: var(--mat-sys-label-medium);
      font-weight: 600;
      color: var(--mat-sys-on-surface-variant);
    }
    .meta,
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .scroll {
      overflow-x: auto;
    }
    .code {
      font-weight: 600;
    }
    .comment {
      white-space: pre-line;
      margin: 0;
    }
    .history {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 16px;
    }
    .history-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .history-list li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .history-list a {
      font-weight: 600;
      color: var(--mat-sys-primary);
    }
    .history-list .num {
      margin-left: auto;
    }
    @media (max-width: 479.98px) {
      .stats,
      .history {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class KpiEmployeeViewComponent {
  readonly dashboard = input.required<KpiEmployeeDashboard>();

  protected readonly score = scoreText;
  protected readonly fixed = fixed2;
  protected readonly unit = withUnit;

  protected readonly historyBars = computed<KpiBar[]>(() =>
    this.dashboard().history.map((h) => ({
      label: h.periodName,
      value: h.overallScore,
      text: scoreText(h.overallScore, h.ratingLabel),
      extra: [h.status],
    })),
  );

  protected readonly historySummary = computed(() => {
    const scored = this.dashboard().history.filter((h) => h.overallScore !== null);
    return scored.length
      ? `Bar chart of your overall score in ${scored.length} earlier period(s): ${scored
          .map((h) => `${h.periodName} ${scoreText(h.overallScore)}`)
          .join(', ')}. Switch to table view for the statuses.`
      : 'No earlier period has an overall score yet.';
  });
}
