import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EvaluationPeriod } from '../../../core/models/kpi.models';
import { EvaluationPeriodDataService } from '../../../core/services/data/kpi-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { buildEvaluationPeriodsConfig } from './evaluation-periods.config';
import { safeValue } from '../../../shared/utilities/resource-value';

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
  private readonly lookupService = inject(OrganizationLookupService);
  /** Company names from the organization lookup (no organization:View needed; never fails). */
  private readonly companies = rxResource({
    stream: () => this.lookupService.organization().pipe(map((o) => o.companies)),
  });

  protected readonly config = computed<CrudPageConfig<EvaluationPeriod> | null>(() => {
    const companies = safeValue(this.companies);
    return companies ? buildEvaluationPeriodsConfig({ service: this.periods, companies }) : null;
  });
}
