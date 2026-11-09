import { Component, computed, inject, input } from '@angular/core';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { PayrollRun } from '../../../core/models/payroll.models';
import { ThemeService } from '../../../core/services/theme.service';
import {
  CHART_INK,
  SERIES_COLORS,
  axisStyle,
  baseChartOptions,
} from '../../../shared/charts/chart-theme';
import { ChartPanelComponent } from '../../../shared/components/chart-panel/chart-panel.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';

const SERIES = [
  { key: 'totalGross', label: 'Gross', slot: 0 },
  { key: 'totalNet', label: 'Net', slot: 2 },
] as const;

/** Gross vs net per period (one money scale, two series: never a dual axis). */
@Component({
  selector: 'app-payroll-trend-chart',
  imports: [BaseChartDirective, ChartPanelComponent, MoneyPipe],
  template: `
    <app-chart-panel
      title="Gross and net pay"
      subtitle="Per processed period · sample figures"
      [height]="300"
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
          Gross and net pay per period
        </caption>
        <thead>
          <tr>
            <th scope="col">Period</th>
            <th scope="col" class="num">Gross</th>
            <th scope="col" class="num">Deductions</th>
            <th scope="col" class="num">Net</th>
          </tr>
        </thead>
        <tbody>
          @for (r of runs(); track r.id) {
            <tr>
              <th scope="row">{{ r.periodName }}</th>
              <td class="num">{{ r.totalGross | money }}</td>
              <td class="num">{{ r.totalDeductions | money }}</td>
              <td class="num">{{ r.totalNet | money }}</td>
            </tr>
          }
        </tbody>
      </table>
    </app-chart-panel>
  `,
})
export class PayrollTrendChartComponent {
  private readonly theme = inject(ThemeService);
  readonly runs = input.required<PayrollRun[]>();

  protected readonly data = computed<ChartData<'bar'>>(() => {
    const scheme = this.theme.scheme();
    const colors = SERIES_COLORS[scheme];
    const runs = this.runs();
    return {
      labels: runs.map((r) => `${r.periodName.slice(0, 3)} ${String(r.year).slice(2)}`),
      datasets: SERIES.map((s) => ({
        label: s.label,
        data: runs.map((r) => r[s.key]),
        backgroundColor: colors[s.slot],
        hoverBackgroundColor: colors[s.slot],
        borderColor: CHART_INK[scheme].surface,
        borderWidth: 2,
        borderRadius: 4,
        maxBarThickness: 26,
      })),
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
        y: {
          beginAtZero: true,
          ...axis,
          ticks: { ...axis.ticks, callback: (v) => `$${Number(v) / 1000}k` },
        },
      },
      plugins: {
        ...base.plugins,
        tooltip: {
          ...base.plugins?.tooltip,
          callbacks: {
            label: (item) => ` ${item.dataset.label}: $${Number(item.raw).toLocaleString('en-US')}`,
          },
        },
      },
    };
  });

  protected readonly summary = computed(() => {
    const runs = this.runs();
    const last = runs.at(-1);
    return last
      ? `Bar chart of gross and net pay for ${runs.length} periods. Latest, ${last.periodName}: gross ${Math.round(last.totalGross)}, net ${Math.round(last.totalNet)} (sample). Switch to table view for all values.`
      : 'No payroll data.';
  });
}
