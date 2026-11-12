import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { SalaryStructure } from '../../../core/models/payroll.models';
import {
  SalaryAssignmentDataService,
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../../core/services/data/payroll-data.services';
import { describeComponent } from '../../../core/utilities/payroll-calc';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

/** Salary structures: named sets of components applied to employees. */
@Component({
  selector: 'app-salary-structures-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading salary structures" />
    }
  `,
})
export class SalaryStructuresPageComponent {
  private readonly structures = inject(SalaryStructureDataService);
  private readonly componentsApi = inject(SalaryComponentDataService);
  private readonly assignmentsApi = inject(SalaryAssignmentDataService);
  private readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        components: this.componentsApi.getAll(),
        assignments: this.assignmentsApi.getAll(),
      }),
  });

  protected readonly config = computed<CrudPageConfig<SalaryStructure> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const today = todayIso();
    const componentName = new Map(l.components.map((c) => [c.id, c.name]));
    // Employees whose current salary uses each structure.
    const current = new Map<number, { structureId: number; from: string }>();
    for (const a of l.assignments) {
      const existing = current.get(a.employeeId);
      if (a.effectiveFrom <= today && (!existing || a.effectiveFrom > existing.from)) {
        current.set(a.employeeId, { structureId: a.structureId, from: a.effectiveFrom });
      }
    }
    const employeesOn = (id: number) =>
      [...current.values()].filter((c) => c.structureId === id).length;

    return {
      title: 'Salary structures',
      description:
        'Sets of allowances and deductions applied to employees. All figures are sample data.',
      entityLabel: 'Salary structure',
      service: this.structures,
      // The payroll module has no Delete permission: the server needs payroll:Edit to delete.
      permissionKeys: { Delete: 'payroll:Edit' },
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (s) => s.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '110px' },
        { key: 'name', header: 'Structure', sortable: true },
        {
          key: 'componentIds',
          header: 'Components',
          hideBelow: 'md',
          value: (s) => s.componentIds.map((id) => componentName.get(id) ?? `#${id}`).join(', '),
        },
        {
          key: 'employees',
          header: 'Employees',
          type: 'number',
          align: 'end',
          hideBelow: 'sm',
          value: (s) => employeesOn(s.id),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ componentIds: [], status: 'Active' }),
      fields: [
        {
          key: 'code',
          label: 'Code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 10,
          lockedOnEdit: true,
          hint: 'e.g. SALES',
        },
        { key: 'name', label: 'Structure name', type: 'text', required: true, maxLength: 60 },
        {
          key: 'componentIds',
          label: 'Components',
          type: 'multiselect',
          required: true,
          fullWidth: true,
          hint: 'Include at least one earning',
          options: () =>
            l.components
              .filter((c) => c.status === 'Active')
              .map((c) => ({
                value: c.id,
                label: `${c.name} · ${c.type} · ${describeComponent(c)}`,
              })),
        },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        { key: 'description', label: 'Description', type: 'textarea', maxLength: 250 },
      ],
    };
  });
}
