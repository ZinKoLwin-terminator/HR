import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { buildDocumentsConfig } from './employee-documents.config';
import { safeValue } from '../../../shared/utilities/resource-value';

/** All employees' documents with filters, upload and delete. */
@Component({
  selector: 'app-employee-documents-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading documents" />
    }
  `,
})
export class EmployeeDocumentsPageComponent {
  private readonly documents = inject(EmployeeDocumentDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  /**
   * Names and the employee picker: every employee with employees:View, otherwise only what the
   * role may read (see OrganizationLookupService.people). Never fails.
   */
  private readonly employees = rxResource({ stream: () => this.lookupService.employeeRefs() });

  protected readonly config = computed(() => {
    const employees = safeValue(this.employees);
    return employees ? buildDocumentsConfig({ service: this.documents, employees }) : null;
  });
}
