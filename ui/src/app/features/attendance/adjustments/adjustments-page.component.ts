import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  APPROVAL_STATUSES,
  AttendanceAdjustment,
  AttendanceStatus,
} from '../../../core/models/attendance.models';
import { fullName } from '../../../core/models/employee.models';
import { AttendanceAdjustmentDataService } from '../../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions } from '../../organization/organization-lookups';

const FORCEABLE: AttendanceStatus[] = ['Present', 'Late', 'Half Day', 'Absent'];

@Component({
  selector: 'app-adjustments-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading adjustments" />
    }
  `,
})
export class AdjustmentsPageComponent {
  private readonly adjustments = inject(AttendanceAdjustmentDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly employees = rxResource({ stream: () => this.employeesApi.getAll() });

  protected readonly config = computed<CrudPageConfig<AttendanceAdjustment> | null>(() => {
    const people = this.employees.value();
    if (!people) {
      return null;
    }
    const names = new Map(people.map((e) => [e.id, `${fullName(e)} (${e.employeeCode})`]));
    const pending = (a: AttendanceAdjustment) => a.status === 'Pending';
    const code = (a: AttendanceAdjustment) => `ADJ-${String(a.id).padStart(4, '0')}`;
    const range = (a: string | null, b: string | null) => `${a ?? '—'} – ${b ?? '—'}`;
    const statusOptions = APPROVAL_STATUSES.map((s) => ({ value: s, label: s }));

    return {
      title: 'Attendance adjustments',
      description:
        'Correct missed or wrong check-in / check-out times. Approval updates the attendance record.',
      entityLabel: 'Adjustment request',
      service: this.adjustments,
      hasStatus: false,
      searchLabel: 'Search by reason',
      defaultSort: { active: 'date', direction: 'desc' },
      nameOf: code,
      canEdit: pending,
      canDelete: pending,
      filters: [
        { key: 'employeeId', label: 'Employee', options: employeeOptions(people) },
        { key: 'status', label: 'Status', options: statusOptions },
      ],
      extraActions: [
        {
          id: 'approve',
          permission: 'Approve',
          label: 'Approve and apply',
          icon: 'check_circle',
          visible: pending,
          confirm: {
            title: 'Approve adjustment?',
            message: (a) =>
              `Apply the requested times to ${names.get(a.employeeId)}'s record for ${a.date}?`,
            confirmText: 'Approve',
          },
          run: (a) => this.adjustments.decide(a.id, 'Approved', ''),
          successMessage: (a) => `${code(a)} approved and applied to the attendance record.`,
        },
        {
          id: 'reject',
          permission: 'Approve',
          label: 'Reject',
          icon: 'cancel',
          destructive: true,
          visible: pending,
          prompt: {
            title: 'Reject adjustment?',
            message: (a) =>
              `Reject the request for ${names.get(a.employeeId)} on ${a.date}? The record stays unchanged.`,
            label: 'Reason for rejection',
            required: true,
            confirmText: 'Reject',
          },
          run: (a, remarks) => this.adjustments.decide(a.id, 'Rejected', remarks),
          successMessage: (a) => `${code(a)} rejected.`,
        },
      ],
      columns: [
        { key: 'date', header: 'Date', type: 'date', sortable: true, nowrap: true },
        {
          key: 'employeeId',
          header: 'Employee',
          value: (a) => names.get(a.employeeId) ?? `#${a.employeeId}`,
        },
        {
          key: 'currentCheckIn',
          header: 'Recorded',
          nowrap: true,
          hideBelow: 'sm',
          value: (a) => range(a.currentCheckIn, a.currentCheckOut),
        },
        {
          key: 'requestedCheckIn',
          header: 'Requested',
          nowrap: true,
          hideBelow: 'sm',
          value: (a) =>
            `${a.requestedCheckIn ?? a.currentCheckIn ?? '—'} – ${a.requestedCheckOut ?? a.currentCheckOut ?? '—'}${a.requestedStatus ? ` · ${a.requestedStatus}` : ''}`,
        },
        { key: 'reason', header: 'Reason', hideBelow: 'lg' },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ date: todayIso() }),
      fixedValues: {
        recordId: null,
        currentCheckIn: null,
        currentCheckOut: null,
        status: 'Pending',
        requestedOn: '',
        decidedBy: null,
        decidedOn: null,
        remarks: '',
      },
      fields: [
        {
          key: 'employeeId',
          label: 'Employee',
          type: 'select',
          required: true,
          fullWidth: true,
          options: () => employeeOptions(people),
        },
        {
          key: 'date',
          label: 'Date to correct',
          type: 'date',
          required: true,
          notInFuture: true,
          hint: 'Must have an attendance record',
        },
        {
          key: 'requestedStatus',
          label: 'Set status (optional)',
          type: 'select',
          nullable: true,
          options: () => FORCEABLE.map((s) => ({ value: s, label: s })),
        },
        {
          key: 'requestedCheckIn',
          label: 'Correct check-in',
          type: 'time',
          hint: 'Leave empty to keep the recorded time',
        },
        {
          key: 'requestedCheckOut',
          label: 'Correct check-out',
          type: 'time',
          hint: 'Leave empty to keep the recorded time',
        },
        { key: 'reason', label: 'Reason', type: 'textarea', required: true, maxLength: 250 },
        { key: 'status', label: 'Status', type: 'text', detailsOnly: true },
        { key: 'decidedBy', label: 'Decided by', type: 'text', detailsOnly: true },
        { key: 'remarks', label: 'Approver remarks', type: 'text', detailsOnly: true },
      ],
    };
  });
}
