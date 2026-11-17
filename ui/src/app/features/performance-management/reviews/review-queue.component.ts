import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../../core/models/common.models';
import { Scorecard, ScorecardFilter } from '../../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  ScorecardDataService,
} from '../../../core/services/data/kpi-data.services';
import { DepartmentDataService } from '../../../core/services/data/organization-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
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
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { options } from '../../organization/organization-lookups';
import { textOptions } from '../shared/kpi-options';
import { LateChipComponent } from '../shared/late-chip.component';
import { loadDueDates, rowIsLate } from '../shared/period-due-dates';
import { REVIEW_QUEUE_STATUSES, overallText, stageLabel } from '../shared/scorecard-rules';

/** The queue's columns. The server's order (oldest submission first) is kept: nothing sorts here. */
export const REVIEW_QUEUE_COLUMNS: TableColumn<Scorecard>[] = [
  { key: 'employeeCode', header: 'Code', nowrap: true },
  { key: 'employeeName', header: 'Employee' },
  { key: 'periodName', header: 'Period', nowrap: true, hideBelow: 'sm' },
  { key: 'status', header: 'Status', type: 'status' },
  { key: 'submittedOn', header: 'Submitted', nowrap: true, hideBelow: 'sm' },
  { key: 'overallScore', header: 'Score', value: overallText, hideBelow: 'md' },
  { key: 'stage', header: 'Stage', value: (s) => stageLabel(s.status), hideBelow: 'md' },
];

/**
 * Scorecards waiting for the signed-in reviewer: the manager stage for the scorecards that name
 * them reviewer (HR sees every one), and, for HR, the Approved ones waiting for the final review
 * (except those they approved themselves). Never the viewer's own. Oldest submission first, as
 * the server returns them; filter by period, department and status, search by employee.
 */
@Component({
  selector: 'app-review-queue',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    LateChipComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
    SearchFieldComponent,
  ],
  template: `
    <app-page-header
      title="KPI reviews"
      description="Scorecards waiting for your review, oldest submission first. Open one to rate it and approve, return or reject it."
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
                  [value]="filterValue(f.key)"
                  (selectionChange)="setFilter(f.key, $event.value)"
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
    </app-page-header>

    @if (page.error() && !result()) {
      <app-empty-state
        icon="error"
        title="The review queue could not be loaded"
        [message]="errorText()"
      >
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <app-data-table
        caption="Scorecards waiting for review"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyIcon="task_alt"
        emptyTitle="Nothing to review"
        emptyMessage="No scorecard is waiting for you. Adjust the filters to look again."
        (pageChange)="query.onPage($event)"
        (rowClick)="open($event)"
      >
        <ng-template appCellTemplate="employeeCode" let-row>
          <a
            class="row-link"
            [routerLink]="['/performance/reviews', row.id]"
            [attr.aria-label]="
              'Review the scorecard of ' + row.employeeCode + ' for ' + row.periodName
            "
            >{{ row.employeeCode }}</a
          >
        </ng-template>
        <ng-template appCellTemplate="submittedOn" let-row>
          <span class="submitted">
            {{ row.submittedOn ? (row.submittedOn | appDate) : '—' }}
            @if (isLate(row)) {
              <app-late-chip />
            }
          </span>
        </ng-template>
      </app-data-table>
    }
  `,
  styles: `
    .row-link {
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .row-link:hover,
    .row-link:focus-visible {
      text-decoration: underline;
    }
    .submitted {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
  `,
})
export class ReviewQueueComponent {
  private readonly scorecards = inject(ScorecardDataService);
  private readonly periodsApi = inject(EvaluationPeriodDataService);
  private readonly departmentsApi = inject(DepartmentDataService);
  private readonly router = inject(Router);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly columns = REVIEW_QUEUE_COLUMNS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly rowLabel = (row: Scorecard) => `${row.employeeCode} ${row.periodName}`;

  protected readonly query = new ListQuery<ScorecardFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) => this.scorecards.pendingReviews(params as PageRequest<ScorecardFilter>),
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

  /** Due dates of the periods on this page (the list answer has none), for the Late chip. */
  private readonly dueDates = rxResource({
    params: () => this.result()?.items,
    stream: ({ params }) => loadDueDates(this.scorecards, params),
  });

  /**
   * The period picker needs no setup permission. A reviewer who cannot list departments still
   * gets the queue, without that filter's options.
   */
  private readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        periods: this.periodsApi.options().pipe(catchError(() => of([]))),
        departments: this.departmentsApi.getAll().pipe(catchError(() => of([]))),
      }),
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
      { key: 'status', label: 'Status', options: textOptions(REVIEW_QUEUE_STATUSES) },
    ];
  });

  protected filterValue(key: string): unknown {
    return (this.query.filters() as Record<string, unknown>)[key] ?? null;
  }

  protected setFilter(key: string, value: unknown): void {
    this.query.setFilter(key as keyof ScorecardFilter, value as never);
  }

  protected isLate(row: Scorecard): boolean {
    return rowIsLate(row, this.dueDates.value());
  }

  protected open(row: Scorecard): void {
    void this.router.navigate(['/performance/reviews', row.id]);
  }
}
