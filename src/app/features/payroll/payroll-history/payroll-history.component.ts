import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { BarController, BarElement, CategoryScale, Legend, LinearScale, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';
import { PAYROLL_RUN_STATUSES, PayrollRun } from '../../../core/models/payroll.models';
import { PayrollRunDataService } from '../../../core/services/data/payroll-data.services';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { PayrollTrendChartComponent } from './payroll-trend-chart.component';

/** Past payroll periods with a gross vs net trend (sample data). */
@Component({
  selector: 'app-payroll-history',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    StatCardComponent,
    DataTableComponent,
    EmptyStateComponent,
    PayrollTrendChartComponent,
    MoneyPipe,
  ],
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend],
    }),
  ],
  templateUrl: './payroll-history.component.html',
})
export class PayrollHistoryComponent {
  private readonly runsApi = inject(PayrollRunDataService);
  private readonly router = inject(Router);

  protected readonly year = signal<number | null>(null);
  protected readonly status = signal<string | null>(null);
  protected readonly statuses = PAYROLL_RUN_STATUSES.filter((s) => s !== 'Draft');

  protected readonly resource = rxResource({ stream: () => this.runsApi.getAll() });
  /** Processed periods only (drafts have no figures), newest first. */
  private readonly processed = computed(() =>
    (this.resource.value() ?? [])
      .filter((r) => r.status !== 'Draft')
      .sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly yearOptions = computed(() =>
    [...new Set(this.processed().map((r) => r.year))].sort((a, b) => b - a),
  );
  protected readonly rows = computed(() =>
    this.processed().filter(
      (r) =>
        (this.year() === null || r.year === this.year()) &&
        (this.status() === null || r.status === this.status()),
    ),
  );
  /** Chronological order for the chart. */
  protected readonly trend = computed(() => [...this.rows()].reverse());

  protected readonly totals = computed(() => {
    const rows = this.rows();
    const net = rows.reduce((sum, r) => sum + r.totalNet, 0);
    return {
      periods: rows.length,
      net,
      average: rows.length ? net / rows.length : 0,
      deductions: rows.reduce((sum, r) => sum + r.totalDeductions, 0),
    };
  });

  protected readonly columns: TableColumn<PayrollRun>[] = [
    { key: 'startDate', header: 'Period', value: (r) => r.periodName },
    { key: 'status', header: 'Status', type: 'status' },
    { key: 'payDate', header: 'Pay date', type: 'date', hideBelow: 'md', nowrap: true },
    { key: 'employeeCount', header: 'Employees', type: 'number', align: 'end', hideBelow: 'sm' },
    { key: 'totalGross', header: 'Gross', type: 'currency', align: 'end', hideBelow: 'sm' },
    {
      key: 'totalDeductions',
      header: 'Deductions',
      type: 'currency',
      align: 'end',
      hideBelow: 'lg',
    },
    { key: 'totalNet', header: 'Net', type: 'currency', align: 'end' },
  ];
  protected readonly actions: TableAction<PayrollRun>[] = [
    { id: 'review', label: 'Review period', icon: 'fact_check' },
  ];
  protected readonly rowLabel = (r: PayrollRun) => r.periodName;

  protected open(run: PayrollRun): void {
    this.router.navigate(['/payroll/processing', run.id]);
  }

  protected onAction(event: TableActionEvent<PayrollRun>): void {
    this.open(event.row);
  }
}
