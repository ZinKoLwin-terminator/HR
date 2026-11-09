import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { Company } from '../../../core/models/organization.models';
import { CompanyDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../organization-lookups';
import { OrganizationPageBase } from '../organization-page.component';

@Component({
  selector: 'app-companies-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading" />
    }
  `,
})
export class CompaniesPageComponent extends OrganizationPageBase<Company> {
  private readonly service = inject(CompanyDataService);

  protected buildConfig(): CrudPageConfig<Company> {
    return {
      title: 'Companies',
      description: 'Legal entities that make up the group.',
      entityLabel: 'Company',
      service: this.service,
      searchLabel: 'Search by code, name or city',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (c) => c.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '90px' },
        { key: 'name', header: 'Company', sortable: true },
        { key: 'industry', header: 'Industry', sortable: true, hideBelow: 'md' },
        {
          key: 'city',
          header: 'Location',
          sortable: true,
          hideBelow: 'sm',
          value: (c) => `${c.city}, ${c.country}`,
        },
        { key: 'foundedDate', header: 'Founded', type: 'date', sortable: true, hideBelow: 'lg' },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ country: 'Myanmar', status: 'Active' }),
      fields: [
        {
          key: 'code',
          label: 'Company code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 10,
          lockedOnEdit: true,
          hint: 'e.g. AHT (letters, numbers, dashes)',
        },
        { key: 'name', label: 'Company name', type: 'text', required: true, maxLength: 100 },
        {
          key: 'legalName',
          label: 'Legal name',
          type: 'text',
          required: true,
          maxLength: 150,
          fullWidth: true,
        },
        { key: 'registrationNumber', label: 'Registration number', type: 'text', maxLength: 40 },
        { key: 'taxId', label: 'Tax ID', type: 'text', maxLength: 40 },
        { key: 'industry', label: 'Industry', type: 'text', required: true, maxLength: 80 },
        {
          key: 'foundedDate',
          label: 'Founded date',
          type: 'date',
          required: true,
          notInFuture: true,
        },
        { key: 'email', label: 'Email', type: 'email', required: true, maxLength: 120 },
        { key: 'phone', label: 'Phone', type: 'tel', required: true },
        { key: 'website', label: 'Website', type: 'url', maxLength: 150, fullWidth: true },
        { key: 'address', label: 'Address', type: 'text', maxLength: 200, fullWidth: true },
        { key: 'city', label: 'City', type: 'text', required: true, maxLength: 60 },
        { key: 'country', label: 'Country', type: 'text', required: true, maxLength: 60 },
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
