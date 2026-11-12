import { Pipe, PipeTransform, inject } from '@angular/core';
import { AppSettingsService } from '../../core/services/app-settings.service';

/**
 * Shows a date in the date format and time zone of the general settings, e.g. `value | appDate`.
 * Not pure on purpose: it reads the settings signal, so screens update when the format changes.
 */
@Pipe({ name: 'appDate', pure: false })
export class AppDatePipe implements PipeTransform {
  private readonly settings = inject(AppSettingsService);

  transform(value: string | number | Date | null | undefined): string {
    return this.settings.formatDate(value);
  }
}
