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
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { Entity, PagedResult, SortState } from '../../../core/models/common.models';
import {
  GRIEVANCE_REPORT_FILTER_KEYS,
  GrievanceReportFilter,
  GrievanceReportRow,
} from '../../../core/models/grievance-insight.models';
import {
  GRIEVANCE_PRIORITIES,
  GRIEVANCE_STATUSES,
  GRIEVANCE_STATUS_LABELS,
} from '../../../core/models/grievance.models';
import {
  GrievanceCaseDataService,
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../../core/services/data/grievance-data.services';
import { ADMIN_ROLE_NAME } from '../../../core/models/settings.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { PrintService } from '../../../core/services/print.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../../shared/components/error-banner/error-banner.component';
import { ExcelActionsComponent } from '../../../shared/components/excel/excel-actions.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PrintHeaderComponent } from '../../../shared/components/print/print-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { GrievancePriorityComponent } from '../shared/grievance-priority.component';
import { grievanceStatusLabel } from '../shared/grievance-status-badge.component';
import { daysText, percentText } from './grievance-insight-bars';
import { safeValue } from '../../../shared/utilities/resource-value';

/** The Excel module of this report (export only; the API's `grievance-cases`). */
export const GRIEVANCE_CASES_EXPORT_MODULE = 'grievance-cases';

/** A report row as the table needs it (keyed by the case id). */
export type GrievanceReportTableRow = GrievanceReportRow & Entity;

/** The columns: the export's, never the subject, description, notes or submitter. */
export const GRIEVANCE_REPORT_COLUMNS: TableColumn<GrievanceReportTableRow>[] = [
  { key: 'caseNo', header: 'Case', sortable: true, nowrap: true },
  { key: 'categoryName', header: 'Category', sortable: true },
  {
    key: 'status',
    header: 'Status',
    type: 'status',
    sortable: true,
    value: (r) => grievanceStatusLabel(r.status),
  },
  { key: 'priority', header: 'Priority', sortable: true },
  {
    key: 'departmentName',
    header: 'Department',
    sortable: true,
    value: (r) => r.departmentName ?? '—',
  },
  { key: 'submittedOn', header: 'Submitted', type: 'date', sortable: true, nowrap: true },
  {
    key: 'targetResolutionDate',
    header: 'Target',
    type: 'date',
    sortable: true,
    nowrap: true,
    hideBelow: 'md',
  },
  {
    key: 'resolvedOn',
    header: 'Resolved',
    type: 'date',
    sortable: true,
    nowrap: true,
    hideBelow: 'md',
  },
  {
    key: 'resolutionDays',
    header: 'Days to resolve',
    type: 'number',
    align: 'end',
    sortable: true,
  },
  {
    key: 'isOverdue',
    header: 'Overdue',
    value: (r) => (r.isOverdue ? 'Yes' : 'No'),
    hideBelow: 'sm',
  },
  {
    key: 'leadInvestigatorName',
    header: 'Lead',
    value: (r) => r.leadInvestigatorName ?? '—',
    hideBelow: 'lg',
  },
  {
    key: 'isEscalated',
    header: 'Escalated',
    value: (r) => (r.isEscalated ? 'Yes' : 'No'),
    hideBelow: 'lg',
  },
];

/**
 * The grievance case report (`grievance-management:View`): the API's filters (submission dates,
 * department, category, status, priority, investigator, days to resolve, overdue), a paged and
 * sortable table, the summary of the same period and category, an Excel export (the export-only
 * `grievance-cases` module) and a print layout (Save as PDF from the browser). Case numbers only:
 * never the subject, the description, notes or the submitter.
 */
@Component({
  selector: 'app-grievance-reports',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    ErrorBannerComponent,
    ExcelActionsComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    GrievancePriorityComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    PrintHeaderComponent,
    RouterLink,
    StatCardComponent,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header
      title="Grievance reports"
      description="Cases by the filters you choose, with the summary, Excel export and print. Case numbers only: subjects, descriptions and submitters are never listed."
      [class.no-print]="printing()"
    >
      <ng-container pageSearch>
        <app-filter-popup [activeCount]="activeFilterCount()" (cleared)="clear()">
          <app-filter-field label="Submitted from">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                aria-label="Submitted from"
                [matDatepicker]="fromPicker"
                [value]="filters().submittedFrom ?? null"
                (dateChange)="set('submittedFrom', $event.value)"
              />
              <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
              <mat-datepicker #fromPicker />
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Submitted to">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                aria-label="Submitted to"
                [matDatepicker]="toPicker"
                [value]="filters().submittedTo ?? null"
                (dateChange)="set('submittedTo', $event.value)"
              />
              <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
              <mat-datepicker #toPicker />
            </mat-form-field>
          </app-filter-field>
          @for (f of selects(); track f.key) {
            <app-filter-field [label]="f.label">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  placeholder="All"
                  [attr.aria-label]="f.label"
                  [value]="filters()[f.key] ?? null"
                  (selectionChange)="set(f.key, $event.value)"
                >
                  <mat-option [value]="null">All</mat-option>
                  @for (o of f.options; track o.value) {
                    <mat-option [value]="o.value">{{ o.label }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            </app-filter-field>
          }
          <app-filter-field label="Days to resolve, at least">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                type="number"
                min="0"
                aria-label="Days to resolve, at least"
                [value]="filters().minResolutionDays ?? ''"
                (change)="setNumber('minResolutionDays', $event)"
              />
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Days to resolve, at most">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                type="number"
                min="0"
                aria-label="Days to resolve, at most"
                [value]="filters().maxResolutionDays ?? ''"
                (change)="setNumber('maxResolutionDays', $event)"
              />
            </mat-form-field>
          </app-filter-field>
        </app-filter-popup>
      </ng-container>
      <ng-container pageTools>
        @if (isAdmin()) {
          <a mat-stroked-button routerLink="/grievances/access-log">
            <mat-icon>history</mat-icon>Access log
          </a>
        }
        <button
          mat-stroked-button
          type="button"
          aria-label="Print the grievance report"
          [disabled]="!result()"
          (click)="print()"
        >
          <mat-icon>print</mat-icon>Print
        </button>
        <app-excel-actions
          [moduleCode]="exportModule"
          [allowImport]="false"
          [filters]="filters()"
          [filterSummary]="filterSummary()"
        />
      </ng-container>
    </app-page-header>

    @if (summary.hasValue() ? summary.value() : undefined; as s) {
      <section class="stats" aria-label="Summary" [class.no-print]="printing()">
        <app-stat-card label="Cases" [value]="s.total" icon="gavel" />
        <app-stat-card label="Open" [value]="s.open" icon="pending_actions" />
        <app-stat-card
          label="Resolved or closed"
          [value]="s.resolved + s.closed"
          icon="task_alt"
          tone="success"
        />
        <app-stat-card
          label="Overdue"
          [value]="s.overdue"
          icon="schedule"
          [tone]="s.overdue > 0 ? 'danger' : 'neutral'"
        />
        <app-stat-card
          label="Resolution rate"
          [value]="percent(s.resolutionRatePct)"
          icon="donut_large"
          tone="success"
        />
        <app-stat-card
          label="Average resolution"
          [value]="days(s.averageResolutionDays)"
          icon="timer"
        />
      </section>
      <p class="muted" [class.no-print]="printing()">
        The summary follows the date, category and priority filters; the table follows all of them.
      </p>
    }

    @if (page.error() && !result()) {
      <app-empty-state icon="error" title="The report could not be loaded" [message]="errorText()">
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      @if (page.error()) {
        <app-error-banner [message]="errorText()" (retry)="page.reload()" />
      }
      <app-data-table
        [class.no-print]="printing()"
        caption="Grievance case report"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [sort]="query.sort()"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        emptyIcon="query_stats"
        emptyTitle="No case matches"
        emptyMessage="Adjust the filters."
        (pageChange)="query.onPage($event)"
        (sortChange)="sortBy($event)"
      >
        <ng-template appCellTemplate="caseNo" let-row>
          <a
            class="row-link"
            [routerLink]="['/grievances/cases', row.caseId]"
            [attr.aria-label]="'Open case ' + row.caseNo"
            >{{ row.caseNo }}</a
          >
        </ng-template>
        <ng-template appCellTemplate="priority" let-row>
          <app-grievance-priority [priority]="row.priority" />
        </ng-template>
      </app-data-table>
    }

    @if (printing() && result(); as view) {
      <div class="print-only" data-testid="grievance-report-print">
        <app-print-header title="Grievance case report" [subtitle]="filterSummary().join(' · ')" />
        @if (summary.hasValue() ? summary.value() : undefined; as s) {
          <p class="print-note">
            Cases: {{ s.total }} · Open: {{ s.open }} · Overdue: {{ s.overdue }} · Resolution rate:
            {{ percent(s.resolutionRatePct) }} · Average resolution:
            {{ days(s.averageResolutionDays) }}
          </p>
        }
        <table class="print-table">
          <caption class="cdk-visually-hidden">
            Grievance case report
          </caption>
          <thead>
            <tr>
              @for (c of columns; track c.key) {
                <th scope="col">{{ c.header }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of view.items; track row.id) {
              <tr>
                @for (c of columns; track c.key) {
                  <td>
                    @let value = cell(c, row);
                    @if (c.type === 'date') {
                      {{ value ? ($any(value) | appDate) : '—' }}
                    } @else {
                      {{ value ?? '—' }}
                    }
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 12px;
    }
    .muted {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .row-link {
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .row-link:hover,
    .row-link:focus-visible {
      text-decoration: underline;
    }
  `,
})
export class GrievanceReportsComponent {
  private readonly insights = inject(GrievanceInsightDataService);
  private readonly cases = inject(GrievanceCaseDataService);
  private readonly mine = inject(MyGrievanceDataService);
  private readonly printer = inject(PrintService);
  private readonly injector = inject(Injector);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly user = inject(CurrentUserService);
  /** HR Administrators also get the module-wide access log. */
  protected readonly isAdmin = computed(() => this.user.current().roles.includes(ADMIN_ROLE_NAME));

  protected readonly exportModule = GRIEVANCE_CASES_EXPORT_MODULE;
  protected readonly columns = GRIEVANCE_REPORT_COLUMNS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly percent = percentText;
  protected readonly days = daysText;
  protected readonly rowLabel = (row: GrievanceReportTableRow) => `Case ${row.caseNo}`;

  protected readonly query = new ListQuery<GrievanceReportFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });
  protected readonly filters = this.query.filters;
  protected readonly activeFilterCount = this.query.activeFilterCount;

  private readonly options = rxResource({
    stream: () => this.mine.options().pipe(catchError(() => of(null))),
  });
  private readonly investigators = rxResource({
    stream: () => this.cases.investigators().pipe(catchError(() => of([]))),
  });

  /** The select filters, with their choices. */
  protected readonly selects = computed(() => {
    const o = safeValue(this.options);
    const yesNo = [
      { value: true, label: 'Yes' },
      { value: false, label: 'No' },
    ];
    return [
      {
        key: 'departmentId' as const,
        label: 'Department',
        options: (o?.departments ?? []).map((d) => ({ value: d.id as unknown, label: d.name })),
      },
      {
        key: 'categoryId' as const,
        label: 'Category',
        options: (o?.categories ?? []).map((c) => ({ value: c.id as unknown, label: c.name })),
      },
      {
        key: 'status' as const,
        label: 'Status',
        options: GRIEVANCE_STATUSES.filter((s) => s !== 'Draft').map((s) => ({
          value: s as unknown,
          label: GRIEVANCE_STATUS_LABELS[s],
        })),
      },
      {
        key: 'priority' as const,
        label: 'Priority',
        options: GRIEVANCE_PRIORITIES.map((p) => ({ value: p as unknown, label: p })),
      },
      {
        key: 'investigatorEmployeeId' as const,
        label: 'Investigator',
        options: (safeValue(this.investigators) ?? []).map((i) => ({
          value: i.employeeId as unknown,
          label: i.displayName,
        })),
      },
      {
        key: 'overdue' as const,
        label: 'Overdue',
        options: yesNo as { value: unknown; label: string }[],
      },
    ];
  });

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) =>
      this.insights.casesReport(params).pipe(
        map((page): PagedResult<GrievanceReportTableRow> => ({
          ...page,
          items: page.items.map((r) => ({ ...r, id: r.caseId })),
        })),
      ),
  });
  protected readonly result = linkedSignal<
    PagedResult<GrievanceReportTableRow> | undefined,
    PagedResult<GrievanceReportTableRow> | undefined
  >({
    source: () => safeValue(this.page),
    computation: (value, previous) => value ?? previous?.value,
  });
  /** The summary of the same dates, category and priority (the summary has no other filters). */
  protected readonly summary = rxResource({
    params: () => {
      const f = this.filters();
      return {
        submittedFrom: f.submittedFrom,
        submittedTo: f.submittedTo,
        categoryId: f.categoryId,
        priority: f.priority,
      };
    },
    stream: ({ params }) => this.insights.summary(params),
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  /** "Status: Under Review" lines: what the rows follow (the export dialog and the print header). */
  protected readonly filterSummary = computed(() => {
    const f = this.filters();
    const lines: string[] = [];
    for (const key of GRIEVANCE_REPORT_FILTER_KEYS) {
      const value = f[key];
      if (value === null || value === undefined || value === '') continue;
      const select = this.selects().find((s) => s.key === key);
      const label = select?.options.find((o) => o.value === value)?.label ?? String(value);
      lines.push(`${FILTER_LABELS[key]}: ${label}`);
    }
    return lines;
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

  // ---- Filters --------------------------------------------------------------------------------

  protected set<K extends keyof GrievanceReportFilter>(key: K, value: unknown): void {
    this.query.setFilter(key, (value === '' ? null : value) as GrievanceReportFilter[K] | null);
  }

  protected setNumber(key: 'minResolutionDays' | 'maxResolutionDays', event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const value = raw === '' ? null : Math.max(0, Math.floor(Number(raw)));
    this.query.setFilter(key, Number.isFinite(value) ? value : null);
  }

  protected clear(): void {
    this.query.reset();
  }

  protected sortBy(sort: SortState): void {
    this.query.setSort(sort);
  }

  protected cell(
    column: TableColumn<GrievanceReportTableRow>,
    row: GrievanceReportTableRow,
  ): unknown {
    return column.value
      ? column.value(row)
      : (row as unknown as Record<string, unknown>)[column.key];
  }
}

const FILTER_LABELS: Record<keyof GrievanceReportFilter, string> = {
  submittedFrom: 'Submitted from',
  submittedTo: 'Submitted to',
  departmentId: 'Department',
  categoryId: 'Category',
  status: 'Status',
  priority: 'Priority',
  investigatorEmployeeId: 'Investigator',
  minResolutionDays: 'Days to resolve, at least',
  maxResolutionDays: 'Days to resolve, at most',
  overdue: 'Overdue',
};
