import { Injectable } from '@angular/core';
import { DateAdapter, MatDateFormats } from '@angular/material/core';
import { parseIsoDate, toIsoDate } from '../utilities/date.util';

const ISO_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];

/** Display / typing format: 04/11/2026. Day first, as used in the organisation. */
export const ISO_DATE_FORMATS: MatDateFormats = {
  parse: { dateInput: 'dd/MM/yyyy' },
  display: {
    dateInput: 'dd/MM/yyyy',
    monthYearLabel: { year: 'numeric', month: 'short' },
    dateA11yLabel: { year: 'numeric', month: 'long', day: 'numeric' },
    monthYearA11yLabel: { year: 'numeric', month: 'long' },
  },
};

/** Builds a date only when the parts form a real calendar day (rejects 31/02 etc.). */
function realDate(year: number, month: number, day: number): string | null {
  if (year < 1000 || year > 9999 || month < 1 || month > 12 || day < 1) {
    return null;
  }
  const date = new Date(year, month - 1, day);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
    ? toIsoDate(date)
    : null;
}

/**
 * Material date adapter whose date type is the app's ISO `yyyy-MM-dd` string,
 * so form controls keep the same plain-string values (no Date objects, no timezone drift).
 */
@Injectable()
export class IsoDateAdapter extends DateAdapter<string> {
  private readonly locale$ = 'en-GB';

  override getYear(date: string): number {
    return parseIsoDate(date).getFullYear();
  }
  override getMonth(date: string): number {
    return parseIsoDate(date).getMonth();
  }
  override getDate(date: string): number {
    return parseIsoDate(date).getDate();
  }
  override getDayOfWeek(date: string): number {
    return parseIsoDate(date).getDay();
  }
  override getMonthNames(style: 'long' | 'short' | 'narrow'): string[] {
    const fmt = new Intl.DateTimeFormat(this.locale$, { month: style });
    return Array.from({ length: 12 }, (_, m) => fmt.format(new Date(2000, m, 1)));
  }
  override getDateNames(): string[] {
    return Array.from({ length: 31 }, (_, i) => String(i + 1));
  }
  override getDayOfWeekNames(style: 'long' | 'short' | 'narrow'): string[] {
    const fmt = new Intl.DateTimeFormat(this.locale$, { weekday: style });
    return Array.from({ length: 7 }, (_, d) => fmt.format(new Date(2000, 0, 2 + d))); // 2 Jan 2000 = Sunday
  }
  override getYearName(date: string): string {
    return String(this.getYear(date));
  }
  override getFirstDayOfWeek(): number {
    return 1; // Monday
  }
  override getNumDaysInMonth(date: string): number {
    const d = parseIsoDate(date);
    return new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
  }
  override clone(date: string): string {
    return date;
  }
  override createDate(year: number, month: number, date: number): string {
    if (month < 0 || month > 11) {
      throw Error(`Invalid month index "${month}". Month index has to be between 0 and 11.`);
    }
    if (date < 1) {
      throw Error(`Invalid date "${date}". Date has to be greater than 0.`);
    }
    const iso = realDate(year, month + 1, date);
    if (!iso) {
      throw Error(`Invalid date "${date}" for month with index "${month}".`);
    }
    return iso;
  }
  override today(): string {
    return toIsoDate(new Date());
  }

  /** Accepts dd/MM/yyyy, d/M/yyyy, dd-MM-yyyy, dd.MM.yyyy, yyyy-MM-dd and "04 Nov 2026". */
  override parse(value: unknown): string | null {
    if (typeof value !== 'string') {
      return value == null ? null : this.invalid();
    }
    const text = value.trim();
    if (!text) {
      return null;
    }
    let match = /^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})$/.exec(text);
    if (match) {
      return realDate(+match[1], +match[2], +match[3]) ?? this.invalid();
    }
    match = /^(\d{1,2})[-/. ](\d{1,2})[-/. ](\d{4})$/.exec(text);
    if (match) {
      return realDate(+match[3], +match[2], +match[1]) ?? this.invalid();
    }
    match = /^(\d{1,2})[-/. ]([a-z]{3})[a-z]*[-/. ,]*(\d{4})$/i.exec(text);
    if (match) {
      const month = MONTHS.indexOf(match[2].toLowerCase()) + 1;
      return realDate(+match[3], month, +match[1]) ?? this.invalid();
    }
    return this.invalid();
  }
  override format(date: string): string {
    if (!this.isValid(date)) {
      throw Error('IsoDateAdapter: Cannot format invalid date.');
    }
    const [y, m, d] = date.split('-');
    return `${d}/${m}/${y}`;
  }
  override addCalendarYears(date: string, years: number): string {
    return this.addCalendarMonths(date, years * 12);
  }
  override addCalendarMonths(date: string, months: number): string {
    const d = parseIsoDate(date);
    const target = new Date(d.getFullYear(), d.getMonth() + months, 1);
    const lastDay = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
    target.setDate(Math.min(d.getDate(), lastDay));
    return toIsoDate(target);
  }
  override addCalendarDays(date: string, days: number): string {
    const d = parseIsoDate(date);
    d.setDate(d.getDate() + days);
    return toIsoDate(d);
  }
  override toIso8601(date: string): string {
    return date;
  }
  override isDateInstance(obj: unknown): boolean {
    return typeof obj === 'string' && ISO_PATTERN.test(obj);
  }
  override isValid(date: string): boolean {
    if (!ISO_PATTERN.test(date)) {
      return false;
    }
    const [y, m, d] = date.split('-').map(Number);
    return realDate(y, m, d) !== null;
  }
  override invalid(): string {
    return 'Invalid date';
  }
  override deserialize(value: unknown): string | null {
    if (value == null || value === '') {
      return null;
    }
    if (typeof value === 'string') {
      const head = value.slice(0, 10);
      return this.isValid(head) ? head : this.invalid();
    }
    return this.invalid();
  }
}
