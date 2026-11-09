import { Component, computed, inject, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Employee } from '../../../../core/models/employee.models';
import { EmployeeDocumentDataService } from '../../../../core/services/data/employee-extras-data.services';
import { CrudPageComponent } from '../../../../shared/components/crud/crud-page.component';
import { buildDocumentsConfig } from '../../employee-documents/employee-documents.config';

/** The employee's documents, reusing the generic CRUD page scoped to one employee. */
@Component({
  selector: 'app-profile-documents-tab',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `<app-crud-page [config]="config()" />`,
})
export class ProfileDocumentsTabComponent {
  readonly employee = input.required<Employee>();
  private readonly documents = inject(EmployeeDocumentDataService);

  protected readonly config = computed(() =>
    buildDocumentsConfig({
      service: this.documents,
      employees: [this.employee()],
      employeeId: this.employee().id,
    }),
  );
}
