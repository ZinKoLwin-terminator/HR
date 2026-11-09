import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { AttendanceTrendPoint } from '../../../core/models/dashboard.models';
import { ThemeService } from '../../../core/services/theme.service';
import {
  CHART_INK,
  SERIES_COLORS,
  axisStyle,
  baseChartOptions,
} from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';
import { parseIsoDate } from '../../../shared/utilities/date.util';

const SERIES = [
  { key: 'present', label: 'Present' },
  { key: 'late', label: 'Late' },
  { key: 'onLeave', label: 'On leave' },
  { key: 'absent', label: 'Absent' },
] as const;

@Component({
  selector: 'app-attendance-trend-chart',
  imports: [BaseChartDirective, ChartPanelComponent, DatePipe],
  template: `
    <app-chart-panel
      title="Attendance trend"
      subtitle="Scheduled employees per working day, last {{ points().length }} days"
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
          Attendance per day
        </caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            @for (s of series; track s.key) {
              <th scope="col" class="num">{{ s.label }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (p of points(); track p.date) {
            <tr>
              <th scope="row">{{ p.date | date: 'EEE, dd MMM' }}</th>
              @for (s of series; track s.key) {
                <td class="num">{{ p[s.key] }}</td>
              }
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class AttendanceTrendChartComponent {
  private readonly theme = inject(ThemeService);
  readonly points = input.required<AttendanceTrendPoint[]>();
  protected readonly series = SERIES;

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const colors = SERIES_COLORS[scheme];
    const surface = CHART_INK[scheme].surface;
    const points = this.points();
    return {
      labels: points.map((p) =>
        parseIsoDate(p.date).toLocaleDateString('en-US', { day: '2-digit', month: 'short' }),
      ),
      datasets: SERIES.map((s, index) => ({
        label: s.label,
        data: points.map((p) => p[s.key]),
        backgroundColor: colors[index],
        hoverBackgroundColor: colors[index],
        // 2px surface gap between stacked segments; rounded data-end on the top segment only.
        borderColor: surface,
        borderWidth: { top: 2 },
        borderSkipped: 'bottom',
        borderRadius: index === SERIES.length - 1 ? { topLeft: 4, topRight: 4 } : 0,
        maxBarThickness: 28,
        stack: 'attendance',
      })),
    };
  });

  protected readonly options = computed<ChartOptions<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const base = baseChartOptions(scheme) as ChartOptions<'bar'>;
    return {
      ...base,
      scales: {
        x: { stacked: true, ...axisStyle(scheme, false) },
        y: {
          stacked: true,
          beginAtZero: true,
          ...axisStyle(scheme, true),
          ticks: { ...axisStyle(scheme, true).ticks, precision: 0 },
        },
      },
      plugins: {
        ...base.plugins,
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            footer: (items) => `Scheduled: ${items.reduce((sum, i) => sum + Number(i.raw), 0)}`,
          },
        },
      },
    };
  });

  protected readonly summary = computed(() => {
    const points = this.points();
    const last = points.at(-1);
    return last
      ? `Stacked bar chart of daily attendance for ${points.length} working days. Latest day: ${last.present} present, ${last.late} late, ${last.onLeave} on leave, ${last.absent} absent. Switch to table view for all values.`
      : 'No attendance data.';
  });
}
