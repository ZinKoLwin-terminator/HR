import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { APPROVAL_STATUSES, OvertimeRecord } from '../../../core/models/attendance.models';
import { fullName } from '../../../core/models/employee.models';
import { OvertimeDataService } from '../../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions } from '../../organization/organization-lookups';

@Component({
  selector: 'app-overtime-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading overtime" />
    }
  `,
})
export class OvertimePageComponent {
  private readonly overtime = inject(OvertimeDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly employees = rxResource({ stream: () => this.employeesApi.getAll() });

  protected readonly config = computed<CrudPageConfig<OvertimeRecord> | null>(() => {
    const people = this.employees.value();
    if (!people) {
      return null;
    }
    const names = new Map(people.map((e) => [e.id, `${fullName(e)} (${e.employeeCode})`]));
    const pending = (o: OvertimeRecord) => o.status === 'Pending';
    const label = (o: OvertimeRecord) =>
      `OT-${String(o.id).padStart(4, '0')} · ${names.get(o.employeeId) ?? ''}`;
    const statusOptions = APPROVAL_STATUSES.map((s) => ({ value: s, label: s }));

    return {
      title: 'Overtime',
      description: 'Overtime worked beyond the scheduled shift. Pending entries need approval.',
      entityLabel: 'Overtime entry',
      service: this.overtime,
      hasStatus: false,
      searchLabel: 'Search by reason',
      defaultSort: { active: 'date', direction: 'desc' },
      nameOf: label,
      canEdit: pending,
      canDelete: pending,
      filters: [
        { key: 'employeeId', label: 'Employee', options: employeeOptions(people) },
        { key: 'status', label: 'Status', options: statusOptions },
      ],
      extraActions: [
        {
          id: 'approve',
          label: 'Approve',
          icon: 'check_circle',
          visible: pending,
          confirm: {
            title: 'Approve overtime?',
            message: (o) =>
              `Approve ${(o.minutes / 60).toFixed(1)} h of overtime for ${names.get(o.employeeId)} on ${o.date}?`,
            confirmText: 'Approve',
          },
          run: (o) => this.overtime.decide(o.id, 'Approved', ''),
          successMessage: (o) => `Overtime for ${names.get(o.employeeId)} approved.`,
        },
        {
          id: 'reject',
          label: 'Reject',
          icon: 'cancel',
          destructive: true,
          visible: pending,
          confirm: {
            title: 'Reject overtime?',
            message: (o) =>
              `Reject ${(o.minutes / 60).toFixed(1)} h of overtime for ${names.get(o.employeeId)} on ${o.date}?`,
            confirmText: 'Reject',
          },
          run: (o) => this.overtime.decide(o.id, 'Rejected', ''),
          successMessage: (o) => `Overtime for ${names.get(o.employeeId)} rejected.`,
        },
      ],
      columns: [
        { key: 'date', header: 'Date', type: 'date', sortable: true, nowrap: true },
        {
          key: 'employeeId',
          header: 'Employee',
          value: (o) => names.get(o.employeeId) ?? `#${o.employeeId}`,
        },
        {
          key: 'startTime',
          header: 'Time',
          nowrap: true,
          hideBelow: 'sm',
          value: (o) => `${o.startTime} – ${o.endTime}`,
        },
        {
          key: 'minutes',
          header: 'Hours',
          align: 'end',
          sortable: true,
          value: (o) => `${(o.minutes / 60).toFixed(1)} h`,
        },
        { key: 'reason', header: 'Reason', hideBelow: 'md' },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ date: todayIso(), startTime: '18:00', endTime: '20:00' }),
      // Server-filled fields; the service derives minutes and sets the workflow state.
      fixedValues: {
        minutes: 0,
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
        { key: 'date', label: 'Date', type: 'date', required: true, notInFuture: true },
        { key: 'reason', label: 'Reason', type: 'text', required: true, maxLength: 120 },
        {
          key: 'startTime',
          label: 'Overtime start',
          type: 'time',
          required: true,
          hint: 'Usually the shift end time',
        },
        { key: 'endTime', label: 'Overtime end', type: 'time', required: true },
        { key: 'status', label: 'Status', type: 'text', detailsOnly: true },
        { key: 'decidedBy', label: 'Decided by', type: 'text', detailsOnly: true },
        { key: 'remarks', label: 'Approver remarks', type: 'text', detailsOnly: true },
      ],
    };
  });
}
