import { Component, computed, effect, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { PagedResult } from '../../../core/models/common.models';
import { Payslip } from '../../../core/models/payroll.models';
import { MyPayslipDataService } from '../../../core/services/data/payroll-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ListQuery } from '../../../shared/utilities/list-query';

/** The API's default page size for "My payslips". */
export const MY_PAYSLIPS_PAGE_SIZE = 12;
const PAGE_SIZE_OPTIONS = [12, 24, 60];

/**
 * The signed-in user's own payslips, newest period first. Only payslips of approved or paid
 * periods appear. Open one to preview and print it. Sample data only.
 */
@Component({
  selector: 'app-my-payslips-list',
  imports: [MatButtonModule, PageHeaderComponent, DataTableComponent, EmptyStateComponent],
  template: `
    <app-page-header
      title="My payslips"
      description="Your own payslips for approved and paid periods, newest first. Select one to view or print it. Sample data only."
    />

    @if (page.error() && !result()) {
      <app-empty-state
        icon="error"
        title="Your payslips could not be loaded"
        [message]="errorText()"
      >
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <app-data-table
        caption="My payslips"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [loading]="page.isLoading()"
        [actions]="actions"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyTitle="No payslips yet"
        emptyMessage="Payslips appear here once your payroll period is approved."
        emptyIcon="receipt_long"
        (pageChange)="query.onPage($event)"
        (action)="onAction($event)"
        (rowClick)="open($event)"
      />
    }
  `,
})
export class MyPayslipsListComponent {
  private readonly api = inject(MyPayslipDataService);
  private readonly router = inject(Router);

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly query = new ListQuery<Record<string, never>>({
    pageSize: MY_PAYSLIPS_PAGE_SIZE,
  });

  protected readonly page = rxResource({
    params: () => ({ pageIndex: this.query.pageIndex(), pageSize: this.query.pageSize() }),
    stream: ({ params }) => this.api.query(params),
  });
  /** Keeps the last page on screen while the next one loads. */
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
    { key: 'periodName', header: 'Period', nowrap: true },
    {
      key: 'grossSalary',
      header: 'Gross',
      type: 'currency',
      align: 'end',
      hideBelow: 'sm',
    },
    {
      key: 'totalDeductions',
      header: 'Deductions',
      type: 'currency',
      align: 'end',
      hideBelow: 'md',
    },
    { key: 'netSalary', header: 'Net', type: 'currency', align: 'end' },
  ];
  protected readonly actions: TableAction<Payslip>[] = [
    { id: 'view', label: 'View payslip', icon: 'receipt_long' },
  ];
  protected readonly rowLabel = (p: Payslip) => `payslip for ${p.periodName}`;

  constructor() {
    // Follow the server when it clamps the page index.
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.query.pageIndex()) {
        this.query.pageIndex.set(value.pageIndex);
      }
    });
  }

  protected open(row: Payslip): void {
    this.router.navigate(['/my/payslips', row.id]);
  }

  protected onAction(event: TableActionEvent<Payslip>): void {
    this.open(event.row);
  }
}
