import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { GrievanceMonthlyCount } from '../../../core/models/grievance-insight.models';
import { ThemeService } from '../../../core/services/theme.service';
import { SERIES_COLORS, axisStyle, baseChartOptions } from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';
import { parseIsoDate } from '../../../shared/utilities/date.util';

/** "2026-11" as "Nov 2026". */
export function monthLabel(month: string): string {
  return parseIsoDate(`${month}-01`).toLocaleDateString('en-US', {
    month: 'short',
    year: 'numeric',
  });
}

/**
 * The last 12 months: cases submitted and cases resolved per month, side by side, with the table
 * twin. The figures are the server's.
 */
@Component({
  selector: 'app-grievance-trend-chart',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel
      title="Monthly trend"
      subtitle="Submitted and resolved per month, last 12 months"
      [height]="240"
    >
      <canvas
        chart
        baseChart
        type="bar"
        [data]="data()"
        [options]="options()"
        role="img"
        [attr.aria-label]="summary()"
      ></canvas>
      <table table class="simple-table">
        <caption class="cdk-visually-hidden">
          Submitted and resolved per month
        </caption>
        <thead>
          <tr>
            <th scope="col">Month</th>
            <th scope="col" class="num">Submitted</th>
            <th scope="col" class="num">Resolved</th>
          </tr>
        </thead>
        <tbody>
          @for (m of months(); track m.month) {
            <tr>
              <th scope="row">{{ label(m.month) }}</th>
              <td class="num">{{ m.submitted }}</td>
              <td class="num">{{ m.resolved }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class GrievanceTrendChartComponent {
  private readonly theme = inject(ThemeService);
  readonly months = input.required<readonly GrievanceMonthlyCount[]>();
  protected readonly label = monthLabel;

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const colors = SERIES_COLORS[this.theme.scheme()];
    const months = this.months();
    return {
      labels: months.map((m) => monthLabel(m.month)),
      datasets: [
        {
          label: 'Submitted',
          data: months.map((m) => m.submitted),
          backgroundColor: colors[0],
          hoverBackgroundColor: colors[0],
          borderRadius: 4,
          maxBarThickness: 18,
        },
        {
          label: 'Resolved',
          data: months.map((m) => m.resolved),
          backgroundColor: colors[2],
          hoverBackgroundColor: colors[2],
          borderRadius: 4,
          maxBarThickness: 18,
        },
      ],
    };
  });

  protected readonly options = computed<ChartOptions<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const base = baseChartOptions(scheme) as ChartOptions<'bar'>;
    const axis = axisStyle(scheme, true);
    return {
      ...base,
      scales: {
        x: { ...axisStyle(scheme, false) },
        y: { beginAtZero: true, ...axis, ticks: { ...axis.ticks, precision: 0 } },
      },
    };
  });

  protected readonly summary = computed(() => {
    const months = this.months();
    const submitted = months.reduce((n, m) => n + m.submitted, 0);
    const resolved = months.reduce((n, m) => n + m.resolved, 0);
    return `Grouped bar chart of the last ${months.length} months: ${submitted} submitted and ${resolved} resolved in all. Switch to table view for each month.`;
  });
}
