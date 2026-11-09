import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { EMPLOYMENT_TYPES } from '../../../core/models/employee.models';
import { LeavePolicy } from '../../../core/models/leave.models';
import {
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../../../core/services/data/leave-data.services';
import { JobGradeDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS, nameMap, options } from '../../organization/organization-lookups';

const ACCRUAL_OPTIONS: FieldOption[] = [
  { value: 'Upfront', label: 'Full amount at the start of the year' },
  { value: 'Monthly', label: 'Accrues monthly' },
];

/**
 * Leave entitlement policies. The most specific active policy for an employee's grade and
 * employment type decides their yearly days (this is the "leave entitlement" setup).
 */
@Component({
  selector: 'app-leave-policies-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading leave policies" />
    }
  `,
})
export class LeavePoliciesPageComponent {
  private readonly policies = inject(LeavePolicyDataService);
  private readonly typesApi = inject(LeaveTypeDataService);
  private readonly gradesApi = inject(JobGradeDataService);
  private readonly lookups = rxResource({
    stream: () => forkJoin({ types: this.typesApi.getAll(), grades: this.gradesApi.getAll() }),
  });

  protected readonly config = computed<CrudPageConfig<LeavePolicy> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const typeName = nameMap(l.types, (t) => t.name);
    const gradeName = nameMap(l.grades, (g) => `${g.code} · ${g.name}`);
    const typeOptions = options(l.types, (t) => t.name);
    const gradeOptions = options(l.grades, (g) => `${g.code} · ${g.name}`);
    const employmentOptions: FieldOption[] = EMPLOYMENT_TYPES.map((t) => ({ value: t, label: t }));

    return {
      title: 'Leave policies',
      description:
        "Entitlement rules by job grade and employment type. The most specific active policy decides an employee's yearly days.",
      entityLabel: 'Leave policy',
      service: this.policies,
      searchLabel: 'Search by policy name',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [
        { key: 'leaveTypeId', label: 'Leave type', options: typeOptions },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (p) => p.name,
      columns: [
        { key: 'name', header: 'Policy', sortable: true },
        {
          key: 'leaveTypeId',
          header: 'Leave type',
          hideBelow: 'sm',
          value: (p) => typeName(p.leaveTypeId),
        },
        {
          key: 'jobGradeId',
          header: 'Job grade',
          hideBelow: 'md',
          value: (p) => (p.jobGradeId ? gradeName(p.jobGradeId) : 'All grades'),
        },
        {
          key: 'employmentType',
          header: 'Employment type',
          hideBelow: 'lg',
          value: (p) => p.employmentType ?? 'All types',
        },
        { key: 'daysPerYear', header: 'Days / year', type: 'number', align: 'end', sortable: true },
        { key: 'accrual', header: 'Accrual', hideBelow: 'lg', sortable: true },
        {
          key: 'minServiceMonths',
          header: 'Min. service',
          align: 'end',
          hideBelow: 'md',
          sortable: true,
          value: (p) => (p.minServiceMonths ? `${p.minServiceMonths} mo` : 'None'),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ accrual: 'Upfront', minServiceMonths: 0, status: 'Active' }),
      fields: [
        {
          key: 'name',
          label: 'Policy name',
          type: 'text',
          required: true,
          maxLength: 80,
          fullWidth: true,
        },
        {
          key: 'leaveTypeId',
          label: 'Leave type',
          type: 'select',
          required: true,
          options: () => typeOptions,
        },
        {
          key: 'daysPerYear',
          label: 'Days per year',
          type: 'number',
          required: true,
          min: 0,
          max: 365,
        },
        {
          key: 'jobGradeId',
          label: 'Job grade',
          type: 'select',
          nullable: true,
          options: () => gradeOptions,
          hint: 'Empty = all grades',
        },
        {
          key: 'employmentType',
          label: 'Employment type',
          type: 'select',
          nullable: true,
          options: () => employmentOptions,
          hint: 'Empty = all types',
        },
        {
          key: 'accrual',
          label: 'Accrual',
          type: 'select',
          required: true,
          options: () => ACCRUAL_OPTIONS,
        },
        {
          key: 'minServiceMonths',
          label: 'Minimum service (months)',
          type: 'number',
          required: true,
          min: 0,
          max: 120,
          hint: 'Not entitled before this much service',
        },
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
