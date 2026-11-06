import {
  Component,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import { Payslip, PayslipFilter } from '../../../core/models/payroll.models';
import {
  PayrollRunDataService,
  PayslipDataService,
} from '../../../core/services/data/payroll-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { ListQuery } from '../../../shared/utilities/list-query';
import {
  OrganizationLookupService,
  employeeOptions,
  options,
} from '../../organization/organization-lookups';

/** Payslips across periods, filterable by period, employee and department. */
@Component({
  selector: 'app-payslips-list',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    SearchFieldComponent,
    DataTableComponent,
    EmptyStateComponent,
  ],
  templateUrl: './payslips-list.component.html',
  styleUrl: './payslips-list.component.scss',
})
export class PayslipsListComponent {
  private readonly payslipsApi = inject(PayslipDataService);
  private readonly runsApi = inject(PayrollRunDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly router = inject(Router);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly query = new ListQuery<PayslipFilter>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'employeeCode', direction: 'asc' },
  });

  private readonly lookups = rxResource({
    stream: () => forkJoin({ org: this.lookupService.load(), runs: this.runsApi.getAll() }),
  });
  /** Periods that have payslips, newest first. */
  protected readonly periodOptions = computed(() =>
    (this.lookups.value()?.runs ?? [])
      .filter((r) => r.status !== 'Draft')
      .sort((a, b) => b.startDate.localeCompare(a.startDate))
      .map((r) => ({ value: r.id, label: r.periodName })),
  );
  protected readonly employeeOptions = computed(() =>
    employeeOptions(this.lookups.value()?.org.employees ?? []),
  );
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.org.departments ?? [], (d) => d.name),
  );

  /** Whether the secondary filter panel (period, employee, department) is expanded. */
  protected readonly panelOpen = signal(false);

  /** Removable chips describing every applied filter. */
  protected readonly activeChips = computed(() => {
    const f = this.query.filters();
    const labelOf = (opts: { value: unknown; label: string }[], v: unknown) =>
      opts.find((o) => o.value === v)?.label ?? String(v);
    const chips: { key: keyof PayslipFilter | 'search'; name: string; label: string }[] = [];
    const term = this.query.search();
    if (term) chips.push({ key: 'search', name: 'Search', label: term });
    if (f.runId)
      chips.push({
        key: 'runId',
        name: 'Period',
        label: labelOf(this.periodOptions(), f.runId),
      });
    if (f.employeeId)
      chips.push({
        key: 'employeeId',
        name: 'Employee',
        label: labelOf(this.employeeOptions(), f.employeeId),
      });
    if (f.departmentId)
      chips.push({
        key: 'departmentId',
        name: 'Department',
        label: labelOf(this.departmentOptions(), f.departmentId),
      });
    return chips;
  });
  /** Filters that live inside the collapsible panel. */
  protected readonly extraFilterCount = computed(
    () =>
      this.activeChips().filter((c) => ['runId', 'employeeId', 'departmentId'].includes(c.key))
        .length,
  );

  protected readonly defaulted = signal(false);

  protected readonly page = rxResource({
    // Wait until the default period has been chosen so the first load is already filtered.
    params: () => (this.defaulted() ? this.query.request() : undefined),
    stream: ({ params }) => this.payslipsApi.query(params),
  });
  protected readonly result = linkedSignal<
    PagedResult<Payslip> | undefined,
    PagedResult<Payslip> | undefined
  >({
    source: this.page.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly columns: TableColumn<Payslip>[] = [
    { key: 'periodName', header: 'Period', hideBelow: 'sm', nowrap: true },
    { key: 'employeeCode', header: 'Code', sortable: true, width: '100px' },
    { key: 'employeeName', header: 'Employee', sortable: true },
    { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'lg' },
    {
      key: 'grossSalary',
      header: 'Gross',
      type: 'currency',
      align: 'end',
      sortable: true,
      hideBelow: 'md',
    },
    {
      key: 'totalDeductions',
      header: 'Deductions',
      type: 'currency',
      align: 'end',
      hideBelow: 'md',
    },
    { key: 'netSalary', header: 'Net', type: 'currency', align: 'end', sortable: true },
  ];
  protected readonly actions: TableAction<Payslip>[] = [
    { id: 'view', label: 'View payslip', icon: 'receipt_long' },
  ];
  protected readonly rowLabel = (p: Payslip) => `${p.employeeName}, ${p.periodName}`;

  constructor() {
    // Start on the most recent processed period.
    effect(() => {
      if (this.lookups.value() && !untracked(this.defaulted)) {
        const periods = this.periodOptions();
        if (periods.length) {
          this.query.setFilter('runId', periods[0].value);
        }
        this.defaulted.set(true);
      }
    });
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.query.pageIndex()) {
        this.query.pageIndex.set(value.pageIndex);
      }
    });
  }

  protected value<K extends keyof PayslipFilter>(key: K): PayslipFilter[K] | null {
    return (this.query.filters()[key] ?? null) as PayslipFilter[K] | null;
  }

  protected setFilter<K extends keyof PayslipFilter>(key: K, value: PayslipFilter[K] | null): void {
    this.query.setFilter(key, value);
  }

  protected removeChip(key: keyof PayslipFilter | 'search'): void {
    if (key === 'search') this.query.setSearch('');
    else this.setFilter(key, null);
  }

  protected open(row: Payslip): void {
    this.router.navigate(['/payroll/payslips', row.id]);
  }

  protected onAction(event: TableActionEvent<Payslip>): void {
    this.open(event.row);
  }
}
