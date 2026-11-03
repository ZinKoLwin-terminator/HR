import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { WEEKDAYS, describeWorkDays } from '../../../core/constants/weekdays';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { ShiftDataService } from '../../../core/services/data/attendance-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import { Shift } from '../../../core/models/attendance.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

const WEEKDAY_OPTIONS = WEEKDAYS.map((d) => ({ value: d.value, label: d.label }));

@Component({
  selector: 'app-shifts-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="employees.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading shifts" />
    }
  `,
})
export class ShiftsPageComponent {
  private readonly shifts = inject(ShiftDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  protected readonly employees = rxResource({ stream: () => this.employeesApi.getAll() });

  protected readonly config = computed<CrudPageConfig<Shift> | null>(() => {
    const people = this.employees.value();
    if (!people) {
      return null;
    }
    const assigned = new Map<number, number>();
    for (const e of people) {
      if (e.shiftId !== null) assigned.set(e.shiftId, (assigned.get(e.shiftId) ?? 0) + 1);
    }
    return {
      title: 'Shift management',
      description: 'Define working hours, breaks, grace periods and working days.',
      entityLabel: 'Shift',
      service: this.shifts,
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'startTime', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (s) => s.name,
      groupValidators: [HrmsValidators.timeOrder('startTime', 'endTime', 'isNightShift')],
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '90px' },
        { key: 'name', header: 'Shift', sortable: true },
        {
          key: 'startTime',
          header: 'Hours',
          sortable: true,
          nowrap: true,
          value: (s) => `${s.startTime} – ${s.endTime}${s.isNightShift ? ' (+1 day)' : ''}`,
        },
        {
          key: 'breakMinutes',
          header: 'Break',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          sortable: true,
        },
        {
          key: 'graceMinutes',
          header: 'Grace',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          sortable: true,
        },
        {
          key: 'workDays',
          header: 'Working days',
          hideBelow: 'sm',
          value: (s) => describeWorkDays(s.workDays),
        },
        {
          key: 'assigned',
          header: 'Employees',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          value: (s) => assigned.get(s.id) ?? 0,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({
        startTime: '09:00',
        endTime: '18:00',
        breakMinutes: 60,
        graceMinutes: 10,
        workDays: [1, 2, 3, 4, 5],
        isNightShift: false,
        color: '#3f6fd8',
        status: 'Active',
      }),
      fields: [
        {
          key: 'code',
          label: 'Shift code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 10,
          lockedOnEdit: true,
          hint: 'e.g. GEN',
        },
        { key: 'name', label: 'Shift name', type: 'text', required: true, maxLength: 60 },
        { key: 'startTime', label: 'Start time', type: 'time', required: true },
        { key: 'endTime', label: 'End time', type: 'time', required: true },
        {
          key: 'breakMinutes',
          label: 'Unpaid break (minutes)',
          type: 'number',
          required: true,
          min: 0,
          max: 240,
        },
        {
          key: 'graceMinutes',
          label: 'Late grace period (minutes)',
          type: 'number',
          required: true,
          min: 0,
          max: 120,
          hint: 'Arrivals within this time are not late',
        },
        {
          key: 'workDays',
          label: 'Working days',
          type: 'multiselect',
          required: true,
          fullWidth: true,
          options: () => WEEKDAY_OPTIONS,
        },
        { key: 'isNightShift', label: 'Night shift (ends the next day)', type: 'toggle' },
        { key: 'color', label: 'Calendar colour', type: 'color' },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
      ],
    };
  });
}
