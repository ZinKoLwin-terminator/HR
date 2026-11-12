import { Component, inject, input } from '@angular/core';
import { PrintService } from '../../../core/services/print.service';
import { AppDatePipe } from '../../pipes/app-date.pipe';

/**
 * Heading of a printed page: organization name, document title and the print date. Put it inside a
 * `.print-only` block; it does not render anything useful on screen.
 */
@Component({
  selector: 'app-print-header',
  imports: [AppDatePipe],
  template: `
    <header class="print-header">
      <p class="print-header__org">{{ print.organizationName() }}</p>
      <h1 class="print-header__title">{{ title() }}</h1>
      @if (subtitle()) {
        <p class="print-header__subtitle">{{ subtitle() }}</p>
      }
      <p class="print-header__date">Printed on {{ today | appDate }}</p>
    </header>
  `,
  styles: `
    .print-header {
      margin: 0 0 12px;
      padding-bottom: 8px;
      border-bottom: 2px solid #000;
    }
    .print-header p,
    .print-header h1 {
      margin: 0;
    }
    .print-header__org {
      font-size: 12px;
      font-weight: 600;
    }
    .print-header__title {
      font-size: 20px;
      font-weight: 700;
    }
    .print-header__subtitle,
    .print-header__date {
      font-size: 11px;
    }
  `,
})
export class PrintHeaderComponent {
  protected readonly print = inject(PrintService);
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  protected readonly today = new Date();
}
