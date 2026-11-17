import { Component, inject } from '@angular/core';
import { KpiCategoryDataService } from '../../../core/services/data/kpi-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { buildKpiCategoriesConfig } from './kpi-categories.config';

@Component({
  selector: 'app-kpi-categories-page',
  imports: [CrudPageComponent],
  template: `<app-crud-page [config]="config" />`,
})
export class KpiCategoriesPageComponent {
  protected readonly config = buildKpiCategoriesConfig(inject(KpiCategoryDataService));
}
