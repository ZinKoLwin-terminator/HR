export interface Weekday {
  /** JavaScript weekday number: 0 = Sunday … 6 = Saturday. */
  value: number;
  label: string;
  short: string;
}

/** Monday-first, as used by work schedules. */
export const WEEKDAYS: readonly Weekday[] = [
  { value: 1, label: 'Monday', short: 'Mon' },
  { value: 2, label: 'Tuesday', short: 'Tue' },
  { value: 3, label: 'Wednesday', short: 'Wed' },
  { value: 4, label: 'Thursday', short: 'Thu' },
  { value: 5, label: 'Friday', short: 'Fri' },
  { value: 6, label: 'Saturday', short: 'Sat' },
  { value: 0, label: 'Sunday', short: 'Sun' },
];

/** "Mon–Fri", "Mon–Sat" or a comma list such as "Mon, Wed, Fri". */
export function describeWorkDays(days: readonly number[]): string {
  const ordered = WEEKDAYS.filter((d) => days.includes(d.value));
  if (!ordered.length) {
    return '—';
  }
  const contiguous = ordered.every(
    (d, i) => i === 0 || WEEKDAYS.indexOf(d) === WEEKDAYS.indexOf(ordered[i - 1]) + 1,
  );
  return contiguous && ordered.length > 2
    ? `${ordered[0].short}–${ordered[ordered.length - 1].short}`
    : ordered.map((d) => d.short).join(', ');
}
