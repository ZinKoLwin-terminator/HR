import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { CountByLabel } from '../../../core/models/dashboard.models';
import { ThemeService } from '../../../core/services/theme.service';
import { SERIES_COLORS, axisStyle, baseChartOptions } from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

/** Ranked horizontal bars: one series → one color (slot 1). */
@Component({
  selector: 'app-department-chart',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel
      title="Headcount by department"
      subtitle="Current employees, ranked"
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
          Employees per department
        </caption>
        <thead>
          <tr>
            <th scope="col">Department</th>
            <th scope="col" class="num">Employees</th>
            <th scope="col" class="num">Share</th>
          </tr>
        </thead>
        <tbody>
          @for (d of departments(); track d.label) {
            <tr>
              <th scope="row">{{ d.label }}</th>
              <td class="num">{{ d.count }}</td>
              <td class="num">{{ share(d.count) }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class DepartmentChartComponent {
  private readonly theme = inject(ThemeService);
  readonly departments = input.required<CountByLabel[]>();

  private readonly total = computed(() => this.departments().reduce((s, d) => s + d.count, 0));
  protected readonly chartHeight = computed(() =>
    Math.max(220, this.departments().length * 30 + 40),
  );

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const color = SERIES_COLORS[this.theme.scheme()][0];
    return {
      labels: this.departments().map((d) => d.label),
      datasets: [
        {
          label: 'Employees',
          data: this.departments().map((d) => d.count),
          backgroundColor: color,
          hoverBackgroundColor: color,
          borderRadius: { topRight: 4, bottomRight: 4 },
          borderSkipped: 'start',
          barThickness: 16,
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
      indexAxis: 'y',
      interaction: { mode: 'nearest', axis: 'y', intersect: false },
      scales: {
        x: { beginAtZero: true, ...axis, ticks: { ...axis.ticks, precision: 0 } },
        y: { ...axisStyle(scheme, false) },
      },
      plugins: {
        ...base.plugins,
        // Single series: the title names it, no legend box.
        legend: { display: false },
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            label: (item) => ` ${item.raw} employees (${this.share(Number(item.raw))})`,
          },
        },
      },
    };
  });

  protected share(count: number): string {
    const total = this.total();
    return total ? `${Math.round((count / total) * 100)}%` : '0%';
  }

  protected readonly summary = computed(() => {
    const top = this.departments()[0];
    return top
      ? `Bar chart of headcount by department. Largest: ${top.label} with ${top.count} of ${this.total()} employees. Switch to table view for all values.`
      : 'No department data.';
  });
}
