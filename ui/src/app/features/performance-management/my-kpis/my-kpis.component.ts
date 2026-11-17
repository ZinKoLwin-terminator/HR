import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink } from '@angular/router';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../../core/models/common.models';
import { Scorecard, ScorecardFilter } from '../../../core/models/kpi.models';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { LateChipComponent } from '../shared/late-chip.component';
import { loadDueDates, rowIsLate } from '../shared/period-due-dates';
import { overallText } from '../shared/scorecard-rules';
import { injectScorecardViewer } from '../shared/scorecard-viewer';

/** The columns of My KPIs. The server's order (newest period first) is kept: nothing sorts here. */
export const MY_KPI_COLUMNS: TableColumn<Scorecard>[] = [
  { key: 'periodName', header: 'Period', nowrap: true },
  { key: 'status', header: 'Status', type: 'status' },
  { key: 'itemCount', header: 'KPIs', type: 'number', align: 'end', hideBelow: 'sm' },
  { key: 'overallScore', header: 'Overall score', value: overallText },
  { key: 'submittedOn', header: 'Submitted', nowrap: true, hideBelow: 'sm' },
];

/**
 * The signed-in employee's scorecards (every status after Planning), newest period first: status,
 * number of KPIs, the server's overall score and rating, and when it was submitted, with a Late
 * chip after the period's due date. A row opens the scorecard. An account without a linked
 * employee has no scorecards and is told so.
 */
@Component({
  selector: 'app-my-kpis',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    LateChipComponent,
    MatButtonModule,
    PageHeaderComponent,
    RouterLink,
  ],
  template: `
    <app-page-header
      title="My KPIs"
      description="Your KPI scorecards, newest period first. Open one to enter results, attach evidence and submit it for review."
    />

    @if (!linked()) {
      <app-empty-state
        icon="person_off"
        title="No employee record is linked to your account"
        message="Scorecards belong to employees. Ask HR to link your user account to your employee record."
      />
    } @else if (page.error() && !result()) {
      <app-empty-state icon="error" title="Your KPIs could not be loaded" [message]="errorText()">
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <app-data-table
        caption="My KPI scorecards"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyIcon="trending_up"
        emptyTitle="No scorecards yet"
        emptyMessage="Your KPIs appear here once HR has finalized your plan for an evaluation period."
        (pageChange)="query.onPage($event)"
        (rowClick)="open($event)"
      >
        <ng-template appCellTemplate="periodName" let-row>
          <a
            class="row-link"
            [routerLink]="['/performance/my-kpis', row.id]"
            [attr.aria-label]="'Open my scorecard for ' + row.periodName"
            >{{ row.periodName }}</a
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
export class MyKpisComponent {
  private readonly scorecards = inject(ScorecardDataService);
  private readonly router = inject(Router);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly viewer = injectScorecardViewer();

  protected readonly columns = MY_KPI_COLUMNS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly rowLabel = (row: Scorecard) => `My scorecard ${row.periodName}`;
  /** Without a linked employee there is nothing to ask for. */
  protected readonly linked = computed(() => this.viewer().employeeId !== null);

  protected readonly query = new ListQuery<ScorecardFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });

  protected readonly page = rxResource({
    params: () => (this.linked() ? this.query.request() : undefined),
    stream: ({ params }) => this.scorecards.myScorecards(params as PageRequest<ScorecardFilter>),
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

  protected isLate(row: Scorecard): boolean {
    return rowIsLate(row, this.dueDates.value());
  }

  protected open(row: Scorecard): void {
    void this.router.navigate(['/performance/my-kpis', row.id]);
  }
}
