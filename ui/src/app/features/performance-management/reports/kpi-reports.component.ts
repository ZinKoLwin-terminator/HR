import { DecimalPipe } from '@angular/common';
import {
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PageRequest } from '../../../core/models/common.models';
import { EmployeeLookupItem } from '../../../core/models/employee.models';
import {
  KpiEmployeeScorecard,
  KpiPeriodRef,
  KpiReportFilter,
} from '../../../core/models/kpi-insight.models';
import { SCORECARD_STATUSES } from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiInsightDataService,
} from '../../../core/services/data/kpi-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { PrintService } from '../../../core/services/print.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { FieldOption } from '../../../shared/components/crud/crud.models';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ExcelActionsComponent } from '../../../shared/components/excel/excel-actions.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PrintHeaderComponent } from '../../../shared/components/print/print-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';
import { fixed2, scoreText, withUnit } from '../dashboard/kpi-dashboard-views';
import { EmployeePickerComponent } from '../shared/employee-picker.component';
import { injectScorecardViewer } from '../shared/scorecard-viewer';
import {
  FILTER_LABELS,
  KPI_REPORTS,
  KPI_REPORT_CHOICES,
  KpiReportId,
  KpiReportRow,
  PERIOD_TYPE_OPTIONS,
  achievementRow,
  departmentRow,
  employeeRow,
  rankingRow,
  summaryRow,
  targetRow,
} from './kpi-report-definitions';
import { safeValue } from '../../../shared/utilities/resource-value';

/** One loaded page of a report, with the facts shown above its table. */
export interface KpiReportView {
  report: KpiReportId;
  rows: KpiReportRow[];
  total: number;
  pageIndex: number;
  pageSize: number;
  /** Totals of the employee and department reports (over every matching scorecard). */
  facts: { label: string; value: string }[];
  /** The period the department report is about (the server's default when none was chosen). */
  period: KpiPeriodRef | null;
  /** The employee report's scorecards, for their KPIs. */
  scorecards: KpiEmployeeScorecard[];
}

interface ReportParams {
  report: KpiReportId;
  request: PageRequest<KpiReportFilter>;
  subject: number | null;
}

/**
 * KPI reports (`/performance/reports`, `kpi:View`): a report selector, the filters that apply to the
 * chosen report, server-side paging and sorting (only the API's sort fields), an Excel export (the
 * export-only KPI modules, gated by `excel:Export` like every module) and a print layout for the
 * browser's "Save as PDF". Rows are scoped by the server: own, team, or everyone for HR. Every figure
 * is the server's.
 */
@Component({
  selector: 'app-kpi-reports',
  imports: [
    AppDatePipe,
    DataTableComponent,
    DecimalPipe,
    EmployeePickerComponent,
    EmptyStateComponent,
    ExcelActionsComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    PrintHeaderComponent,
    SearchFieldComponent,
  ],
  templateUrl: './kpi-reports.component.html',
  styleUrl: './kpi-reports.component.scss',
})
export class KpiReportsComponent {
  private readonly insights = inject(KpiInsightDataService);
  private readonly permissions = inject(PermissionService);
  private readonly printer = inject(PrintService);
  private readonly injector = inject(Injector);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly viewer = injectScorecardViewer();

  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly categoriesApi = inject(KpiCategoryDataService);
  private readonly lookupService = inject(OrganizationLookupService);

  protected readonly choices = KPI_REPORT_CHOICES;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;

  protected readonly choiceId = signal(KPI_REPORT_CHOICES[0].id);
  protected readonly choice = computed(
    () => KPI_REPORT_CHOICES.find((c) => c.id === this.choiceId()) ?? KPI_REPORT_CHOICES[0],
  );
  protected readonly definition = computed(() => KPI_REPORTS[this.choice().report]);

  protected readonly query = new ListQuery<KpiReportFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });

  /** The department of the department report. */
  protected readonly departmentId = signal<number | null>(null);
  /** The employee chosen in the picker: the subject of the employee report, a filter elsewhere. */
  protected readonly employee = signal<EmployeeLookupItem | null>(null);
  /** The picker needs the employee lookup (`employees:View`); without it the history is the user's own. */
  protected readonly canPickEmployee = computed(() => this.permissions.can('employees:View'));
  private readonly ownEmployeeId = computed(() => this.viewer().employeeId);

  /** The route id of the employee or department report; null while it is still to be chosen. */
  protected readonly subject = computed<number | null>(() => {
    switch (this.definition().report) {
      case 'employee':
        return this.canPickEmployee()
          ? (this.employee()?.id ?? this.ownEmployeeId())
          : this.ownEmployeeId();
      case 'department':
        return this.departmentId();
      default:
        return null;
    }
  });
  protected readonly needsSubject = computed(() => {
    const report = this.definition().report;
    return (report === 'employee' || report === 'department') && this.subject() === null;
  });

  /** The request of the chosen report: only its own filters, the search when it takes one. */
  protected readonly request = computed<PageRequest<KpiReportFilter>>(() => {
    const def = this.definition();
    const base = this.query.request();
    const filters: Partial<Record<keyof KpiReportFilter, unknown>> = {};
    for (const key of def.filters) {
      const value = (base.filters as Record<string, unknown> | undefined)?.[key];
      if (value !== null && value !== undefined && value !== '') filters[key] = value;
    }
    const picked = this.employee();
    if (def.employeeFilter && picked) filters.employeeId = picked.id;
    return {
      pageIndex: base.pageIndex,
      pageSize: base.pageSize,
      sort: def.sortKeys.includes(base.sort?.active ?? '') ? base.sort : undefined,
      search: def.search ? base.search : undefined,
      filters: filters as Partial<KpiReportFilter>,
    };
  });

  protected readonly report = rxResource({
    params: (): ReportParams | undefined =>
      this.needsSubject()
        ? undefined
        : { report: this.definition().report, request: this.request(), subject: this.subject() },
    stream: ({ params }) => this.load(params),
  });
  /** Keeps the last page of the same report while the next one loads, so the table does not blank. */
  protected readonly result = linkedSignal<KpiReportView | undefined, KpiReportView | undefined>({
    // value() throws while the resource is in error: read it only when there is one.
    source: () => safeValue(this.report),
    computation: (value, previous) =>
      value ?? (previous?.value?.report === this.definition().report ? previous.value : undefined),
  });
  protected readonly errorText = computed(() =>
    this.report.error() ? errorMessage(this.report.error()) : '',
  );

  // ---- Lookups for the filters (each one optional: a role that cannot read it gets no options) ----

  private readonly lookups = rxResource({
    stream: () => {
      const safe = <T>(source: Observable<T[]>) => source.pipe(catchError(() => of([] as T[])));
      return forkJoin({
        periods: safe(this.periodsApi.options()),
        categories: safe(this.categoriesApi.getAll()),
        // Organization names from the lookup: no organization:View needed, never fails.
        org: this.lookupService.organization(),
      }).pipe(
        map(({ periods, categories, org }) => ({
          periods,
          categories,
          companies: org.companies,
          branches: org.branches,
          departments: org.departments,
          positions: org.positions,
        })),
      );
    },
  });

  protected readonly departmentOptions = computed(() =>
    options(safeValue(this.lookups)?.departments ?? [], (d) => d.name),
  );

  /** The options of every filter; a lookup the role cannot read leaves its filter out. */
  private readonly filterOptions = computed<Record<keyof KpiReportFilter, FieldOption[]>>(() => {
    const l = safeValue(this.lookups);
    const years = [...new Set((l?.periods ?? []).map((p) => Number(p.startDate.slice(0, 4))))]
      .sort((a, b) => b - a)
      .map((y) => ({ value: y, label: String(y) }));
    return {
      companyId: options(l?.companies ?? [], (c) => c.name),
      branchId: options(l?.branches ?? [], (b) => b.name),
      departmentId: options(l?.departments ?? [], (d) => d.name),
      positionId: options(l?.positions ?? [], (p) => p.title),
      employeeId: [],
      categoryId: options(l?.categories ?? [], (c) => c.name),
      periodId: (l?.periods ?? []).map((p) => ({ value: p.id, label: `${p.name} (${p.status})` })),
      status: SCORECARD_STATUSES.map((s) => ({ value: s, label: s })),
      periodType: PERIOD_TYPE_OPTIONS,
      year: years.length
        ? years
        : [{ value: new Date().getFullYear(), label: String(new Date().getFullYear()) }],
    };
  });

  protected readonly filters = computed(() =>
    this.definition()
      .filters.map((key) => ({
        key,
        label: FILTER_LABELS[key],
        options: this.filterOptions()[key],
      }))
      .filter((f) => f.options.length > 0),
  );

  protected readonly activeFilterCount = computed(
    () =>
      Object.keys(this.request().filters ?? {}).filter((k) => k !== 'employeeId').length +
      (this.request().search ? 1 : 0) +
      (this.definition().employeeFilter && this.employee() ? 1 : 0),
  );

  /** "Department: Engineering" lines: what the rows follow (the export dialog and the print header). */
  protected readonly filterSummary = computed(() => {
    const parts: string[] = [];
    const def = this.definition();
    const view = this.result();
    if (def.report === 'department') {
      const name = this.departmentOptions().find((o) => o.value === this.departmentId())?.label;
      if (name) parts.push(`Department: ${name}`);
      if (view?.period) parts.push(`Period: ${view.period.name}`);
    }
    if (def.report === 'employee' && this.subject() !== null) {
      const picked = this.employee();
      parts.push(
        picked ? `Employee: ${picked.employeeCode} ${picked.displayName}` : 'Employee: you',
      );
    }
    for (const [key, value] of Object.entries(this.request().filters ?? {})) {
      const typed = key as keyof KpiReportFilter;
      if (typed === 'employeeId') {
        const picked = this.employee();
        if (picked) parts.push(`Employee: ${picked.employeeCode}`);
        continue;
      }
      const label = this.filterOptions()[typed]?.find((o) => o.value === value)?.label;
      parts.push(`${FILTER_LABELS[typed]}: ${label ?? String(value)}`);
    }
    return parts;
  });

  /** The export's filters: the report's, with the subject of the department or employee report. */
  protected readonly exportFilters = computed<Record<string, unknown>>(() => {
    const filters: Record<string, unknown> = { ...this.request().filters };
    const report = this.definition().report;
    if (report === 'department') {
      filters['departmentId'] = this.departmentId();
      const period = this.result()?.period;
      if (period) filters['periodId'] = period.id;
    }
    if (report === 'employee') filters['employeeId'] = this.subject();
    return filters;
  });

  // ---- Printing -------------------------------------------------------------------------------

  /** True while the print layout is shown (between Print and the end of the browser dialog). */
  protected readonly printing = signal(false);

  constructor() {
    this.printer.afterPrint$.pipe(takeUntilDestroyed()).subscribe(() => this.printing.set(false));
  }

  /** Lays out the loaded page for paper and opens the browser's print dialog (Save as PDF there). */
  protected print(): void {
    this.printing.set(true);
    afterNextRender(() => this.printer.print(), { injector: this.injector });
  }

  // ---- Interactions ---------------------------------------------------------------------------

  protected selectReport(id: string): void {
    this.choiceId.set(id);
    this.query.reset();
    const preset = this.choice().periodType;
    if (preset) this.query.setFilter('periodType', preset);
  }

  protected filterValue(key: keyof KpiReportFilter): unknown {
    return (this.query.filters() as Record<string, unknown>)[key] ?? null;
  }

  protected setFilter(key: keyof KpiReportFilter, value: unknown): void {
    this.query.setFilter(key, value as never);
  }

  protected clearFilters(): void {
    const preset = this.choice().periodType;
    this.query.reset();
    if (preset) this.query.setFilter('periodType', preset);
    if (this.definition().employeeFilter) this.employee.set(null);
  }

  protected chooseDepartment(id: number | null): void {
    this.departmentId.set(id);
    this.query.pageIndex.set(0);
  }

  protected chooseEmployee(employee: EmployeeLookupItem | null): void {
    this.employee.set(employee);
    this.query.pageIndex.set(0);
  }

  // ---- Cells (screen and print) ---------------------------------------------------------------

  protected readonly score = scoreText;
  protected readonly fixed = fixed2;
  protected readonly unit = withUnit;
  protected readonly rowLabel = (row: KpiReportRow): string =>
    [row['employeeCode'], row['periodName'], row['kpiCode']].filter(Boolean).join(' ') ||
    `Row ${row.id}`;

  protected cellValue(column: TableColumn<KpiReportRow>, row: KpiReportRow): unknown {
    return column.value ? column.value(row) : row[column.key];
  }

  // ---- Loading --------------------------------------------------------------------------------

  private load(params: ReportParams): Observable<KpiReportView> {
    const { request, subject } = params;
    const view = (
      rows: KpiReportRow[],
      page: { total: number; pageIndex: number; pageSize: number },
      extra: Partial<KpiReportView> = {},
    ): KpiReportView => ({
      report: params.report,
      rows,
      total: page.total,
      pageIndex: page.pageIndex,
      pageSize: page.pageSize,
      facts: [],
      period: null,
      scorecards: [],
      ...extra,
    });
    switch (params.report) {
      case 'summary':
        return this.insights
          .summaryReport(request)
          .pipe(map((page) => view(page.items.map(summaryRow), page)));
      case 'employee':
        return this.insights.employeeReport(subject ?? 0, request).pipe(
          map((r) =>
            view(r.scorecards.items.map(employeeRow), r.scorecards, {
              facts: [
                { label: 'Employee', value: `${r.employeeCode} · ${r.employeeName}` },
                { label: 'Finalized scorecards', value: String(r.finalized) },
                { label: 'Average score', value: scoreText(r.averageScore) },
              ],
              scorecards: r.scorecards.items,
            }),
          ),
        );
      case 'department':
        return this.insights.departmentReport(subject ?? 0, request).pipe(
          map((r) =>
            view(r.rows.items.map(departmentRow), r.rows, {
              period: r.period,
              facts: [
                { label: 'Department', value: r.departmentName },
                { label: 'Period', value: r.period?.name ?? '—' },
                { label: 'Scorecards', value: String(r.scorecards) },
                { label: 'Finalized', value: String(r.finalized) },
                { label: 'Average score', value: scoreText(r.averageScore) },
              ],
            }),
          ),
        );
      case 'ranking':
        return this.insights
          .rankingReport(request)
          .pipe(map((page) => view(page.items.map(rankingRow), page)));
      case 'kpiAchievement':
        return this.insights
          .kpiAchievementReport(request)
          .pipe(map((page) => view(page.items.map(achievementRow), page)));
      case 'targetVsActual':
        return this.insights
          .targetVsActualReport(request)
          .pipe(map((page) => view(page.items.map(targetRow), page)));
    }
  }
}
