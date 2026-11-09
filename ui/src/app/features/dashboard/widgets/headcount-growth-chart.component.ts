import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions, Plugin } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { HeadcountGrowthPoint } from '../../../core/models/dashboard.models';
import { ThemeService } from '../../../core/services/theme.service';
import {
  CHART_INK,
  SERIES_COLORS,
  axisStyle,
  baseChartOptions,
  crosshairPlugin,
} from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

function monthLabel(month: string, style: 'short' | 'long' = 'short'): string {
  return new Date(`${month}-01T00:00:00`).toLocaleDateString('en-US', {
    month: style,
    year: style === 'long' ? 'numeric' : '2-digit',
  });
}

/**
 * Single-series line (headcount). Joiners/exits are a different scale, so they
 * live in the tooltip and table — never on a second y-axis.
 */
@Component({
  selector: 'app-headcount-growth-chart',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel title="Employee growth" subtitle="Headcount at month end, last 12 months">
      <canvas
        chart
        baseChart
        type="line"
        [data]="data()"
        [options]="options()"
        [plugins]="plugins()"
        role="img"
        [attr.aria-label]="summary()"
      ></canvas>
      <table table class="simple-table">
        <caption class="cdk-visually-hidden">
          Headcount by month
        </caption>
        <thead>
          <tr>
            <th scope="col">Month</th>
            <th scope="col" class="num">Headcount</th>
            <th scope="col" class="num">Joiners</th>
            <th scope="col" class="num">Exits</th>
          </tr>
        </thead>
        <tbody>
          @for (p of points(); track p.month) {
            <tr>
              <th scope="row">{{ label(p.month) }}</th>
              <td class="num">{{ p.headcount }}</td>
              <td class="num">{{ p.joiners }}</td>
              <td class="num">{{ p.exits }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class HeadcountGrowthChartComponent {
  private readonly theme = inject(ThemeService);
  readonly points = input.required<HeadcountGrowthPoint[]>();

  protected readonly plugins = computed<Plugin<'line'>[]>(() => [
    crosshairPlugin(this.theme.scheme()) as Plugin<'line'>,
  ]);

  protected readonly data = computed<ChartData<'line'>>(() => {
    const scheme = this.theme.scheme();
    const color = SERIES_COLORS[scheme][0];
    return {
      labels: this.points().map((p) => monthLabel(p.month)),
      datasets: [
        {
          label: 'Headcount',
          data: this.points().map((p) => p.headcount),
          borderColor: color,
          backgroundColor: color,
          borderWidth: 2,
          tension: 0.25,
          pointRadius: 0,
          pointHoverRadius: 5,
          pointHitRadius: 14,
          pointHoverBorderWidth: 2,
          pointHoverBorderColor: CHART_INK[scheme].surface,
        },
      ],
    };
  });

  protected readonly options = computed<ChartOptions<'line'>>(() => {
    const scheme = this.theme.scheme();
    const base = baseChartOptions(scheme) as ChartOptions<'line'>;
    const axis = axisStyle(scheme, true);
    return {
      ...base,
      scales: {
        x: { ...axisStyle(scheme, false) },
        // Anchor well below the data so small month-to-month moves are not exaggerated.
        y: {
          ...axis,
          ticks: { ...axis.ticks, precision: 0 },
          suggestedMin: Math.floor(Math.min(...this.points().map((p) => p.headcount)) * 0.8),
          grace: '10%',
        },
      },
      plugins: {
        ...base.plugins,
        legend: { display: false },
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            title: (items) => monthLabel(this.points()[items[0].dataIndex].month, 'long'),
            label: (item) => ` Headcount: ${item.raw}`,
            afterBody: (items) => {
              const p = this.points()[items[0].dataIndex];
              return [`Joiners: ${p.joiners}`, `Exits: ${p.exits}`];
            },
          },
        },
      },
    };
  });

  protected label(month: string): string {
    return monthLabel(month, 'long');
  }

  protected readonly summary = computed(() => {
    const points = this.points();
    const first = points[0];
    const last = points.at(-1);
    return first && last
      ? `Line chart of headcount from ${monthLabel(first.month, 'long')} (${first.headcount}) to ${monthLabel(last.month, 'long')} (${last.headcount}). Switch to table view for all values.`
      : 'No headcount data.';
  });
}
