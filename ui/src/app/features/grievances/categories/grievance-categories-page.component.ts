import { Component, inject } from '@angular/core';
import { GrievanceCategoryDataService } from '../../../core/services/data/grievance-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { buildGrievanceCategoriesConfig } from './grievance-categories.config';

/** Grievance categories through the shared CRUD page (keys from the route: `grievance-management`). */
@Component({
  selector: 'app-grievance-categories-page',
  imports: [CrudPageComponent],
  template: `<app-crud-page [config]="config" />`,
})
export class GrievanceCategoriesPageComponent {
  protected readonly config = buildGrievanceCategoriesConfig(inject(GrievanceCategoryDataService));
}
