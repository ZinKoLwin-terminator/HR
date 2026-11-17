import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { ScorecardItem } from '../../../core/models/kpi.models';
import { actualText } from './scorecard-rules';

/**
 * What a KPI on a scorecard asks for (category, direction and scoring, unit, target, weight, range
 * or rating scale) and, with `showResults`, the result: the actual value and the server's
 * achievement and weighted score. Never calculated here.
 */
@Component({
  selector: 'app-kpi-item-facts',
  imports: [DecimalPipe],
  template: `
    @let i = item();
    <dl class="facts">
      <div>
        <dt>Category</dt>
        <dd>{{ i.categoryName }}</dd>
      </div>
      <div>
        <dt>Direction · scoring</dt>
        <dd>{{ i.direction }} · {{ i.scoringMethod }}</dd>
      </div>
      <div>
        <dt>Target</dt>
        <dd>
          @if (i.measurementType === 'Boolean') {
            Achieved (yes)
          } @else if (i.rangeMin !== null && i.rangeMax !== null) {
            {{ i.rangeMin | number: '1.0-4' }} to {{ i.rangeMax | number: '1.0-4' }}
          } @else {
            {{ i.target | number: '1.0-4' }}
          }
          @if (i.unit) {
            <span class="unit">{{ i.unit }}</span>
          }
          @if (i.maxRating !== null) {
            <span class="unit">(scale 0 to {{ i.maxRating }})</span>
          }
        </dd>
      </div>
      <div>
        <dt>Weight</dt>
        <dd>{{ i.weight | number: '1.0-2' }}%</dd>
      </div>
      @if (showResults()) {
        <div>
          <dt>Actual</dt>
          <dd data-testid="item-actual">{{ actual(i) }}</dd>
        </div>
        <div>
          <dt>Achievement</dt>
          <dd data-testid="item-achievement">
            {{ i.achievementPct === null ? '—' : (i.achievementPct | number: '1.2-2') + '%' }}
          </dd>
        </div>
        <div>
          <dt>Weighted score</dt>
          <dd data-testid="item-weighted">
            {{ i.weightedScore === null ? '—' : (i.weightedScore | number: '1.2-2') }}
          </dd>
        </div>
      }
    </dl>
  `,
  styles: `
    :host {
      display: block;
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 8px 20px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-small);
    }
    dd {
      margin: 0;
      font: var(--mat-sys-body-medium);
    }
    .unit {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class KpiItemFactsComponent {
  readonly item = input.required<ScorecardItem>();
  /** Adds the actual value and the server's achievement and weighted score. */
  readonly showResults = input(false);
  protected readonly actual = actualText;
}
