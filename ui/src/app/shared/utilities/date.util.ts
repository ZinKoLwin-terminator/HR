import { isValidTimeZone, isoDateInZone } from './format.util';

/**
 * Date helpers working with ISO `yyyy-MM-dd` strings in LOCAL time.
 * Keeping dates as plain strings avoids timezone drift in forms and mock data.
 */

export function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function parseIsoDate(value: string): Date {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1);
}

let displayTimeZone: string | null = null;

/**
 * Sets the organization time zone used by {@link todayIso} (from the general settings). `null` goes
 * back to the browser's own zone. Only "today" is affected; all other helpers stay timezone-free.
 */
export function setDisplayTimeZone(zone: string | null): void {
  displayTimeZone = zone && isValidTimeZone(zone) ? zone : null;
}

export function todayIso(): string {
  return displayTimeZone ? isoDateInZone(new Date(), displayTimeZone) : toIsoDate(new Date());
}

export function addDays(value: string, days: number): string {
  const date = parseIsoDate(value);
  date.setDate(date.getDate() + days);
  return toIsoDate(date);
}

export function addMonths(value: string, months: number): string {
  const date = parseIsoDate(value);
  date.setMonth(date.getMonth() + months);
  return toIsoDate(date);
}

export function dayOfWeek(value: string): number {
  return parseIsoDate(value).getDay();
}

/** Whole days from `from` to `to` (positive when `to` is later). */
export function diffInDays(from: string, to: string): number {
  const ms = parseIsoDate(to).getTime() - parseIsoDate(from).getTime();
  return Math.round(ms / 86_400_000);
}

/** Inclusive list of ISO dates between two dates. */
export function eachDay(from: string, to: string): string[] {
  const days: string[] = [];
  for (let current = from; current <= to; current = addDays(current, 1)) {
    days.push(current);
  }
  return days;
}

/** Working days in an inclusive range, excluding weekend days and the given holidays. */
export function countWorkingDays(
  from: string,
  to: string,
  weekendDays: readonly number[],
  holidays: ReadonlySet<string> = new Set(),
): number {
  if (!from || !to || to < from) {
    return 0;
  }
  return eachDay(from, to).filter((d) => !weekendDays.includes(dayOfWeek(d)) && !holidays.has(d))
    .length;
}

export function isDateInRange(value: string, from?: string | null, to?: string | null): boolean {
  const date = value.slice(0, 10);
  return (!from || date >= from) && (!to || date <= to);
}

export function startOfMonth(value: string): string {
  return `${value.slice(0, 7)}-01`;
}

export function endOfMonth(value: string): string {
  return addDays(addMonths(startOfMonth(value), 1), -1);
}

/** Full years between a birth/joining date and a reference date. */
export function yearsBetween(from: string, to: string): number {
  const a = parseIsoDate(from);
  const b = parseIsoDate(to);
  let years = b.getFullYear() - a.getFullYear();
  if (b.getMonth() < a.getMonth() || (b.getMonth() === a.getMonth() && b.getDate() < a.getDate())) {
    years--;
  }
  return years;
}

/** Human tenure such as "3 yrs 2 mos", "5 mos" or "12 days". */
export function formatTenure(from: string, to: string): string {
  const start = parseIsoDate(from);
  const end = parseIsoDate(to);
  let months = (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth());
  if (end.getDate() < start.getDate()) {
    months--;
  }
  if (months < 1) {
    const days = Math.max(0, diffInDays(from, to));
    return `${days} day${days === 1 ? '' : 's'}`;
  }
  const years = Math.floor(months / 12);
  const rest = months % 12;
  const parts: string[] = [];
  if (years) parts.push(`${years} yr${years === 1 ? '' : 's'}`);
  if (rest) parts.push(`${rest} mo${rest === 1 ? '' : 's'}`);
  return parts.join(' ');
}

/** Minutes since midnight for an `HH:mm` string. */
export function timeToMinutes(value: string): number {
  const [h, m] = value.split(':').map(Number);
  return (h ?? 0) * 60 + (m ?? 0);
}

export function minutesToTime(totalMinutes: number): string {
  const normalized = ((totalMinutes % 1440) + 1440) % 1440;
  const h = Math.floor(normalized / 60);
  const m = normalized % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h}h ${String(m).padStart(2, '0')}m` : `${m}m`;
}
