import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EvaluationPeriod } from '../../../core/models/kpi.models';
import { EvaluationPeriodDataService } from '../../../core/services/data/kpi-data.services';
import { CompanyDataService } from '../../../core/services/data/organization-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { buildEvaluationPeriodsConfig } from './evaluation-periods.config';

@Component({
  selector: 'app-evaluation-periods-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading evaluation periods" />
    }
  `,
})
export class EvaluationPeriodsPageComponent {
  private readonly periods = inject(EvaluationPeriodDataService);
  private readonly companiesApi = inject(CompanyDataService);
  private readonly companies = rxResource({ stream: () => this.companiesApi.getAll() });

  protected readonly config = computed<CrudPageConfig<EvaluationPeriod> | null>(() => {
    const companies = this.companies.value();
    return companies ? buildEvaluationPeriodsConfig({ service: this.periods, companies }) : null;
  });
}
