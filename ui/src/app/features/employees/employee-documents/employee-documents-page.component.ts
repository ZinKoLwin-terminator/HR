import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { buildDocumentsConfig } from './employee-documents.config';

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
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly employees = rxResource({ stream: () => this.employeesApi.getAll() });

  protected readonly config = computed(() => {
    const employees = this.employees.value();
    return employees ? buildDocumentsConfig({ service: this.documents, employees }) : null;
  });
}
