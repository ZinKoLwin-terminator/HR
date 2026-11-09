import { Pipe, PipeTransform } from '@angular/core';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 31_536_000],
  ['month', 2_592_000],
  ['week', 604_800],
  ['day', 86_400],
  ['hour', 3_600],
  ['minute', 60],
];

const formatter = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

/** "5 minutes ago", "yesterday" … (evaluated when the view renders). */
@Pipe({ name: 'relativeTime' })
export class RelativeTimePipe implements PipeTransform {
  transform(value: string | null | undefined, now: number = Date.now()): string {
    if (!value) {
      return '';
    }
    const seconds = Math.round((new Date(value).getTime() - now) / 1000);
    for (const [unit, size] of UNITS) {
      if (Math.abs(seconds) >= size) {
        return formatter.format(Math.round(seconds / size), unit);
      }
    }
    return 'just now';
  }
}
