import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ThemeService } from '../../../core/services/theme.service';
import { SERIES_COLORS, axisStyle, baseChartOptions } from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

/** One bar: a label and a count. */
export interface GrievanceBar {
  label: string;
  value: number;
}

const LABEL_MAX = 18;

/**
 * Counts as horizontal bars in a chart panel with its table twin (the accessible view: exact values,
 * no colour needed), as the KPI dashboard draws them. The figures are the server's: this only draws.
 */
@Component({
  selector: 'app-grievance-bar-chart',
  imports: [BaseChartDirective, ChartPanelComponent],
  template: `
    <app-chart-panel [title]="title()" [subtitle]="subtitle()" [height]="height()">
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
          {{
            title()
          }}
        </caption>
        <thead>
          <tr>
            <th scope="col">{{ labelHeader() }}</th>
            <th scope="col" class="num">Cases</th>
          </tr>
        </thead>
        <tbody>
          @for (bar of bars(); track $index) {
            <tr>
              <th scope="row">{{ bar.label }}</th>
              <td class="num">{{ bar.value }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="2">Nothing to show.</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class GrievanceBarChartComponent {
  private readonly theme = inject(ThemeService);

  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly labelHeader = input('Name');
  readonly bars = input.required<readonly GrievanceBar[]>();
  /** Palette slot (0 to 7). */
  readonly slot = input(0);

  protected readonly height = computed(() => Math.max(180, this.bars().length * 30 + 48));
  protected readonly summary = computed(() => {
    const bars = this.bars();
    if (bars.length === 0) return `${this.title()}: nothing to show.`;
    const list = bars.map((b) => `${b.label} ${b.value}`).join(', ');
    return `Bar chart, ${this.title()}: ${list}. Switch to table view for the values.`;
  });

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const color = SERIES_COLORS[this.theme.scheme()][this.slot() % 8];
    return {
      labels: this.bars().map((b) =>
        b.label.length > LABEL_MAX ? `${b.label.slice(0, LABEL_MAX - 1)}…` : b.label,
      ),
      datasets: [
        {
          label: 'Cases',
          data: this.bars().map((b) => b.value),
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
        legend: { display: false },
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            title: (items) => this.bars()[items[0]?.dataIndex ?? 0]?.label ?? '',
          },
        },
      },
    };
  });
}
