import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { KpiTemplate } from '../../../core/models/kpi.models';
import {
  KpiCategoryDataService,
  KpiTemplateDataService,
} from '../../../core/services/data/kpi-data.services';
import { DepartmentDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { buildKpiTemplatesConfig } from './kpi-templates.config';

@Component({
  selector: 'app-kpi-templates-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading KPI templates" />
    }
  `,
})
export class KpiTemplatesPageComponent {
  private readonly templates = inject(KpiTemplateDataService);
  private readonly categoriesApi = inject(KpiCategoryDataService);
  private readonly departmentsApi = inject(DepartmentDataService);
  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        categories: this.categoriesApi.getAll(),
        departments: this.departmentsApi.getAll(),
      }),
  });

  protected readonly config = computed<CrudPageConfig<KpiTemplate> | null>(() => {
    const l = this.lookups.value();
    return l ? buildKpiTemplatesConfig({ service: this.templates, ...l }) : null;
  });
}
