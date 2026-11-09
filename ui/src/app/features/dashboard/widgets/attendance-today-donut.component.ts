import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions, Plugin } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { TodayAttendanceSummary } from '../../../core/models/dashboard.models';
import { ThemeService } from '../../../core/services/theme.service';
import { CHART_INK, SERIES_COLORS, baseChartOptions } from '../../../shared/charts/chart-theme';
import { centerTextPlugin } from '../../../shared/charts/center-text.plugin';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

/** Doughnut of today's attendance split (same slot colors as the trend chart). */
@Component({
  selector: 'app-attendance-today-donut',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel
      title="Attendance overview"
      subtitle="Today, share of scheduled employees"
      [height]="200"
    >
      <canvas
        chart
        baseChart
        type="doughnut"
        [data]="data()"
        [options]="options()"
        [plugins]="plugins()"
        role="img"
        [attr.aria-label]="summary()"
      ></canvas>
      <table table class="simple-table">
        <caption class="cdk-visually-hidden">
          Attendance today
        </caption>
        <thead>
          <tr>
            <th scope="col">Status</th>
            <th scope="col" class="num">Employees</th>
          </tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.label) {
            <tr>
              <th scope="row">{{ r.label }}</th>
              <td class="num">{{ r.value }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class AttendanceTodayDonutComponent {
  private readonly theme = inject(ThemeService);
  readonly today = input.required<TodayAttendanceSummary>();

  protected readonly rows = computed(() => {
    const t = this.today();
    return [
      { label: 'Present', value: t.present + t.halfDay },
      { label: 'Late', value: t.late },
      { label: 'On leave', value: t.onLeave },
      { label: 'Absent', value: t.absent },
    ];
  });

  protected readonly data = computed<ChartData<'doughnut'>>(() => {
    const scheme = this.theme.scheme();
    return {
      labels: this.rows().map((r) => r.label),
      datasets: [
        {
          data: this.rows().map((r) => r.value),
          backgroundColor: SERIES_COLORS[scheme].slice(0, 4),
          borderColor: CHART_INK[scheme].surface,
          borderWidth: 3,
          hoverOffset: 4,
        },
      ],
    };
  });

  protected readonly plugins = computed<Plugin<'doughnut'>[]>(() => [
    centerTextPlugin(this.theme.scheme(), `${this.today().attendanceRate}%`, 'checked in'),
  ]);

  protected readonly options = computed<ChartOptions<'doughnut'>>(() => {
    const base = baseChartOptions(this.theme.scheme()) as ChartOptions<'doughnut'>;
    return {
      ...base,
      interaction: { mode: 'nearest', intersect: true },
      cutout: '70%',
      plugins: {
        ...base.plugins,
        legend: { ...base.plugins?.legend, position: 'right', align: 'center' },
      },
    };
  });

  protected readonly summary = computed(
    () =>
      `Doughnut chart of today's attendance: ${this.rows()
        .map((r) => `${r.value} ${r.label.toLowerCase()}`)
        .join(', ')}.`,
  );
}
