import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { Branch, BranchType } from '../../../core/models/organization.models';
import { BranchDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import {
  OrgLookups,
  STATUS_OPTIONS,
  employeeOptions,
  nameMap,
  options,
} from '../organization-lookups';
import { OrganizationPageBase } from '../organization-page.component';

const BRANCH_TYPES: BranchType[] = ['Head Office', 'Branch', 'Regional Office', 'Warehouse'];
const TYPE_OPTIONS: FieldOption[] = BRANCH_TYPES.map((t) => ({ value: t, label: t }));

@Component({
  selector: 'app-branches-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class BranchesPageComponent extends OrganizationPageBase<Branch> {
  private readonly service = inject(BranchDataService);

  protected buildConfig(l: OrgLookups): CrudPageConfig<Branch> {
    const companyName = nameMap(l.companies, (c) => c.name);
    const companyOptions = options(l.companies, (c) => c.name);
    const people = employeeOptions(l.employees);
    const personName = nameMap(l.employees, (e) => `${e.firstName} ${e.lastName}`);
    return {
      title: 'Branches',
      excel: { moduleCode: 'branches' },
      description: 'Head offices, branches, regional offices and warehouses.',
      entityLabel: 'Branch',
      service: this.service,
      searchLabel: 'Search by code, name or city',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [
        { key: 'companyId', label: 'Company', options: companyOptions },
        { key: 'type', label: 'Type', options: TYPE_OPTIONS },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (b) => b.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '110px' },
        { key: 'name', header: 'Branch', sortable: true },
        {
          key: 'companyId',
          header: 'Company',
          sortable: true,
          hideBelow: 'md',
          value: (b) => companyName(b.companyId),
        },
        { key: 'type', header: 'Type', sortable: true, hideBelow: 'lg' },
        { key: 'city', header: 'City', sortable: true, hideBelow: 'sm' },
        {
          key: 'managerEmployeeId',
          header: 'Manager',
          hideBelow: 'lg',
          value: (b) => personName(b.managerEmployeeId),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ country: 'Myanmar', type: 'Branch', status: 'Active' }),
      fields: [
        {
          key: 'companyId',
          label: 'Company',
          type: 'select',
          required: true,
          options: () => companyOptions,
        },
        {
          key: 'code',
          label: 'Branch code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 12,
          lockedOnEdit: true,
          hint: 'e.g. YGN-HQ',
        },
        { key: 'name', label: 'Branch name', type: 'text', required: true, maxLength: 100 },
        {
          key: 'type',
          label: 'Branch type',
          type: 'select',
          required: true,
          options: () => TYPE_OPTIONS,
        },
        { key: 'email', label: 'Email', type: 'email', maxLength: 120 },
        { key: 'phone', label: 'Phone', type: 'tel' },
        { key: 'address', label: 'Address', type: 'text', maxLength: 200, fullWidth: true },
        { key: 'city', label: 'City', type: 'text', required: true, maxLength: 60 },
        { key: 'country', label: 'Country', type: 'text', required: true, maxLength: 60 },
        {
          key: 'managerEmployeeId',
          label: 'Branch manager',
          type: 'select',
          nullable: true,
          options: () => people,
        },
        {
          key: 'openedDate',
          label: 'Opened date',
          type: 'date',
          required: true,
          notInFuture: true,
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
  }
}
