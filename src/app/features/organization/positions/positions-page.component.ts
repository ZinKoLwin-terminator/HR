import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { Position } from '../../../core/models/organization.models';
import { PositionDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { OrgLookups, STATUS_OPTIONS, nameMap, options } from '../organization-lookups';
import { OrganizationPageBase } from '../organization-page.component';

@Component({
  selector: 'app-positions-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class PositionsPageComponent extends OrganizationPageBase<Position> {
  private readonly service = inject(PositionDataService);

  protected buildConfig(l: OrgLookups): CrudPageConfig<Position> {
    const departmentName = nameMap(l.departments, (d) => d.name);
    const gradeName = nameMap(l.grades, (g) => `${g.code} · ${g.name}`);
    const departmentOptions = options(l.departments, (d) => d.name);
    const gradeOptions = options(l.grades, (g) => `${g.code} · ${g.name}`);

    const filled = new Map<number, number>();
    for (const e of l.employees) {
      if (e.employmentStatus !== 'Resigned' && e.employmentStatus !== 'Terminated') {
        filled.set(e.positionId, (filled.get(e.positionId) ?? 0) + 1);
      }
    }

    return {
      title: 'Positions',
      description: 'Approved positions, grades and headcount.',
      entityLabel: 'Position',
      service: this.service,
      searchLabel: 'Search by code or title',
      defaultSort: { active: 'title', direction: 'asc' },
      filters: [
        { key: 'departmentId', label: 'Department', options: departmentOptions },
        { key: 'jobGradeId', label: 'Job grade', options: gradeOptions },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (p) => p.title,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '110px' },
        { key: 'title', header: 'Position', sortable: true },
        {
          key: 'departmentId',
          header: 'Department',
          hideBelow: 'sm',
          value: (p) => departmentName(p.departmentId),
        },
        {
          key: 'jobGradeId',
          header: 'Grade',
          hideBelow: 'md',
          value: (p) => gradeName(p.jobGradeId),
        },
        {
          key: 'headcount',
          header: 'Approved',
          type: 'number',
          sortable: true,
          align: 'end',
          hideBelow: 'md',
        },
        {
          key: 'filled',
          header: 'Filled',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          value: (p) => filled.get(p.id) ?? 0,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ headcount: 1, status: 'Active' }),
      fields: [
        {
          key: 'departmentId',
          label: 'Department',
          type: 'select',
          required: true,
          options: () => departmentOptions,
        },
        {
          key: 'sectionId',
          label: 'Section',
          type: 'select',
          nullable: true,
          dependsOn: 'departmentId',
          hint: 'Only sections of the selected department',
          options: (values) =>
            options(
              l.sections.filter((s) => s.departmentId === values['departmentId']),
              (s) => s.name,
            ),
        },
        {
          key: 'code',
          label: 'Position code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 12,
          lockedOnEdit: true,
          hint: 'e.g. POS-034',
        },
        { key: 'title', label: 'Position title', type: 'text', required: true, maxLength: 100 },
        {
          key: 'jobGradeId',
          label: 'Job grade',
          type: 'select',
          required: true,
          options: () => gradeOptions,
        },
        {
          key: 'headcount',
          label: 'Approved headcount',
          type: 'number',
          required: true,
          min: 1,
          max: 500,
        },
        {
          key: 'reportsToPositionId',
          label: 'Reports to',
          type: 'select',
          nullable: true,
          options: (_values, { id }) =>
            options(
              l.positions.filter((p) => p.id !== id),
              (p) => `${p.title} (${departmentName(p.departmentId)})`,
            ),
        },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        { key: 'description', label: 'Description', type: 'textarea', maxLength: 300 },
      ],
    };
  }
}
