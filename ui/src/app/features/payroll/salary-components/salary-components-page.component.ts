import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SalaryComponent } from '../../../core/models/payroll.models';
import {
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../../core/services/data/payroll-data.services';
import { describeComponent } from '../../../core/utilities/payroll-calc';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

const TYPE_OPTIONS: FieldOption[] = [
  { value: 'Earning', label: 'Earning (allowance)' },
  { value: 'Deduction', label: 'Deduction' },
];
const CALCULATION_OPTIONS: FieldOption[] = [
  { value: 'Fixed', label: 'Fixed amount' },
  { value: 'PercentOfBasic', label: '% of basic salary' },
  { value: 'PercentOfGross', label: '% of gross salary' },
];

/** Allowances and deductions that salary structures are built from (sample values). */
@Component({
  selector: 'app-salary-components-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading salary components" />
    }
  `,
})
export class SalaryComponentsPageComponent {
  private readonly components = inject(SalaryComponentDataService);
  private readonly structuresApi = inject(SalaryStructureDataService);
  private readonly structures = rxResource({ stream: () => this.structuresApi.getAll() });

  protected readonly config = computed<CrudPageConfig<SalaryComponent> | null>(() => {
    const structures = this.structures.value();
    if (!structures) {
      return null;
    }
    const usedBy = (c: SalaryComponent) =>
      structures.filter((s) => s.componentIds.includes(c.id)).length;
    return {
      title: 'Salary components',
      description:
        'Allowances and deductions used to build salary structures. All amounts are sample values.',
      entityLabel: 'Salary component',
      service: this.components,
      // The payroll module has no Delete permission: the server needs payroll:Edit to delete.
      permissionKeys: { Delete: 'payroll:Edit' },
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'type', direction: 'asc' },
      filters: [
        {
          key: 'type',
          label: 'Type',
          options: TYPE_OPTIONS.map((o) => ({ value: o.value, label: String(o.value) })),
        },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (c) => c.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '120px' },
        { key: 'name', header: 'Component', sortable: true },
        { key: 'type', header: 'Type', sortable: true },
        { key: 'calculation', header: 'Calculation', value: (c) => describeComponent(c) },
        {
          key: 'structures',
          header: 'Used in',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          value: usedBy,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ type: 'Earning', calculation: 'Fixed', status: 'Active' }),
      fields: [
        {
          key: 'code',
          label: 'Code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 14,
          lockedOnEdit: true,
          hint: 'e.g. BONUS',
        },
        { key: 'name', label: 'Component name', type: 'text', required: true, maxLength: 60 },
        { key: 'type', label: 'Type', type: 'select', required: true, options: () => TYPE_OPTIONS },
        {
          key: 'calculation',
          label: 'Calculation',
          type: 'select',
          required: true,
          options: () => CALCULATION_OPTIONS,
        },
        {
          key: 'value',
          label: 'Amount or percentage',
          type: 'number',
          required: true,
          min: 0,
          max: 1_000_000_000,
          hint: 'An amount for fixed components, a percentage (up to 100) otherwise',
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
