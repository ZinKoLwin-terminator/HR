import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { filter, forkJoin } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../../core/models/common.models';
import {
  AssignKpiResult,
  BulkAssignmentResult,
  SCORECARD_STATUSES,
  Scorecard,
  ScorecardFilter,
} from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  ScorecardDataService,
} from '../../../core/services/data/kpi-data.services';
import { DepartmentDataService } from '../../../core/services/data/organization-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { KPI_WEIGHT_TOTAL } from '../../../core/utilities/kpi-calculation';
import { FilterConfig } from '../../../shared/components/crud/crud.models';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { ListQuery } from '../../../shared/utilities/list-query';
import { options } from '../../organization/organization-lookups';
import { assignmentMessage } from '../shared/assignment-rows';
import { textOptions } from '../shared/kpi-options';
import { AssignKpisDialogComponent } from './assign-kpis-dialog.component';
import { BulkAssignDialogComponent } from './bulk-assign-dialog.component';

/** Whether a plan's weights are not exactly 100% (highlighted in the list). */
export const weightIsOff = (row: Pick<Scorecard, 'totalWeight'>): boolean =>
  row.totalWeight !== KPI_WEIGHT_TOTAL;

/** Overall score with its rating label, or null (shown as a dash) before there is one. */
export function scoreText(row: Pick<Scorecard, 'overallScore' | 'ratingLabel'>): string | null {
  if (row.overallScore === null) {
    return null;
  }
  const score = row.overallScore.toFixed(2);
  return row.ratingLabel ? `${score} · ${row.ratingLabel}` : score;
}

/**
 * The columns of the assignments list (exported so a test can check them). Only the keys of the
 * API's sort whitelist are sortable; the HTTP adapter drops any other key.
 */
export const ASSIGNMENT_COLUMNS: TableColumn<Scorecard>[] = [
  { key: 'employeeCode', header: 'Code', sortable: true, nowrap: true },
  { key: 'employeeName', header: 'Employee', sortable: true },
  { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'md' },
  { key: 'periodName', header: 'Period', sortable: true, hideBelow: 'sm', nowrap: true },
  { key: 'status', header: 'Status', type: 'status', sortable: true },
  { key: 'itemCount', header: 'KPIs', type: 'number', align: 'end', hideBelow: 'sm' },
  { key: 'totalWeight', header: 'Total weight %', align: 'end', nowrap: true },
  {
    key: 'overallScore',
    header: 'Score',
    align: 'end',
    sortable: true,
    hideBelow: 'lg',
    value: scoreText,
  },
  {
    key: 'reviewerName',
    header: 'Reviewer',
    hideBelow: 'lg',
    value: (s) => s.reviewerName ?? '—',
  },
];

/**
 * HR planning screen: every scorecard with its plan. Opens the plan detail, and starts a single or
 * a bulk assignment. The rows are scoped by the server; the buttons follow the `kpi-setup`
 * permissions (UX only: the server enforces them).
 */
@Component({
  selector: 'app-assignment-list',
  imports: [
    CellTemplateDirective,
    DataTableComponent,
    DecimalPipe,
    EmptyStateComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
    SearchFieldComponent,
  ],
  template: `
    <app-page-header
      title="KPI assignments"
      description="Plan each employee's KPIs for an evaluation period, then finalize the plan at exactly 100% weight."
    >
      <ng-container pageSearch>
        <app-search-field
          label="Search by employee code or name"
          [value]="query.search()"
          (searchChange)="query.setSearch($event)"
        />
        <app-filter-popup [activeCount]="query.activeFilterCount()" (cleared)="query.reset()">
          @for (f of filters(); track f.key) {
            <app-filter-field [label]="f.label">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  placeholder="All"
                  [attr.aria-label]="f.label"
                  [value]="query.filters()[f.key] ?? null"
                  (selectionChange)="query.setFilter(f.key, $event.value)"
                >
                  <mat-option [value]="null">All</mat-option>
                  @for (option of f.options; track option.value) {
                    <mat-option [value]="option.value">{{ option.label }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            </app-filter-field>
          }
        </app-filter-popup>
      </ng-container>
      <ng-container pageActions>
        @if (canCreate()) {
          <button mat-stroked-button type="button" (click)="openBulk()">
            <mat-icon>groups</mat-icon>Bulk assign
          </button>
          <button mat-flat-button type="button" (click)="openAssign()">
            <mat-icon>add</mat-icon>Assign KPIs
          </button>
        }
      </ng-container>
    </app-page-header>

    @if (page.error() && !result()) {
      <app-empty-state icon="error" title="Assignments could not be loaded" [message]="errorText()">
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <app-data-table
        caption="KPI assignments"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [sort]="query.sort()"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyTitle="No scorecards found"
        emptyMessage="Assign KPIs to an employee to start a plan, or adjust the filters."
        (pageChange)="query.onPage($event)"
        (sortChange)="query.setSort($event)"
        (rowClick)="open($event)"
      >
        <ng-template appCellTemplate="employeeCode" let-row>
          <a
            class="plan-link"
            [routerLink]="['/performance/assignments', row.id]"
            [attr.aria-label]="'Open the plan of ' + row.employeeCode + ' for ' + row.periodName"
            >{{ row.employeeCode }}</a
          >
        </ng-template>
        <ng-template appCellTemplate="totalWeight" let-row>
          <span class="weight" [class.weight--off]="isOff(row)">
            {{ row.totalWeight | number: '1.0-2' }}
            @if (isOff(row)) {
              <mat-icon aria-hidden="true">warning</mat-icon>
              <span class="cdk-visually-hidden">(not 100%)</span>
            }
          </span>
        </ng-template>
      </app-data-table>
    }
  `,
  styles: `
    .plan-link {
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .plan-link:hover,
    .plan-link:focus-visible {
      text-decoration: underline;
    }
    .weight {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .weight--off {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .weight mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
  `,
})
export class AssignmentListComponent {
  private readonly scorecards = inject(ScorecardDataService);
  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly departmentsApi = inject(DepartmentDataService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly toast = inject(UiNotificationService);
  private readonly permissions = inject(PermissionService);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly columns = ASSIGNMENT_COLUMNS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly isOff = weightIsOff;
  protected readonly rowLabel = (row: Scorecard) => `${row.employeeCode} ${row.periodName}`;
  /** UX only: assigning needs `kpi-setup:Create`. */
  protected readonly canCreate = computed(() => this.permissions.can('kpi-setup:Create'));

  protected readonly query = new ListQuery<Record<string, unknown>>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'employeeCode', direction: 'asc' },
  });

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) => this.scorecards.query(params as PageRequest<ScorecardFilter>),
  });
  /** Keeps the last page while the next one loads, so the table does not blank. */
  protected readonly result = linkedSignal<
    PagedResult<Scorecard> | undefined,
    PagedResult<Scorecard> | undefined
  >({
    source: this.page.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  private readonly lookups = rxResource({
    stream: () =>
      forkJoin({ periods: this.periodsApi.getAll(), departments: this.departmentsApi.getAll() }),
  });

  protected readonly filters = computed<FilterConfig[]>(() => {
    const l = this.lookups.value();
    return [
      { key: 'periodId', label: 'Period', options: options(l?.periods ?? [], (p) => p.name) },
      {
        key: 'departmentId',
        label: 'Department',
        options: options(l?.departments ?? [], (d) => d.name),
      },
      { key: 'status', label: 'Status', options: textOptions(SCORECARD_STATUSES) },
    ];
  });

  protected open(row: Scorecard): void {
    void this.router.navigate(['/performance/assignments', row.id]);
  }

  protected openAssign(): void {
    this.dialog
      .open<AssignKpisDialogComponent, void, AssignKpiResult>(AssignKpisDialogComponent, {
        width: '820px',
        maxWidth: '96vw',
        disableClose: true,
      })
      .afterClosed()
      .pipe(filter((r): r is AssignKpiResult => !!r))
      .subscribe((result) => {
        this.toast.success(assignmentMessage(result));
        this.page.reload();
      });
  }

  protected openBulk(): void {
    this.dialog
      .open<BulkAssignDialogComponent, void, BulkAssignmentResult>(BulkAssignDialogComponent, {
        width: '820px',
        maxWidth: '96vw',
        disableClose: true,
      })
      .afterClosed()
      // Whatever the outcome, the list may have changed.
      .subscribe(() => this.page.reload());
  }
}
