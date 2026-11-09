import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { Section } from '../../../core/models/organization.models';
import { SectionDataService } from '../../../core/services/data/organization-data.services';
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
  selector: 'app-sections-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class SectionsPageComponent extends OrganizationPageBase<Section> {
  private readonly service = inject(SectionDataService);

  protected buildConfig(l: OrgLookups): CrudPageConfig<Section> {
    const departmentName = nameMap(l.departments, (d) => d.name);
    const personName = nameMap(l.employees, (e) => `${e.firstName} ${e.lastName}`);
    const departmentOptions = options(l.departments, (d) => d.name);
    return {
      title: 'Sections',
      description: 'Teams and units within departments.',
      entityLabel: 'Section',
      service: this.service,
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [
        { key: 'departmentId', label: 'Department', options: departmentOptions },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (s) => s.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '110px' },
        { key: 'name', header: 'Section', sortable: true },
        {
          key: 'departmentId',
          header: 'Department',
          hideBelow: 'sm',
          value: (s) => departmentName(s.departmentId),
        },
        {
          key: 'supervisorEmployeeId',
          header: 'Supervisor',
          hideBelow: 'md',
          value: (s) => personName(s.supervisorEmployeeId),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ status: 'Active' }),
      fields: [
        {
          key: 'departmentId',
          label: 'Department',
          type: 'select',
          required: true,
          options: () => departmentOptions,
        },
        {
          key: 'code',
          label: 'Section code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 12,
          lockedOnEdit: true,
          hint: 'e.g. HR-TA',
        },
        { key: 'name', label: 'Section name', type: 'text', required: true, maxLength: 100 },
        {
          key: 'supervisorEmployeeId',
          label: 'Supervisor',
          type: 'select',
          nullable: true,
          options: () => employeeOptions(l.employees),
        },
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
