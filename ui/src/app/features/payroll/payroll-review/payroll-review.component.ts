import { DatePipe } from '@angular/common';
import {
  Component,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, expand, filter, map, reduce } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import {
  PAYROLL_RUN_STATUSES,
  PayrollRun,
  Payslip,
  PayslipFilter,
} from '../../../core/models/payroll.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { PermissionService } from '../../../core/services/permission.service';
import { PrintService } from '../../../core/services/print.service';
import {
  PayrollRunDataService,
  PayslipDataService,
} from '../../../core/services/data/payroll-data.services';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { formatDuration } from '../../../shared/utilities/date.util';
import { ListQuery } from '../../../shared/utilities/list-query';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';
import { PayslipSheetComponent } from '../payslips/payslip-sheet.component';

type Row = Payslip;

/** Review a payroll period: totals, warnings, department summary and payslips, plus workflow actions. */
@Component({
  selector: 'app-payroll-review',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    StatCardComponent,
    StatusBadgeComponent,
    SearchFieldComponent,
    DataTableComponent,
    EmptyStateComponent,
    MoneyPipe,
    PayslipSheetComponent,
  ],
  templateUrl: './payroll-review.component.html',
  styleUrl: './payroll-review.component.scss',
})
export class PayrollReviewComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly runsApi = inject(PayrollRunDataService);
  private readonly payslipsApi = inject(PayslipDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  private readonly router = inject(Router);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly permissions = inject(PermissionService);
  private readonly printer = inject(PrintService);
  private readonly injector = inject(Injector);
  /** UX only (the server enforces): process needs payroll:Edit, the other actions payroll:Approve. */
  protected readonly canProcess = computed(() => this.permissions.can('payroll:Edit'));
  protected readonly canApprove = computed(() => this.permissions.can('payroll:Approve'));

  protected readonly runId = computed(() => Number(this.id()));
  protected readonly steps: readonly string[] = PAYROLL_RUN_STATUSES;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;

  protected readonly runs = rxResource({ stream: () => this.runsApi.getAll() });
  protected readonly run = computed(
    () => this.runs.value()?.find((r) => r.id === this.runId()) ?? null,
  );
  protected readonly notFound = computed(() => !!this.runs.value() && !this.run());
  protected readonly previous = computed(() => {
    const run = this.run();
    return run
      ? (this.runs.value() ?? [])
          .filter((r) => r.startDate < run.startDate && r.status !== 'Draft')
          .sort((a, b) => b.startDate.localeCompare(a.startDate))[0]
      : undefined;
  });
  protected readonly netChange = computed(() => {
    const run = this.run();
    const prev = this.previous();
    return run && prev?.totalNet
      ? Math.round(((run.totalNet - prev.totalNet) / prev.totalNet) * 1000) / 10
      : null;
  });
  protected readonly stepIndex = computed(() => this.steps.indexOf(this.run()?.status ?? 'Draft'));
  protected readonly hasPayslips = computed(() => (this.run()?.status ?? 'Draft') !== 'Draft');

  private readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.departments ?? [], (d) => d.name),
  );

  protected readonly query = new ListQuery<PayslipFilter>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'employeeCode', direction: 'asc' },
  });

  protected readonly page = rxResource({
    params: () => ({ run: this.runId(), request: this.query.request(), has: this.hasPayslips() }),
    stream: ({ params }) =>
      this.payslipsApi.query({
        ...params.request,
        filters: { ...params.request.filters, runId: params.run },
      }),
  });
  protected readonly result = linkedSignal<
    PagedResult<Payslip> | undefined,
    PagedResult<Payslip> | undefined
  >({
    source: this.page.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  /** All payslips of the period (for the department summary), independent of table filters. */
  private readonly allPayslips = rxResource({
    params: () => ({ run: this.runId(), has: this.hasPayslips() }),
    stream: ({ params }) => this.allPayslipsOf(params.run),
  });
  /** Every payslip of a run, read in pages (the API serves at most 200 per page). */
  private allPayslipsOf(runId: number): Observable<PagedResult<Payslip>> {
    const pageSize = 200;
    const read = (pageIndex: number) =>
      this.payslipsApi.query({ pageIndex, pageSize, filters: { runId } });
    return read(0).pipe(
      expand((page) =>
        (page.pageIndex + 1) * page.pageSize < page.total ? read(page.pageIndex + 1) : EMPTY,
      ),
      reduce((all, page) => ({ ...page, items: [...all.items, ...page.items] }), {
        items: [] as Payslip[],
        total: 0,
        pageIndex: 0,
        pageSize,
      } as PagedResult<Payslip>),
      map((all) => ({ ...all, pageIndex: 0, pageSize: all.items.length })),
    );
  }
  /** Every payslip of the period, in employee-code order, for printing. */
  protected readonly allPayslipList = computed(() =>
    [...(this.allPayslips.value()?.items ?? [])].sort((a, b) =>
      a.employeeCode.localeCompare(b.employeeCode),
    ),
  );
  /** True while the whole run is laid out for printing (one payslip per page). */
  protected readonly printingAll = signal(false);
  protected readonly departments = computed(() => {
    const map = new Map<string, { name: string; count: number; gross: number; net: number }>();
    for (const p of this.allPayslips.value()?.items ?? []) {
      const row = map.get(p.departmentName) ?? {
        name: p.departmentName,
        count: 0,
        gross: 0,
        net: 0,
      };
      row.count++;
      row.gross += p.grossSalary;
      row.net += p.netSalary;
      map.set(p.departmentName, row);
    }
    return [...map.values()].sort((a, b) => b.net - a.net);
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly columns: TableColumn<Row>[] = [
    { key: 'employeeCode', header: 'Code', sortable: true, width: '100px' },
    { key: 'employeeName', header: 'Employee', sortable: true },
    { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'lg' },
    {
      key: 'basicSalary',
      header: 'Basic',
      type: 'currency',
      align: 'end',
      sortable: true,
      hideBelow: 'sm',
    },
    {
      key: 'earnings',
      header: 'Allowances',
      type: 'currency',
      align: 'end',
      hideBelow: 'md',
      value: (p) => Math.round((p.grossSalary - p.basicSalary) * 100) / 100,
    },
    {
      key: 'totalDeductions',
      header: 'Deductions',
      type: 'currency',
      align: 'end',
      sortable: true,
      hideBelow: 'md',
    },
    { key: 'netSalary', header: 'Net', type: 'currency', align: 'end', sortable: true },
    { key: 'notes', header: 'Notes', hideBelow: 'lg', value: (p) => this.notes(p) },
  ];
  protected readonly rowLabel = (p: Row) => p.employeeName;

  constructor() {
    this.printer.afterPrint$
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.printingAll.set(false));
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.query.pageIndex()) {
        this.query.pageIndex.set(value.pageIndex);
      }
    });
  }

  protected notes(p: Payslip): string {
    const notes: string[] = [];
    if (p.daysEmployed < p.periodDays)
      notes.push(`Part month (${p.daysEmployed}/${p.periodDays} days)`);
    if (p.overtimeMinutes) notes.push(`Overtime ${formatDuration(p.overtimeMinutes)}`);
    if (p.unpaidLeaveDays) notes.push(`${p.unpaidLeaveDays} unpaid leave day(s)`);
    return notes.join(' · ') || '—';
  }

  /** Lays out every payslip of the period, one per page, then opens the print dialog. */
  protected printAll(): void {
    this.printingAll.set(true);
    afterNextRender(() => this.printer.print(), { injector: this.injector });
  }

  protected setDepartment(value: number | null): void {
    this.query.setFilter('departmentId', value);
  }

  protected openPayslip(p: Payslip): void {
    this.router.navigate(['/payroll/payslips', p.id]);
  }

  private act(request: Observable<PayrollRun>, message: string): void {
    request.subscribe({
      next: () => {
        this.toast.success(message);
        this.runs.reload();
        this.page.reload();
        this.allPayslips.reload();
      },
      error: (error: unknown) => this.toast.error(error),
    });
  }

  protected process(run: PayrollRun): void {
    this.confirm
      .confirm({
        title: 'Process payroll?',
        message: `Generate sample payslips for everyone employed in ${run.periodName}?`,
        confirmText: 'Process',
      })
      .pipe(filter(Boolean))
      .subscribe(() => this.act(this.runsApi.process(run.id), `${run.periodName} processed.`));
  }

  protected approve(run: PayrollRun): void {
    this.confirm
      .confirm({
        title: 'Approve payroll?',
        message: `Approve ${run.periodName} for ${run.employeeCount} employees (sample figures)?`,
        confirmText: 'Approve',
      })
      .pipe(filter(Boolean))
      .subscribe(() => this.act(this.runsApi.approve(run.id), `${run.periodName} approved.`));
  }

  protected markPaid(run: PayrollRun): void {
    this.confirm
      .confirm({
        title: 'Mark as paid?',
        message: `This only records ${run.periodName} as paid in the demo. No payment is made and no bank is contacted.`,
        confirmText: 'Mark as paid',
      })
      .pipe(filter(Boolean))
      .subscribe(() =>
        this.act(this.runsApi.markPaid(run.id), `${run.periodName} marked as paid.`),
      );
  }

  protected returnToDraft(run: PayrollRun): void {
    this.confirm
      .prompt({
        title: 'Return to draft?',
        message: `The payslips for ${run.periodName} will be removed and the period reopened.`,
        label: 'Reason (optional)',
        confirmText: 'Return to draft',
        destructive: true,
      })
      .pipe(filter((text): text is string => text !== null))
      .subscribe((reason) =>
        this.act(
          this.runsApi.returnToDraft(run.id, reason),
          `${run.periodName} returned to draft.`,
        ),
      );
  }
}
