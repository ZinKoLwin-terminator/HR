import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { DepartmentAttendance } from '../../../core/models/attendance.models';
import { ThemeService } from '../../../core/services/theme.service';
import {
  CHART_INK,
  SERIES_COLORS,
  axisStyle,
  baseChartOptions,
} from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

/**
 * Status → palette slot. Matches the attendance trend chart so a status keeps its
 * colour everywhere (Present blue, Late orange, On leave aqua, Absent yellow).
 */
const SERIES = [
  { key: 'present', label: 'Present', slot: 0 },
  { key: 'late', label: 'Late', slot: 1 },
  { key: 'halfDay', label: 'Half day', slot: 4 },
  { key: 'onLeave', label: 'On leave', slot: 2 },
  { key: 'absent', label: 'Absent', slot: 3 },
] as const;

@Component({
  selector: 'app-department-attendance-chart',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel
      title="Attendance by department"
      subtitle="Scheduled employees on the selected day"
      [height]="chartHeight()"
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
          Attendance by department
        </caption>
        <thead>
          <tr>
            <th scope="col">Department</th>
            @for (s of series; track s.key) {
              <th scope="col" class="num">{{ s.label }}</th>
            }
            <th scope="col" class="num">Total</th>
          </tr>
        </thead>
        <tbody>
          @for (d of departments(); track d.departmentId) {
            <tr>
              <th scope="row">{{ d.department }}</th>
              @for (s of series; track s.key) {
                <td class="num">{{ d[s.key] }}</td>
              }
              <td class="num">{{ d.total }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class DepartmentAttendanceChartComponent {
  private readonly theme = inject(ThemeService);
  readonly departments = input.required<DepartmentAttendance[]>();
  protected readonly series = SERIES;

  protected readonly chartHeight = computed(() =>
    Math.max(220, this.departments().length * 34 + 70),
  );

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const colors = SERIES_COLORS[scheme];
    const surface = CHART_INK[scheme].surface;
    const rows = this.departments();
    return {
      labels: rows.map((d) => d.department),
      datasets: SERIES.map((s) => ({
        label: s.label,
        data: rows.map((d) => d[s.key]),
        backgroundColor: colors[s.slot],
        hoverBackgroundColor: colors[s.slot],
        // 2px surface gap between stacked segments.
        borderColor: surface,
        borderWidth: { right: 2 },
        borderSkipped: false,
        barThickness: 18,
        stack: 'attendance',
      })),
    };
  });

  protected readonly options = computed<ChartOptions<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const base = baseChartOptions(scheme) as ChartOptions<'bar'>;
    const axis = axisStyle(scheme, true);
    return {
      ...base,
      indexAxis: 'y',
      interaction: { mode: 'nearest', axis: 'y', intersect: false },
      scales: {
        x: { stacked: true, beginAtZero: true, ...axis, ticks: { ...axis.ticks, precision: 0 } },
        y: { stacked: true, ...axisStyle(scheme, false) },
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
    const rows = this.departments();
    return rows.length
      ? `Stacked bar chart of attendance status for ${rows.length} departments. Switch to table view for all values.`
      : 'No attendance data for this day.';
  });
}
