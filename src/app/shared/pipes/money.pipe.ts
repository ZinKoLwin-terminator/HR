import { Pipe, PipeTransform, inject } from '@angular/core';
import { APP_CONFIG } from '../../core/configuration/app-config';

/** Formats amounts in the configured currency/locale, e.g. `1234 | money` → "$1,234". */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  private readonly config = inject(APP_CONFIG);
  private readonly formatters = new Map<number, Intl.NumberFormat>();

  transform(value: number | null | undefined, fractionDigits = 0): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    let formatter = this.formatters.get(fractionDigits);
    if (!formatter) {
      formatter = new Intl.NumberFormat(this.config.locale, {
        style: 'currency',
        currency: this.config.currency,
        minimumFractionDigits: fractionDigits,
        maximumFractionDigits: fractionDigits,
      });
      this.formatters.set(fractionDigits, formatter);
    }
    return formatter.format(value);
  }
}
