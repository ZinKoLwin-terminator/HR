import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import { EmployeeLeaveBalances } from '../../../core/models/leave.models';
import {
  LeaveBalanceDataService,
  LeaveTypeDataService,
} from '../../../core/services/data/leave-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { SortState } from '../../../core/models/common.models';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';

type Row = EmployeeLeaveBalances & { id: number };

/** Every balance of one employee, with a usage bar per leave type. */
@Component({
  selector: 'app-leave-balance-dialog',
  imports: [DecimalPipe, MatDialogModule, MatButtonModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>{{ data.row.employeeName }} · {{ data.year }}</h2>
    <mat-dialog-content>
      <p class="sub">{{ data.row.employeeCode }} · {{ data.row.departmentName }}</p>
      <table class="simple-table">
        <caption class="cdk-visually-hidden">
          Leave balances
        </caption>
        <thead>
          <tr>
            <th scope="col">Leave type</th>
            <th scope="col" class="num">Entitled</th>
            <th scope="col" class="num">Taken</th>
            <th scope="col" class="num">Pending</th>
            <th scope="col" class="num">Available</th>
            <th scope="col" class="bar">Used</th>
          </tr>
        </thead>
        <tbody>
          @for (b of data.row.balances; track b.leaveTypeId) {
            <tr>
              <th scope="row">
                <span class="dot" [style.background]="b.color" aria-hidden="true"></span
                >{{ b.name }}
              </th>
              <td class="num">{{ b.entitled | number: '1.0-1' }}</td>
              <td class="num">{{ b.taken | number: '1.0-1' }}</td>
              <td class="num">{{ b.pending | number: '1.0-1' }}</td>
              <td class="num strong">{{ b.available | number: '1.0-1' }}</td>
              <td class="bar">
                <mat-progress-bar
                  mode="determinate"
                  [value]="usage(b.taken + b.pending, b.entitled)"
                  [attr.aria-label]="b.name + ' used'"
                />
              </td>
            </tr>
          }
        </tbody>
      </table>
      <p class="note">
        Entitlements follow the leave policies for the employee's grade and employment type.
        Approved leave counts as taken; submitted leave is held as pending.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .sub {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .note {
      margin: 12px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      margin-right: 8px;
      border-radius: 50%;
    }
    .strong {
      font-weight: 600;
    }
    .bar {
      width: 130px;
      min-width: 100px;
    }
  `,
})
export class LeaveBalanceDialogComponent {
  protected readonly data = inject<{ row: EmployeeLeaveBalances; year: number }>(MAT_DIALOG_DATA);

  protected usage(used: number, entitled: number): number {
    return entitled ? Math.min(100, Math.round((used / entitled) * 100)) : 0;
  }
}

@Component({
  selector: 'app-leave-balances',
  imports: [
    FilterFieldComponent,
    FilterPopupComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    SearchFieldComponent,
    DataTableComponent,
    EmptyStateComponent,
  ],
  templateUrl: './leave-balances.component.html',
})
export class LeaveBalancesComponent {
  private readonly balancesApi = inject(LeaveBalanceDataService);
  private readonly typesApi = inject(LeaveTypeDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly dialog = inject(MatDialog);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly currentYear = new Date().getFullYear();
  protected readonly yearOptions = [this.currentYear, this.currentYear - 1];

  protected readonly year = signal(this.currentYear);
  protected readonly search = signal('');
  protected readonly departmentId = signal<number | null>(null);
  protected readonly branchId = signal<number | null>(null);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(this.appConfig.defaultPageSize);
  protected readonly sort = signal<SortState>({ active: 'employeeCode', direction: 'asc' });

  private readonly lookups = rxResource({
    stream: () => forkJoin({ org: this.lookupService.load(), types: this.typesApi.getAll() }),
  });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.org.departments ?? [], (d) => d.name),
  );
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.org.branches ?? [], (b) => b.name),
  );

  protected readonly page = rxResource({
    params: () => ({
      year: this.year(),
      search: this.search(),
      departmentId: this.departmentId(),
      branchId: this.branchId(),
      pageIndex: this.pageIndex(),
      pageSize: this.pageSize(),
      sort: this.sort(),
    }),
    stream: ({ params }) => this.balancesApi.query(params),
  });
  protected readonly result = linkedSignal<
    PagedResult<EmployeeLeaveBalances> | undefined,
    PagedResult<Row> | undefined
  >({
    source: this.page.value,
    computation: (value, previous) =>
      value
        ? { ...value, items: value.items.map((r) => ({ ...r, id: r.employeeId })) }
        : previous?.value,
  });
  /** Applied filters (year always has a value, so it is not counted). */
  protected readonly filterCount = computed(
    () => (this.departmentId() === null ? 0 : 1) + (this.branchId() === null ? 0 : 1),
  );

  protected clearFilters(): void {
    this.departmentId.set(null);
    this.branchId.set(null);
    this.pageIndex.set(0);
  }

  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  /** One column per leave type available to everyone (gender-specific types appear in the details). */
  protected readonly columns = computed<TableColumn<Row>[]>(() => {
    const types = (this.lookups.value()?.types ?? []).filter(
      (t) => t.status === 'Active' && !t.eligibleGender,
    );
    return [
      { key: 'employeeCode', header: 'Code', sortable: true, width: '100px' },
      { key: 'employeeName', header: 'Employee', sortable: true },
      { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'lg' },
      ...types.map<TableColumn<Row>>((t, index) => ({
        key: `type_${t.id}`,
        header: t.name,
        align: 'end',
        nowrap: true,
        hideBelow: index < 3 ? undefined : 'md',
        value: (row) => {
          const b = row.balances.find((x) => x.leaveTypeId === t.id);
          return b ? `${b.available} / ${b.entitled}` : '—';
        },
      })),
    ];
  });

  protected readonly actions: TableAction<Row>[] = [
    { id: 'view', label: 'View all balances', icon: 'visibility' },
  ];
  protected readonly rowLabel = (r: Row) => r.employeeName;

  constructor() {
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.pageIndex()) {
        this.pageIndex.set(value.pageIndex);
      }
    });
  }

  protected setSearch(term: string): void {
    this.search.set(term);
    this.pageIndex.set(0);
  }

  protected setFilter(kind: 'department' | 'branch' | 'year', value: number | null): void {
    if (kind === 'department') this.departmentId.set(value);
    else if (kind === 'branch') this.branchId.set(value);
    else this.year.set(value ?? this.currentYear);
    this.pageIndex.set(0);
  }

  protected openDetails(row: Row): void {
    this.dialog.open(LeaveBalanceDialogComponent, {
      data: { row, year: this.year() },
      width: '720px',
      maxWidth: '96vw',
    });
  }

  protected onAction(event: TableActionEvent<Row>): void {
    this.openDetails(event.row);
  }

  protected onSort(sort: SortState): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
  }

  protected onPage(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }
}
