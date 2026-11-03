import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { JobGrade } from '../../../core/models/organization.models';
import { JobGradeDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { HrmsValidators } from '../../../shared/validators/hrms-validators';
import { STATUS_OPTIONS } from '../organization-lookups';
import { OrganizationPageBase } from '../organization-page.component';

@Component({
  selector: 'app-job-grades-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class JobGradesPageComponent extends OrganizationPageBase<JobGrade> {
  private readonly service = inject(JobGradeDataService);

  protected buildConfig(): CrudPageConfig<JobGrade> {
    return {
      title: 'Job grades',
      description: 'Grade levels and their sample monthly salary bands.',
      entityLabel: 'Job grade',
      service: this.service,
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'level', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (g) => `${g.code} ${g.name}`,
      groupValidators: [HrmsValidators.minMax('minSalary', 'maxSalary')],
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '80px' },
        { key: 'name', header: 'Grade', sortable: true },
        { key: 'level', header: 'Level', type: 'number', sortable: true, align: 'end' },
        {
          key: 'minSalary',
          header: 'Min salary',
          type: 'currency',
          sortable: true,
          align: 'end',
          hideBelow: 'sm',
        },
        {
          key: 'maxSalary',
          header: 'Max salary',
          type: 'currency',
          sortable: true,
          align: 'end',
          hideBelow: 'sm',
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ level: 1, status: 'Active' }),
      fields: [
        {
          key: 'code',
          label: 'Grade code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 6,
          lockedOnEdit: true,
          hint: 'e.g. G10',
        },
        { key: 'name', label: 'Grade name', type: 'text', required: true, maxLength: 60 },
        {
          key: 'level',
          label: 'Level',
          type: 'number',
          required: true,
          min: 1,
          max: 20,
          hint: '1 = most junior',
        },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        {
          key: 'minSalary',
          label: 'Minimum monthly salary (sample)',
          type: 'number',
          required: true,
          min: 0,
          max: 1_000_000,
        },
        {
          key: 'maxSalary',
          label: 'Maximum monthly salary (sample)',
          type: 'number',
          required: true,
          min: 0,
          max: 1_000_000,
        },
        { key: 'description', label: 'Description', type: 'textarea', maxLength: 300 },
      ],
    };
  }
}
