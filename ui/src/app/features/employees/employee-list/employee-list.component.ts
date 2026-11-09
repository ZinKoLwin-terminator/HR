import { Component, computed, effect, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import {
  EMPLOYMENT_STATUSES,
  EMPLOYMENT_TYPES,
  EmployeeFilter,
  EmployeeListItem,
} from '../../../core/models/employee.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  UiNotificationService,
  errorMessage,
} from '../../../core/services/ui-notification.service';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  TableAction,
  TableActionEvent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ExcelExportService } from '../../../shared/components/excel/excel-export.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { ListQuery } from '../../../shared/utilities/list-query';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';

@Component({
  selector: 'app-employee-list',
  imports: [
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    SearchFieldComponent,
    DataTableComponent,
    CellTemplateDirective,
    AvatarComponent,
    EmptyStateComponent,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  templateUrl: './employee-list.component.html',
  styleUrl: './employee-list.component.scss',
})
export class EmployeeListComponent {
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly excelExport = inject(ExcelExportService);

  /** `?search=` query parameter (bound by the router; used by the header search box). */
  readonly search = input<string>();

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly query = new ListQuery<EmployeeFilter>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'employeeCode', direction: 'asc' },
  });

  protected readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.departments ?? [], (d) => d.name),
  );
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.branches ?? [], (b) => b.name),
  );
  protected readonly statusOptions = EMPLOYMENT_STATUSES;
  protected readonly typeOptions = EMPLOYMENT_TYPES;

  /** Removable chips describing every applied filter. */
  protected readonly activeChips = computed(() => {
    const f = this.query.filters();
    const labelOf = (opts: { value: unknown; label: string }[], v: unknown) =>
      opts.find((o) => o.value === v)?.label ?? String(v);
    const chips: { key: keyof EmployeeFilter | 'search'; name: string; label: string }[] = [];
    const term = this.query.search();
    if (term) chips.push({ key: 'search', name: 'Search', label: term });
    if (f.employmentStatus)
      chips.push({ key: 'employmentStatus', name: 'Status', label: f.employmentStatus });
    if (f.departmentId)
      chips.push({
        key: 'departmentId',
        name: 'Department',
        label: labelOf(this.departmentOptions(), f.departmentId),
      });
    if (f.branchId)
      chips.push({
        key: 'branchId',
        name: 'Branch',
        label: labelOf(this.branchOptions(), f.branchId),
      });
    if (f.employmentType)
      chips.push({ key: 'employmentType', name: 'Type', label: f.employmentType });
    if (f.joiningFrom)
      chips.push({ key: 'joiningFrom', name: 'Joined from', label: f.joiningFrom });
    if (f.joiningTo) chips.push({ key: 'joiningTo', name: 'Joined to', label: f.joiningTo });
    return chips;
  });
  /** Number of applied filters (excluding the search term). */
  protected readonly filterCount = computed(
    () => this.activeChips().filter((c) => c.key !== 'search').length,
  );

  protected readonly page = rxResource({
    params: () => this.query.request(),
    stream: ({ params }) => this.employeesApi.query(params),
  });
  /** Keeps the previous page visible while the next one loads. */
  protected readonly result = linkedSignal<
    PagedResult<EmployeeListItem> | undefined,
    PagedResult<EmployeeListItem> | undefined
  >({
    source: this.page.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly columns: TableColumn<EmployeeListItem>[] = [
    { key: 'employeeCode', header: 'Code', sortable: true, width: '100px', nowrap: true },
    { key: 'fullName', header: 'Employee', sortable: true },
    { key: 'gender', header: 'Gender', sortable: true, hideBelow: 'lg', nowrap: true },
    { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'md' },
    { key: 'positionTitle', header: 'Position', sortable: true, hideBelow: 'md', width: '140px' },
    { key: 'branchName', header: 'Branch', sortable: true, hideBelow: 'lg', width: '130px' },
    { key: 'employmentType', header: 'Type', sortable: true, hideBelow: 'lg', nowrap: true },
    {
      key: 'joiningDate',
      header: 'Joined',
      type: 'date',
      sortable: true,
      hideBelow: 'sm',
      nowrap: true,
    },
    { key: 'employmentStatus', header: 'Status', type: 'status', sortable: true },
  ];

  protected readonly actions: TableAction<EmployeeListItem>[] = [
    { id: 'view', label: 'View profile', icon: 'badge' },
    { id: 'edit', label: 'Edit', icon: 'edit' },
    { id: 'delete', label: 'Delete', icon: 'delete', destructive: true },
  ];

  protected readonly rowLabel = (row: EmployeeListItem) => row.fullName;

  constructor() {
    effect(() => {
      const term = this.search();
      if (term !== undefined && term !== this.query.search()) {
        this.query.setSearch(term);
      }
    });
    // After a delete on the last page the service clamps the index; follow it.
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
    this.query.setFilter(key, value === '' ? null : value);
  }

  protected removeChip(key: keyof EmployeeFilter | 'search'): void {
    if (key === 'search') this.query.setSearch('');
    else this.setFilter(key, null);
  }

  protected value<K extends keyof EmployeeFilter>(key: K): EmployeeFilter[K] | null {
    return (this.query.filters()[key] ?? null) as EmployeeFilter[K] | null;
  }

  /** Exports the employees matching the current search and filters (server-side, not just this page). */
  protected openExport(): void {
    const labelOf = (options: { value: unknown; label: string }[], value: unknown) =>
      options.find((o) => o.value === value)?.label ?? String(value);
    const summary: string[] = [];
    const f = this.query.filters();
    if (f.departmentId)
      summary.push(`Department: ${labelOf(this.departmentOptions(), f.departmentId)}`);
    if (f.branchId) summary.push(`Branch: ${labelOf(this.branchOptions(), f.branchId)}`);
    if (f.employmentStatus) summary.push(`Status: ${f.employmentStatus}`);
    if (f.employmentType) summary.push(`Type: ${f.employmentType}`);
    if (f.joiningFrom) summary.push(`Joined from: ${f.joiningFrom}`);
    if (f.joiningTo) summary.push(`Joined to: ${f.joiningTo}`);
    this.excelExport
      .open({
        moduleCode: 'employees',
        search: this.query.search() || undefined,
        filters: { ...f },
        filterSummary: summary,
      })
      .subscribe();
  }

  protected openProfile(row: EmployeeListItem): void {
    this.router.navigate(['/employees', row.id]);
  }

  protected onAction({ action, row }: TableActionEvent<EmployeeListItem>): void {
    if (action === 'view') {
      this.openProfile(row);
    } else if (action === 'edit') {
      this.router.navigate(['/employees', row.id, 'edit']);
    } else if (action === 'delete') {
      this.confirm
        .confirmDelete('Employee', `${row.fullName} (${row.employeeCode})`)
        .pipe(filter(Boolean))
        .subscribe(() =>
          this.employeesApi.delete(row.id).subscribe({
            next: () => {
              this.toast.success(`Employee "${row.fullName}" deleted.`);
              this.page.reload();
            },
            error: (error: unknown) => this.toast.error(error),
          }),
        );
    }
  }
}
