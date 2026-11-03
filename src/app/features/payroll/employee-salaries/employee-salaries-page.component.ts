import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { fullName } from '../../../core/models/employee.models';
import { SalaryAssignment } from '../../../core/models/payroll.models';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  SalaryAssignmentDataService,
  SalaryStructureDataService,
} from '../../../core/services/data/payroll-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions, options } from '../../organization/organization-lookups';

/**
 * Salary assignments. A new row with a later effective date revises an employee's salary;
 * the row in force today is the "Current" one. Basic salary must stay inside the job-grade band.
 */
@Component({
  selector: 'app-employee-salaries-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading employee salaries" />
    }
  `,
})
export class EmployeeSalariesPageComponent {
  private readonly assignmentsApi = inject(SalaryAssignmentDataService);
  private readonly structuresApi = inject(SalaryStructureDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        employees: this.employeesApi.getAll(),
        structures: this.structuresApi.getAll(),
        assignments: this.assignmentsApi.getAll(),
      }),
  });

  protected readonly config = computed<CrudPageConfig<SalaryAssignment> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const today = todayIso();
    const people = new Map(l.employees.map((e) => [e.id, `${fullName(e)} (${e.employeeCode})`]));
    const structureName = new Map(l.structures.map((s) => [s.id, s.name]));
    // The assignment in force today, per employee.
    const currentId = new Map<number, number>();
    const bestFrom = new Map<number, string>();
    for (const a of l.assignments) {
      if (a.effectiveFrom <= today && a.effectiveFrom > (bestFrom.get(a.employeeId) ?? '')) {
        bestFrom.set(a.employeeId, a.effectiveFrom);
        currentId.set(a.employeeId, a.id);
      }
    }
    const state = (a: SalaryAssignment) =>
      a.effectiveFrom > today
        ? 'Scheduled'
        : currentId.get(a.employeeId) === a.id
          ? 'Current'
          : 'Previous';

    return {
      title: 'Employee salaries',
      description:
        'Basic salary and salary structure per employee. Add a row with a later date to revise a salary (sample data).',
      entityLabel: 'Salary assignment',
      service: this.assignmentsApi,
      hasStatus: false,
      searchLabel: 'Search by remarks',
      defaultSort: { active: 'effectiveFrom', direction: 'desc' },
      filters: [
        { key: 'employeeId', label: 'Employee', options: employeeOptions(l.employees) },
        { key: 'structureId', label: 'Structure', options: options(l.structures, (s) => s.name) },
      ],
      nameOf: (a) => `${people.get(a.employeeId) ?? ''} from ${a.effectiveFrom}`,
      canDelete: (a) => l.assignments.filter((x) => x.employeeId === a.employeeId).length > 1,
      columns: [
        {
          key: 'employeeId',
          header: 'Employee',
          value: (a) => people.get(a.employeeId) ?? `#${a.employeeId}`,
        },
        {
          key: 'structureId',
          header: 'Structure',
          hideBelow: 'sm',
          value: (a) => structureName.get(a.structureId) ?? '—',
        },
        {
          key: 'basicSalary',
          header: 'Basic salary',
          type: 'currency',
          align: 'end',
          sortable: true,
        },
        {
          key: 'effectiveFrom',
          header: 'Effective from',
          type: 'date',
          sortable: true,
          nowrap: true,
        },
        { key: 'state', header: 'State', hideBelow: 'sm', value: state },
        { key: 'remarks', header: 'Remarks', hideBelow: 'lg' },
      ],
      newItem: () => ({ effectiveFrom: today }),
      fields: [
        {
          key: 'employeeId',
          label: 'Employee',
          type: 'select',
          required: true,
          fullWidth: true,
          lockedOnEdit: true,
          options: () => employeeOptions(l.employees),
        },
        {
          key: 'structureId',
          label: 'Salary structure',
          type: 'select',
          required: true,
          options: () =>
            options(
              l.structures.filter((s) => s.status === 'Active'),
              (s) => s.name,
            ),
        },
        {
          key: 'basicSalary',
          label: 'Monthly basic salary (sample)',
          type: 'number',
          required: true,
          min: 1,
          max: 1_000_000,
          hint: "Must stay inside the employee's job-grade band",
        },
        { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true },
        { key: 'remarks', label: 'Remarks', type: 'text', maxLength: 120 },
      ],
    };
  });
}
