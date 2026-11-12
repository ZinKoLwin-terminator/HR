import { Pipe, PipeTransform, inject } from '@angular/core';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AppSettingsService } from '../../core/services/app-settings.service';
import { formatMoney } from '../utilities/format.util';

/**
 * Formats amounts in the currency of the general settings, e.g. `1234 | money` → "MMK 1,234".
 * Not pure on purpose: it reads the settings signal, so screens update when the currency changes.
 */
@Pipe({ name: 'money', pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly config = inject(APP_CONFIG);
  private readonly settings = inject(AppSettingsService);

  transform(value: number | null | undefined, fractionDigits = 0): string {
    return formatMoney(value, this.settings.currencyCode(), this.config.locale, fractionDigits);
  }
}
