import { Component, inject } from '@angular/core';
import { RatingBandDataService } from '../../../core/services/data/kpi-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { buildRatingBandsConfig } from './rating-bands.config';

@Component({
  selector: 'app-rating-bands-page',
  imports: [CrudPageComponent],
  template: `<app-crud-page [config]="config" />`,
})
export class RatingBandsPageComponent {
  protected readonly config = buildRatingBandsConfig(inject(RatingBandDataService));
}
