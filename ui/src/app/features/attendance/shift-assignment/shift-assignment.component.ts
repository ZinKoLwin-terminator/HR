import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { PermissionService } from '../../../core/services/permission.service';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import { EmployeeFilter, EmployeeListItem } from '../../../core/models/employee.models';
import { ShiftDataService } from '../../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { ListQuery } from '../../../shared/utilities/list-query';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';

/** Assign a shift to one or many employees at once. */
@Component({
  selector: 'app-shift-assignment',
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
  templateUrl: './shift-assignment.component.html',
  styleUrl: './shift-assignment.component.scss',
})
export class ShiftAssignmentComponent {
  /** Assigning a shift edits the employee: employees:Edit (UX only; the server enforces). */
  protected readonly permissions = inject(PermissionService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly shiftsApi = inject(ShiftDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly toast = inject(UiNotificationService);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly query = new ListQuery<EmployeeFilter>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'employeeCode', direction: 'asc' },
  });

  private readonly lookups = rxResource({
    stream: () => forkJoin({ org: this.lookupService.load(), shifts: this.shiftsApi.getAll() }),
  });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.org.departments ?? [], (d) => d.name),
  );
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.org.branches ?? [], (b) => b.name),
  );
  protected readonly shifts = computed(
    () => this.lookups.value()?.shifts.filter((s) => s.status === 'Active') ?? [],
  );
  protected readonly shiftOptions = computed(() =>
    options(this.lookups.value()?.shifts ?? [], (s) => s.name),
  );

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) => this.employeesApi.query(params),
  });
  protected readonly result = linkedSignal<
    PagedResult<EmployeeListItem> | undefined,
    PagedResult<EmployeeListItem> | undefined
  >({ source: this.page.value, computation: (value, previous) => value ?? previous?.value });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly targetShiftId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly targetShiftName = computed(
    () => this.shifts().find((s) => s.id === this.targetShiftId())?.name ?? '',
  );

  protected readonly columns: TableColumn<EmployeeListItem>[] = [
    { key: 'employeeCode', header: 'Code', sortable: true, width: '100px' },
    { key: 'fullName', header: 'Employee', sortable: true },
    { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'md' },
    { key: 'branchName', header: 'Branch', sortable: true, hideBelow: 'lg' },
    { key: 'shiftName', header: 'Current shift', sortable: true },
    { key: 'employmentStatus', header: 'Status', type: 'status', sortable: true, hideBelow: 'sm' },
  ];
  protected readonly rowLabel = (row: EmployeeListItem) => row.fullName;

  constructor() {
    effect(() => {
      const value = this.page.value();
      if (value && value.pageIndex !== this.query.pageIndex()) {
        this.query.pageIndex.set(value.pageIndex);
      }
    });
  }

  protected setFilter<K extends keyof EmployeeFilter>(
    key: K,
    value: EmployeeFilter[K] | null,
  ): void {
    this.query.setFilter(key, value);
  }

  protected value<K extends keyof EmployeeFilter>(key: K): EmployeeFilter[K] | null {
    return (this.query.filters()[key] ?? null) as EmployeeFilter[K] | null;
  }

  protected clearSelection(): void {
    this.selected.set(new Set());
  }

  protected assign(): void {
    const shiftId = this.targetShiftId();
    const ids = [...this.selected()];
    if (shiftId === null || ids.length === 0 || this.saving()) {
      return;
    }
    this.saving.set(true);
    forkJoin(ids.map((id) => this.employeesApi.update(id, { shiftId }))).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(
          `${this.targetShiftName()} assigned to ${ids.length} employee${ids.length === 1 ? '' : 's'}.`,
        );
        this.clearSelection();
        this.page.reload();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.toast.error(error);
      },
    });
  }
}
