import { formatDate } from '@angular/common';

/**
 * Display formatters driven by the general settings. They only change how a value is SHOWN; stored
 * and transmitted dates stay ISO `yyyy-MM-dd` and amounts stay plain numbers.
 */

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;
const moneyFormatters = new Map<string, Intl.NumberFormat>();
const zoneFormatters = new Map<string, Intl.DateTimeFormat>();
const zoneValidity = new Map<string, boolean>();

/** True when the runtime knows the IANA time zone. Cached: the impure date pipe calls it often. */
export function isValidTimeZone(zone: string): boolean {
  let valid = zoneValidity.get(zone);
  if (valid === undefined) {
    try {
      new Intl.DateTimeFormat('en-US', { timeZone: zone });
      valid = true;
    } catch {
      valid = false;
    }
    zoneValidity.set(zone, valid);
  }
  return valid;
}

/** The calendar date (`yyyy-MM-dd`) of an instant as seen in the given time zone. */
export function isoDateInZone(instant: Date, zone: string): string {
  let formatter = zoneFormatters.get(zone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
    zoneFormatters.set(zone, formatter);
  }
  const parts = Object.fromEntries(formatter.formatToParts(instant).map((p) => [p.type, p.value]));
  return `${parts['year']}-${parts['month']}-${parts['day']}`;
}

/**
 * Formats a date with one of the `DATE_FORMATS` patterns. A plain `yyyy-MM-dd` value is formatted as
 * is (no time zone shift); an instant (timestamp or `Date`) is first moved to `timeZone`.
 * Returns an empty string for an unusable value.
 */
export function formatDisplayDate(
  value: string | number | Date | null | undefined,
  format: string,
  timeZone?: string | null,
): string {
  if (value === null || value === undefined || value === '') return '';
  let iso: string;
  if (typeof value === 'string' && ISO_DATE.test(value)) {
    iso = value;
  } else {
    const instant = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(instant.getTime())) return '';
    if (timeZone && isValidTimeZone(timeZone)) {
      iso = isoDateInZone(instant, timeZone);
    } else {
      return formatDate(instant, format, 'en-US');
    }
  }
  const [, y, m, d] = ISO_DATE.exec(iso) ?? [];
  if (!y) return '';
  return formatDate(new Date(Number(y), Number(m) - 1, Number(d)), format, 'en-US');
}

/** Formats an amount in the given ISO 4217 currency. `—` for a missing value. */
export function formatMoney(
  value: number | null | undefined,
  currency: string,
  locale = 'en-US',
  fractionDigits = 0,
): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  const key = `${locale}|${currency}|${fractionDigits}`;
  let formatter = moneyFormatters.get(key);
  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    });
    moneyFormatters.set(key, formatter);
  }
  return formatter.format(value);
}
