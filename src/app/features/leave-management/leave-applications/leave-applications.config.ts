import { Employee, fullName } from '../../../core/models/employee.models';
import { Holiday } from '../../../core/models/attendance.models';
import { LEAVE_STATUSES, LeaveApplication, LeaveType } from '../../../core/models/leave.models';
import { LeaveApplicationDataService } from '../../../core/services/data/leave-data.services';
import { CrudExtraAction, CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { countWorkingDays, todayIso } from '../../../shared/utilities/date.util';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import { employeeOptions, options } from '../../organization/organization-lookups';

export type LeaveListMode = 'applications' | 'pending' | 'history';

export interface LeaveConfigOptions {
  mode: LeaveListMode;
  service: LeaveApplicationDataService;
  employees: Employee[];
  types: LeaveType[];
  holidays: Holiday[];
  weekendDays: readonly number[];
}

const statusOptions = LEAVE_STATUSES.map((s) => ({ value: s, label: s }));

const formatDate = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });

/** Builds the CRUD page configuration for the leave application screens. */
export function buildLeaveApplicationsConfig(
  o: LeaveConfigOptions,
): CrudPageConfig<LeaveApplication> {
  const people = new Map(o.employees.map((e) => [e.id, `${fullName(e)} (${e.employeeCode})`]));
  const typeName = new Map(o.types.map((t) => [t.id, t.name]));
  const holidaySet = new Set(o.holidays.map((h) => h.date));
  const employeeName = (a: LeaveApplication) => people.get(a.employeeId) ?? `#${a.employeeId}`;
  const label = (a: LeaveApplication) => `${a.applicationNo} · ${employeeName(a)}`;
  const isDraft = (a: LeaveApplication) => a.status === 'Draft';
  const isSubmitted = (a: LeaveApplication) => a.status === 'Submitted';
  const canCancel = (a: LeaveApplication) =>
    isSubmitted(a) || (a.status === 'Approved' && a.startDate > todayIso());
  const days = (a: LeaveApplication) => `${a.days} day${a.days === 1 ? '' : 's'}`;

  const submit: CrudExtraAction<LeaveApplication> = {
    id: 'submit',
    label: 'Submit for approval',
    icon: 'send',
    visible: isDraft,
    confirm: {
      title: 'Submit for approval?',
      message: (a) =>
        `Submit ${a.applicationNo} (${days(a)}) for approval? You cannot change it afterwards.`,
      confirmText: 'Submit',
    },
    run: (a) => o.service.submit(a.id),
    successMessage: (a) => `${a.applicationNo} submitted for approval.`,
  };
  const approve: CrudExtraAction<LeaveApplication> = {
    id: 'approve',
    label: 'Approve',
    icon: 'check_circle',
    visible: isSubmitted,
    prompt: {
      title: 'Approve leave?',
      message: (a) =>
        `${employeeName(a)} · ${typeName.get(a.leaveTypeId)} · ${formatDate(a.startDate)} – ${formatDate(a.endDate)} (${days(a)})`,
      label: 'Remarks (optional)',
      required: false,
      confirmText: 'Approve',
    },
    run: (a, remarks) => o.service.decide(a.id, 'Approved', remarks),
    successMessage: (a) => `${a.applicationNo} approved.`,
  };
  const reject: CrudExtraAction<LeaveApplication> = {
    id: 'reject',
    label: 'Reject',
    icon: 'cancel',
    destructive: true,
    visible: isSubmitted,
    prompt: {
      title: 'Reject leave?',
      message: (a) =>
        `${employeeName(a)} · ${typeName.get(a.leaveTypeId)} · ${formatDate(a.startDate)} – ${formatDate(a.endDate)}`,
      label: 'Reason for rejection',
      required: true,
      confirmText: 'Reject',
    },
    run: (a, remarks) => o.service.decide(a.id, 'Rejected', remarks),
    successMessage: (a) => `${a.applicationNo} rejected.`,
  };
  const cancel: CrudExtraAction<LeaveApplication> = {
    id: 'cancel',
    label: 'Cancel leave',
    icon: 'event_busy',
    destructive: true,
    visible: canCancel,
    prompt: {
      title: 'Cancel this leave?',
      message: (a) => `${a.applicationNo} will be cancelled and its days returned to the balance.`,
      label: 'Reason (optional)',
      required: false,
      confirmText: 'Cancel leave',
    },
    run: (a, remarks) => o.service.cancel(a.id, remarks),
    successMessage: (a) => `${a.applicationNo} cancelled.`,
  };

  const baseColumns: TableColumn<LeaveApplication>[] = [
    { key: 'applicationNo', header: 'Application', sortable: true, nowrap: true },
    { key: 'employeeId', header: 'Employee', value: employeeName },
    {
      key: 'leaveTypeId',
      header: 'Leave type',
      hideBelow: 'sm',
      value: (a) => typeName.get(a.leaveTypeId) ?? '—',
    },
    {
      key: 'startDate',
      header: 'Dates',
      sortable: true,
      nowrap: true,
      value: (a) =>
        a.startDate === a.endDate
          ? formatDate(a.startDate)
          : `${formatDate(a.startDate)} – ${formatDate(a.endDate)}`,
    },
    { key: 'days', header: 'Days', type: 'number', align: 'end', sortable: true },
  ];
  const decisionColumns: TableColumn<LeaveApplication>[] = [
    {
      key: 'approverId',
      header: 'Decided by',
      hideBelow: 'lg',
      value: (a) => (a.approverId ? (people.get(a.approverId) ?? '—') : '—'),
    },
    {
      key: 'actionedOn',
      header: 'Decided on',
      type: 'date',
      sortable: true,
      hideBelow: 'md',
      nowrap: true,
    },
    { key: 'approverRemarks', header: 'Remarks', hideBelow: 'lg' },
  ];
  const statusColumn: TableColumn<LeaveApplication> = {
    key: 'status',
    header: 'Status',
    type: 'status',
    sortable: true,
  };

  const detailFields = [
    { key: 'applicationNo', label: 'Application no.', type: 'text' as const, detailsOnly: true },
    { key: 'status', label: 'Status', type: 'text' as const, detailsOnly: true },
    { key: 'appliedOn', label: 'Applied on', type: 'date' as const, detailsOnly: true },
    {
      key: 'approverId',
      label: 'Decided by',
      type: 'select' as const,
      detailsOnly: true,
      options: () => employeeOptions(o.employees),
    },
    { key: 'actionedOn', label: 'Decided on', type: 'date' as const, detailsOnly: true },
    {
      key: 'approverRemarks',
      label: 'Approver remarks',
      type: 'text' as const,
      detailsOnly: true,
      fullWidth: true,
    },
  ];

  const mode = o.mode;
  const common = {
    entityLabel: 'Leave application',
    service: o.service,
    hasStatus: false,
    nameOf: label,
    searchLabel: 'Search by application no. or reason',
    defaultSort: {
      active: mode === 'history' ? 'actionedOn' : 'startDate',
      direction: 'desc' as const,
    },
    dateFilter: {
      fromKey: 'dateFrom',
      toKey: 'dateTo',
      fromLabel: 'Leave from',
      toLabel: 'Leave to',
    },
    // Required by the contract; the real form is only used on the applications screen.
    newItem: () => ({
      employeeId: null,
      halfDay: false,
      startDate: todayIso(),
      endDate: todayIso(),
    }),
  };

  const formFields: CrudPageConfig<LeaveApplication>['fields'] = [
    {
      key: 'employeeId',
      label: 'Employee',
      type: 'select',
      required: true,
      fullWidth: true,
      options: () => employeeOptions(o.employees),
    },
    {
      key: 'leaveTypeId',
      label: 'Leave type',
      type: 'select',
      required: true,
      options: (v) => {
        const gender = o.employees.find((e) => e.id === v['employeeId'])?.gender;
        return options(
          o.types.filter(
            (t) =>
              t.status === 'Active' &&
              (!t.eligibleGender || !gender || t.eligibleGender === gender),
          ),
          (t) => t.name,
        );
      },
      dependsOn: 'employeeId',
      hint: 'Only types available to the employee',
    },
    { key: 'halfDay', label: 'Half day', type: 'toggle', hint: 'Single date only' },
    { key: 'startDate', label: 'Start date', type: 'date', required: true },
    { key: 'endDate', label: 'End date', type: 'date', required: true },
    {
      key: 'days',
      label: 'Number of days',
      type: 'number',
      hint: 'Calculated: weekends and holidays are excluded',
      derive: (v) => {
        const start = String(v['startDate'] ?? '');
        const end = String(v['endDate'] ?? '');
        if (!start || !end || end < start) return 0;
        return v['halfDay'] ? 0.5 : countWorkingDays(start, end, o.weekendDays, holidaySet);
      },
    },
    { key: 'reason', label: 'Reason', type: 'textarea', required: true, maxLength: 250 },
    {
      key: 'documentName',
      label: 'Supporting document',
      type: 'file',
      fullWidth: true,
      hint: 'Required before submitting for types such as Medical Leave (demo: not stored)',
    },
    ...detailFields,
  ];

  switch (mode) {
    case 'pending':
      return {
        ...common,
        title: 'Pending approvals',
        description: 'Leave requests waiting for a decision.',
        embedded: true,
        canCreate: false,
        lockedFilters: { status: 'Submitted' },
        defaultSort: { active: 'appliedOn', direction: 'asc' },
        canEdit: () => false,
        canDelete: () => false,
        extraActions: [approve, reject],
        filters: [
          { key: 'employeeId', label: 'Employee', options: employeeOptions(o.employees) },
          { key: 'leaveTypeId', label: 'Leave type', options: options(o.types, (t) => t.name) },
        ],
        columns: [
          ...baseColumns,
          {
            key: 'appliedOn',
            header: 'Applied',
            type: 'date',
            sortable: true,
            hideBelow: 'md',
            nowrap: true,
          },
          { key: 'reason', header: 'Reason', hideBelow: 'lg' },
        ],
        fields: formFields,
      };
    case 'history':
      return {
        ...common,
        title: 'Decision history',
        description: 'Approved, rejected and cancelled leave.',
        embedded: true,
        canCreate: false,
        lockedFilters: { status: ['Approved', 'Rejected', 'Cancelled'] },
        canEdit: () => false,
        canDelete: () => false,
        extraActions: [cancel],
        filters: [
          { key: 'employeeId', label: 'Employee', options: employeeOptions(o.employees) },
          { key: 'leaveTypeId', label: 'Leave type', options: options(o.types, (t) => t.name) },
          {
            key: 'status',
            label: 'Status',
            options: statusOptions.filter((s) =>
              ['Approved', 'Rejected', 'Cancelled'].includes(String(s.value)),
            ),
          },
        ],
        columns: [...baseColumns, statusColumn, ...decisionColumns],
        fields: formFields,
      };
    default:
      return {
        ...common,
        title: 'Leave applications',
        description: 'Apply for leave and track it from draft to decision.',
        canEdit: isDraft,
        canDelete: isDraft,
        groupValidators: [HrmsValidators.dateRange('startDate', 'endDate')],
        extraActions: [submit, cancel],
        filters: [
          { key: 'employeeId', label: 'Employee', options: employeeOptions(o.employees) },
          { key: 'leaveTypeId', label: 'Leave type', options: options(o.types, (t) => t.name) },
          { key: 'status', label: 'Status', options: statusOptions },
        ],
        columns: [
          ...baseColumns,
          statusColumn,
          {
            key: 'appliedOn',
            header: 'Applied',
            type: 'date',
            sortable: true,
            hideBelow: 'lg',
            nowrap: true,
          },
        ],
        fields: formFields,
      };
  }
}
