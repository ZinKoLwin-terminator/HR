import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { Department } from '../../../core/models/organization.models';
import { DepartmentDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import {
  OrgLookups,
  STATUS_OPTIONS,
  employeeOptions,
  nameMap,
  options,
} from '../organization-lookups';
import { OrganizationPageBase } from '../organization-page.component';

@Component({
  selector: 'app-departments-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class DepartmentsPageComponent extends OrganizationPageBase<Department> {
  private readonly service = inject(DepartmentDataService);

  protected buildConfig(l: OrgLookups): CrudPageConfig<Department> {
    const companyName = nameMap(l.companies, (c) => c.name);
    const branchName = nameMap(l.branches, (b) => b.name);
    const personName = nameMap(l.employees, (e) => `${e.firstName} ${e.lastName}`);
    const companyOptions = options(l.companies, (c) => c.name);
    const people = employeeOptions(l.employees);
    return {
      title: 'Departments',
      excel: { moduleCode: 'departments' },
      description: 'Functional departments within each branch.',
      entityLabel: 'Department',
      service: this.service,
      searchLabel: 'Search by code, name or cost center',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [
        { key: 'companyId', label: 'Company', options: companyOptions },
        { key: 'branchId', label: 'Branch', options: options(l.branches, (b) => b.name) },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (d) => d.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '90px' },
        { key: 'name', header: 'Department', sortable: true },
        {
          key: 'branchId',
          header: 'Branch',
          hideBelow: 'sm',
          value: (d) => branchName(d.branchId),
        },
        {
          key: 'companyId',
          header: 'Company',
          hideBelow: 'lg',
          value: (d) => companyName(d.companyId),
        },
        {
          key: 'headEmployeeId',
          header: 'Head',
          hideBelow: 'md',
          value: (d) => personName(d.headEmployeeId),
        },
        { key: 'costCenter', header: 'Cost center', sortable: true, hideBelow: 'lg' },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ status: 'Active' }),
      fields: [
        {
          key: 'companyId',
          label: 'Company',
          type: 'select',
          required: true,
          options: () => companyOptions,
        },
        {
          key: 'branchId',
          label: 'Branch',
          type: 'select',
          required: true,
          dependsOn: 'companyId',
          hint: 'Only branches of the selected company',
          options: (values) =>
            options(
              l.branches.filter((b) => b.companyId === values['companyId']),
              (b) => b.name,
            ),
        },
        {
          key: 'code',
          label: 'Department code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 10,
          lockedOnEdit: true,
        },
        { key: 'name', label: 'Department name', type: 'text', required: true, maxLength: 100 },
        {
          key: 'headEmployeeId',
          label: 'Department head',
          type: 'select',
          nullable: true,
          options: () => people,
        },
        { key: 'costCenter', label: 'Cost center', type: 'text', maxLength: 20 },
        { key: 'description', label: 'Description', type: 'textarea', maxLength: 300 },
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
