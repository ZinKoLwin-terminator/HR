import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, forkJoin, map, of } from 'rxjs';
import { KpiCategory, KpiTemplate } from '../../../core/models/kpi.models';
import {
  KpiCategoryDataService,
  KpiTemplateDataService,
} from '../../../core/services/data/kpi-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { buildKpiTemplatesConfig } from './kpi-templates.config';
import { safeValue } from '../../../shared/utilities/resource-value';

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
  private readonly lookupService = inject(OrganizationLookupService);
  /**
   * Names for the dropdowns: departments from the organization lookup (no organization:View
   * needed). Each part falls back to an empty list, so the list below always renders.
   */
  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        categories: this.categoriesApi.getAll().pipe(catchError(() => of([] as KpiCategory[]))),
        departments: this.lookupService.organization().pipe(map((o) => o.departments)),
      }),
  });

  protected readonly config = computed<CrudPageConfig<KpiTemplate> | null>(() => {
    const l = safeValue(this.lookups);
    return l ? buildKpiTemplatesConfig({ service: this.templates, ...l }) : null;
  });
}
