import { Component, input } from '@angular/core';
import { ImportPreview } from '../../../core/models/excel.models';
import { StatCardComponent } from '../stat-card/stat-card.component';

/** Summary cards for a validated import: total, valid, invalid, duplicate and warning rows. */
@Component({
  selector: 'app-excel-validation-summary',
  imports: [StatCardComponent],
  template: `
    <section class="cards" aria-label="Validation summary">
      <app-stat-card
        label="Total rows"
        [value]="preview().totalRows"
        icon="table_rows"
        tone="primary"
      />
      <app-stat-card
        label="Ready to import"
        [value]="preview().validRows"
        icon="check_circle"
        tone="success"
        hint="Valid or valid with warnings"
      />
      <app-stat-card
        label="Invalid rows"
        [value]="preview().invalidRows"
        icon="error"
        tone="danger"
      />
      <app-stat-card
        label="Duplicate rows"
        [value]="preview().duplicateRows"
        icon="content_copy"
        tone="warning"
      />
      <app-stat-card
        label="Warnings"
        [value]="preview().warningRows"
        icon="warning"
        tone="warning"
      />
    </section>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 12px;
      margin-bottom: 16px;
    }
    @media (max-width: 1199.98px) {
      .cards {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }
    }
    @media (max-width: 599.98px) {
      .cards {
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 8px;
      }
    }
  `,
})
export class ExcelValidationSummaryComponent {
  readonly preview = input.required<ImportPreview>();
}
