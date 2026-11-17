import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ThemeService } from '../../../core/services/theme.service';
import { SERIES_COLORS, axisStyle, baseChartOptions } from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';

/** One bar: its label, the value drawn (null draws nothing) and the text the table and tooltip show. */
export interface KpiBar {
  label: string;
  value: number | null;
  /** Shown in the table and the tooltip instead of the bare value (e.g. "80.88 · Exceeds Expectations"). */
  text?: string;
  /** Extra table columns, in the order of `extraHeaders`. */
  extra?: readonly string[];
}

/** Longest axis label drawn on the canvas; the table view always has the full text. */
const LABEL_MAX = 16;

/**
 * A ranked horizontal bar chart in a chart panel, with its table twin (the accessible view: exact
 * values, no colour needed). One series, one colour slot of the shared palette, light and dark
 * schemes from `ThemeService`, as the main dashboard's charts. The figures are the server's: this
 * only draws them. Horizontal bars keep long labels readable on a 390 px screen.
 */
@Component({
  selector: 'app-kpi-bar-chart',
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
            <th scope="col" class="num">{{ valueHeader() }}</th>
            @for (header of extraHeaders(); track header) {
              <th scope="col">{{ header }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (bar of bars(); track $index) {
            <tr>
              <th scope="row">{{ bar.label }}</th>
              <td class="num">{{ bar.text ?? (bar.value === null ? '—' : bar.value) }}</td>
              @for (cell of bar.extra ?? []; track $index) {
                <td>{{ cell }}</td>
              }
            </tr>
          } @empty {
            <tr>
              <td [attr.colspan]="2 + extraHeaders().length">Nothing to show.</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class KpiBarChartComponent {
  private readonly theme = inject(ThemeService);

  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly bars = input.required<readonly KpiBar[]>();
  /** What a bar measures ("Overall score", "Scorecards"): the series and the value column. */
  readonly valueHeader = input.required<string>();
  /** Header of the label column of the table ("Employee", "Period"). */
  readonly labelHeader = input('Name');
  readonly extraHeaders = input<readonly string[]>([]);
  /** Palette slot (0 to 7). */
  readonly slot = input(0);
  /** Whole numbers only on the value axis (counts). */
  readonly counts = input(false);
  /** The text alternative of the canvas: what the chart shows, in one or two sentences. */
  readonly summary = input.required<string>();

  protected readonly height = computed(() => Math.max(180, this.bars().length * 30 + 48));

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const color = SERIES_COLORS[this.theme.scheme()][this.slot() % 8];
    return {
      labels: this.bars().map((b) =>
        b.label.length > LABEL_MAX ? `${b.label.slice(0, LABEL_MAX - 1)}…` : b.label,
      ),
      datasets: [
        {
          label: this.valueHeader(),
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
        x: {
          beginAtZero: true,
          ...axis,
          ticks: { ...axis.ticks, ...(this.counts() ? { precision: 0 } : {}) },
        },
        y: { ...axisStyle(scheme, false) },
      },
      plugins: {
        ...base.plugins,
        // One series: the panel title names it, no legend box.
        legend: { display: false },
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            title: (items) => this.bars()[items[0]?.dataIndex ?? 0]?.label ?? '',
            label: (item) => {
              const bar = this.bars()[item.dataIndex];
              return ` ${this.valueHeader()}: ${bar?.text ?? item.raw}`;
            },
          },
        },
      },
    };
  });
}
