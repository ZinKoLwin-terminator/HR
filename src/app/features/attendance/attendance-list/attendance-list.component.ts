import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult } from '../../../core/models/common.models';
import {
  ATTENDANCE_STATUSES,
  AttendanceFilter,
  AttendanceListItem,
} from '../../../core/models/attendance.models';
import { AttendanceRecordDataService } from '../../../core/services/data/attendance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import {
  EntityDetailsDialogData,
  FieldConfig,
  FormValues,
} from '../../../shared/components/crud/crud.models';
import { EntityDetailsDialogComponent } from '../../../shared/components/crud/entity-details-dialog.component';
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
import { formatDuration } from '../../../shared/utilities/date.util';
import { ListQuery } from '../../../shared/utilities/list-query';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import {
  OrganizationLookupService,
  employeeOptions,
  options,
} from '../../organization/organization-lookups';

export type AttendanceListMode = 'all' | 'late' | 'early';

const minutes = (value: number) => (value > 0 ? formatDuration(value) : '—');

const DETAIL_FIELDS: FieldConfig[] = [
  { key: 'employeeText', label: 'Employee', type: 'text' },
  { key: 'date', label: 'Date', type: 'date' },
  { key: 'departmentName', label: 'Department', type: 'text' },
  { key: 'shiftName', label: 'Shift', type: 'text' },
  { key: 'checkIn', label: 'Check-in', type: 'text' },
  { key: 'checkOut', label: 'Check-out', type: 'text' },
  { key: 'workedText', label: 'Worked', type: 'text' },
  { key: 'lateText', label: 'Late by', type: 'text' },
  { key: 'earlyText', label: 'Left early by', type: 'text' },
  { key: 'overtimeText', label: 'Overtime', type: 'text' },
  { key: 'status', label: 'Status', type: 'text' },
  { key: 'source', label: 'Source', type: 'text' },
  { key: 'remarks', label: 'Remarks', type: 'text', fullWidth: true },
];

/**
 * Attendance records with filters (employee, department, branch, date range,
 * status). `late` / `early` modes show only exceptions.
 */
@Component({
  selector: 'app-attendance-list',
  imports: [
    NgTemplateOutlet,
    PageHeaderComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    FilterFieldComponent,
    FilterPopupComponent,
    SearchFieldComponent,
    DataTableComponent,
    EmptyStateComponent,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  templateUrl: './attendance-list.component.html',
  styleUrl: './attendance-list.component.scss',
})
export class AttendanceListComponent {
  readonly mode = input<AttendanceListMode>('all');
  /** When set, the list renders its own page header with search + filters on the title row. */
  readonly title = input('');
  readonly description = input('');

  private readonly api = inject(AttendanceRecordDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly dialog = inject(MatDialog);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly query = new ListQuery<AttendanceFilter>({
    pageSize: this.appConfig.defaultPageSize,
    sort: { active: 'date', direction: 'desc' },
  });
  protected readonly statuses = ATTENDANCE_STATUSES;

  protected readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly employeeOptions = computed(() =>
    employeeOptions(this.lookups.value()?.employees ?? []),
  );
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.departments ?? [], (d) => d.name),
  );
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.branches ?? [], (b) => b.name),
  );

  /** Removable chips describing every applied filter. */
  protected readonly activeChips = computed(() => {
    const f = this.query.filters();
    const labelOf = (opts: { value: unknown; label: string }[], v: unknown) =>
      opts.find((o) => o.value === v)?.label ?? String(v);
    const chips: { key: keyof AttendanceFilter | 'search'; name: string; label: string }[] = [];
    const term = this.query.search();
    if (term) chips.push({ key: 'search', name: 'Search', label: term });
    if (f.employeeId)
      chips.push({
        key: 'employeeId',
        name: 'Employee',
        label: labelOf(this.employeeOptions(), f.employeeId),
      });
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
    if (f.status) chips.push({ key: 'status', name: 'Status', label: f.status });
    if (f.dateFrom) chips.push({ key: 'dateFrom', name: 'From', label: f.dateFrom });
    if (f.dateTo) chips.push({ key: 'dateTo', name: 'To', label: f.dateTo });
    return chips;
  });
  /** Number of applied filters (excluding the search term). */
  protected readonly filterCount = computed(
    () => this.activeChips().filter((c) => c.key !== 'search').length,
  );

  protected readonly page = rxResource({
    params: () => ({ request: this.query.request(), mode: this.mode() }),
    stream: ({ params }) =>
      this.api.query({
        ...params.request,
        filters: {
          ...params.request.filters,
          lateOnly: params.mode === 'late' ? true : null,
          earlyOnly: params.mode === 'early' ? true : null,
        },
      }),
  });
  protected readonly result = linkedSignal<
    PagedResult<AttendanceListItem> | undefined,
    PagedResult<AttendanceListItem> | undefined
  >({ source: this.page.value, computation: (value, previous) => value ?? previous?.value });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected readonly columns = computed<TableColumn<AttendanceListItem>[]>(() => {
    const base: TableColumn<AttendanceListItem>[] = [
      { key: 'date', header: 'Date', type: 'date', sortable: true, nowrap: true },
      {
        key: 'employeeName',
        header: 'Employee',
        sortable: true,
        value: (r) => `${r.employeeName} (${r.employeeCode})`,
      },
      { key: 'departmentName', header: 'Department', sortable: true, hideBelow: 'lg' },
      { key: 'shiftName', header: 'Shift', hideBelow: 'lg' },
    ];
    const status: TableColumn<AttendanceListItem> = {
      key: 'status',
      header: 'Status',
      type: 'status',
      sortable: true,
    };
    const checkIn: TableColumn<AttendanceListItem> = {
      key: 'checkIn',
      header: 'Check-in',
      sortable: true,
      nowrap: true,
    };
    const checkOut: TableColumn<AttendanceListItem> = {
      key: 'checkOut',
      header: 'Check-out',
      sortable: true,
      nowrap: true,
    };
    switch (this.mode()) {
      case 'late':
        return [
          ...base,
          checkIn,
          {
            key: 'lateMinutes',
            header: 'Late by',
            sortable: true,
            align: 'end',
            value: (r) => minutes(r.lateMinutes),
          },
          status,
        ];
      case 'early':
        return [
          ...base,
          checkOut,
          {
            key: 'earlyLeaveMinutes',
            header: 'Left early by',
            sortable: true,
            align: 'end',
            value: (r) => minutes(r.earlyLeaveMinutes),
          },
          {
            key: 'workedMinutes',
            header: 'Worked',
            align: 'end',
            hideBelow: 'md',
            value: (r) => minutes(r.workedMinutes),
          },
          status,
        ];
      default:
        return [
          ...base,
          checkIn,
          checkOut,
          {
            key: 'workedMinutes',
            header: 'Worked',
            sortable: true,
            align: 'end',
            hideBelow: 'md',
            value: (r) => minutes(r.workedMinutes),
          },
          {
            key: 'overtimeMinutes',
            header: 'Overtime',
            sortable: true,
            align: 'end',
            hideBelow: 'lg',
            value: (r) => minutes(r.overtimeMinutes),
          },
          status,
          { key: 'source', header: 'Source', hideBelow: 'lg' },
        ];
    }
  });

  protected readonly actions: TableAction<AttendanceListItem>[] = [
    { id: 'view', label: 'View details', icon: 'visibility' },
  ];
  protected readonly rowLabel = (r: AttendanceListItem) => `${r.employeeName} on ${r.date}`;
  protected readonly emptyTitle = computed(() =>
    this.mode() === 'late'
      ? 'No late arrivals found'
      : this.mode() === 'early'
        ? 'No early departures found'
        : 'No attendance records found',
  );

  protected setFilter<K extends keyof AttendanceFilter>(
    key: K,
    value: AttendanceFilter[K] | null,
  ): void {
    this.query.setFilter(key, value === '' ? null : value);
  }

  protected value<K extends keyof AttendanceFilter>(key: K): AttendanceFilter[K] | null {
    return (this.query.filters()[key] ?? null) as AttendanceFilter[K] | null;
  }

  protected removeChip(key: keyof AttendanceFilter | 'search'): void {
    if (key === 'search') this.query.setSearch('');
    else this.setFilter(key, null);
  }

  protected openDetails({ row }: TableActionEvent<AttendanceListItem>): void {
    this.showDetails(row);
  }

  protected showDetails(r: AttendanceListItem): void {
    const values: FormValues = {
      ...r,
      employeeText: `${r.employeeName} (${r.employeeCode})`,
      checkIn: r.checkIn ?? '—',
      checkOut: r.checkOut ?? '—',
      workedText: minutes(r.workedMinutes),
      lateText: minutes(r.lateMinutes),
      earlyText: minutes(r.earlyLeaveMinutes),
      overtimeText: minutes(r.overtimeMinutes),
    };
    this.dialog.open<EntityDetailsDialogComponent, EntityDetailsDialogData>(
      EntityDetailsDialogComponent,
      {
        data: {
          title: `Attendance · ${r.date}`,
          subtitle: r.employeeName,
          fields: DETAIL_FIELDS,
          row: values,
          id: r.id,
          readonly: true,
        },
        width: '640px',
        maxWidth: '96vw',
      },
    );
  }
}
