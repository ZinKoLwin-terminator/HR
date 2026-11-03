import { AbstractControl } from '@angular/forms';
import { ColumnMapping, ExcelColumn } from '../../core/models/excel.models';
import { HrmsValidators } from '../../shared/validators/hrms-validators';

/** A cell as read from a worksheet. Formulas are never evaluated: only flagged. */
export interface RawCell {
  value: string | number | boolean | Date | null;
  formula: boolean;
}

export type ParsedValue = string | number | boolean | null;
export type ParsedRow = Record<string, ParsedValue>;

export interface CoercedCell {
  value: ParsedValue;
  /** The original value as text (for messages). */
  text: string;
  error: string | null;
  suggestion?: string;
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;
const LONG_DATE = /^(\d{1,2})[ -]([A-Za-z]{3})[a-z]*[ -](\d{4})$/;
const TIME = /^(\d{1,2}):(\d{2})(?::\d{2})?$/;
const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];
const EXCEL_EPOCH = Date.UTC(1899, 11, 30);

/** Headers are compared ignoring case, spaces, punctuation and "required" asterisks. */
export function normalizeHeader(value: string): string {
  return value.toLowerCase().replace(/[^\p{L}\p{N}]/gu, '');
}

export interface HeaderMatch {
  mapping: ColumnMapping;
  /** Headers (non-blank) that match no column. */
  unknownHeaders: string[];
  /** Headers that match a column that another header also matches. */
  ambiguousHeaders: string[];
  missingRequired: string[];
}

export function matchHeaders(
  headers: readonly string[],
  columns: readonly ExcelColumn[],
): HeaderMatch {
  const index = new Map<string, ExcelColumn>();
  for (const column of columns) {
    for (const name of [column.header, column.key, ...(column.aliases ?? [])]) {
      index.set(normalizeHeader(name), column);
    }
  }
  const mapping: ColumnMapping = {};
  const headersByColumn = new Map<string, string[]>();
  for (const header of headers) {
    const column = index.get(normalizeHeader(header));
    mapping[header] = column?.key ?? null;
    if (column) {
      headersByColumn.set(column.key, [...(headersByColumn.get(column.key) ?? []), header]);
    }
  }
  return {
    mapping,
    unknownHeaders: headers.filter((h) => mapping[h] === null),
    ambiguousHeaders: [...headersByColumn.values()].filter((list) => list.length > 1).flat(),
    missingRequired: columns
      .filter((c) => c.required && !headersByColumn.has(c.key))
      .map((c) => c.key),
  };
}

/** Problems with a user-edited mapping (empty list when it can be used). */
export function mappingProblems(mapping: ColumnMapping, columns: readonly ExcelColumn[]): string[] {
  const problems: string[] = [];
  const keys = new Set(columns.map((c) => c.key));
  const used = new Map<string, string>();
  for (const [header, key] of Object.entries(mapping)) {
    if (key === null) continue;
    if (!keys.has(key)) {
      problems.push(`"${header}" is mapped to an unknown field.`);
    } else if (used.has(key)) {
      const label = columns.find((c) => c.key === key)?.header ?? key;
      problems.push(`"${used.get(key)}" and "${header}" are both mapped to ${label}.`);
    } else {
      used.set(key, header);
    }
  }
  for (const column of columns.filter((c) => c.required && !used.has(c.key))) {
    problems.push(`The required column "${column.header}" is not mapped.`);
  }
  return problems;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function isoFromParts(year: number, month: number, day: number): string | null {
  const date = new Date(Date.UTC(year, month - 1, day));
  const valid =
    date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
  return valid ? `${String(year).padStart(4, '0')}-${pad(month)}-${pad(day)}` : null;
}

/** Text of a cell for messages and previews (dates as ISO). */
export function cellText(cell: RawCell): string {
  const value = cell.value;
  if (value === null) return '';
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? '' : value.toISOString().slice(0, 10);
  }
  return String(value).trim();
}

function toIsoDate(cell: RawCell): string | null {
  const value = cell.value;
  if (value instanceof Date) {
    return Number.isNaN(value.getTime())
      ? null
      : isoFromParts(value.getUTCFullYear(), value.getUTCMonth() + 1, value.getUTCDate());
  }
  if (typeof value === 'number') {
    if (!Number.isFinite(value) || value < 1 || value > 2958465) return null;
    return toIsoDate({
      value: new Date(EXCEL_EPOCH + Math.floor(value) * 86_400_000),
      formula: false,
    });
  }
  if (typeof value === 'string') {
    const text = value.trim();
    const iso = ISO_DATE.exec(text);
    if (iso) return isoFromParts(Number(iso[1]), Number(iso[2]), Number(iso[3]));
    const long = LONG_DATE.exec(text);
    if (long) {
      const month = MONTHS.indexOf(long[2].toLowerCase()) + 1;
      return month ? isoFromParts(Number(long[3]), month, Number(long[1])) : null;
    }
  }
  return null;
}

function toTime(cell: RawCell): string | null {
  const value = cell.value;
  if (value instanceof Date) {
    return Number.isNaN(value.getTime())
      ? null
      : `${pad(value.getUTCHours())}:${pad(value.getUTCMinutes())}`;
  }
  if (typeof value === 'number') {
    if (value < 0 || value >= 1) return null;
    const minutes = Math.round(value * 24 * 60);
    return `${pad(Math.floor(minutes / 60) % 24)}:${pad(minutes % 60)}`;
  }
  if (typeof value === 'string') {
    const match = TIME.exec(value.trim());
    if (match && Number(match[1]) < 24 && Number(match[2]) < 60) {
      return `${pad(Number(match[1]))}:${match[2]}`;
    }
  }
  return null;
}

function toNumber(cell: RawCell): number | null {
  const value = cell.value;
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value === 'string' && /^[+-]?\d+(\.\d+)?$/.test(value.trim()))
    return Number(value.trim());
  return null;
}

function toBoolean(cell: RawCell): boolean | null {
  const value = cell.value;
  if (typeof value === 'boolean') return value;
  const text =
    typeof value === 'number'
      ? String(value)
      : typeof value === 'string'
        ? value.trim().toLowerCase()
        : '';
  if (['yes', 'y', 'true', '1'].includes(text)) return true;
  if (['no', 'n', 'false', '0'].includes(text)) return false;
  return null;
}

function levenshtein(a: string, b: string): number {
  const row = Array.from({ length: b.length + 1 }, (_, i) => i);
  for (let i = 1; i <= a.length; i++) {
    let previous = row[0];
    row[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const temp = row[j];
      row[j] = Math.min(row[j] + 1, row[j - 1] + 1, previous + (a[i - 1] === b[j - 1] ? 0 : 1));
      previous = temp;
    }
  }
  return row[b.length];
}

/** The allowed value closest to the text (when it is close enough to be a likely typo). */
export function closestAllowed(text: string, allowed: readonly string[]): string | undefined {
  const needle = text.trim().toLowerCase();
  let best: { value: string; distance: number } | undefined;
  for (const value of allowed) {
    const distance = levenshtein(needle, value.toLowerCase());
    if (!best || distance < best.distance) best = { value, distance };
  }
  return best && best.distance <= Math.max(1, Math.floor(best.value.length / 4))
    ? best.value
    : undefined;
}

/** Converts one cell to the column's type and checks the column rules (no business rules). */
export function coerceCell(column: ExcelColumn, cell: RawCell): CoercedCell {
  const text = cellText(cell);
  const fail = (error: string, suggestion?: string): CoercedCell => ({
    value: null,
    text,
    error,
    suggestion,
  });

  if (cell.formula) {
    return fail(
      'Formulas are not allowed. Replace the formula with its value.',
      'Copy the cell and paste it as values only.',
    );
  }
  if (text === '') {
    return column.required
      ? fail(`${column.header} is required.`)
      : { value: null, text, error: null };
  }

  switch (column.type) {
    case 'text':
    case 'email':
    case 'phone': {
      if (column.maxLength && text.length > column.maxLength) {
        return fail(
          `${column.header} must be at most ${column.maxLength} characters (found ${text.length}).`,
          `Shorten it to ${column.maxLength} characters.`,
        );
      }
      if (column.type === 'email' && !EMAIL.test(text)) {
        return fail(
          `${column.header} is not a valid email address.`,
          'Use the form name@example.com.',
        );
      }
      if (column.type === 'phone' && HrmsValidators.phone({ value: text } as AbstractControl)) {
        return fail(
          `${column.header} is not a valid phone number.`,
          'Use digits, spaces or dashes with an optional leading +.',
        );
      }
      return { value: text, text, error: null };
    }
    case 'integer':
    case 'decimal': {
      const number = toNumber(cell);
      if (number === null) {
        return fail(`${column.header} must be a number.`);
      }
      if (column.type === 'integer' && !Number.isInteger(number)) {
        return fail(`${column.header} must be a whole number.`);
      }
      if (column.type === 'decimal' && column.decimals !== undefined) {
        const factor = 10 ** column.decimals;
        if (Math.abs(Math.round(number * factor) - number * factor) > 1e-6) {
          return fail(
            `${column.header} allows at most ${column.decimals} decimal places.`,
            `Round it to ${column.decimals} decimal places.`,
          );
        }
      }
      if (column.min !== undefined && number < column.min) {
        return fail(`${column.header} must be at least ${column.min}.`);
      }
      if (column.max !== undefined && number > column.max) {
        return fail(`${column.header} must be at most ${column.max}.`);
      }
      return { value: number, text, error: null };
    }
    case 'date': {
      const iso = toIsoDate(cell);
      return iso
        ? { value: iso, text, error: null }
        : fail(
            `${column.header} is not a valid date.`,
            'Use the format yyyy-MM-dd, for example 2026-03-31.',
          );
    }
    case 'time': {
      const time = toTime(cell);
      return time
        ? { value: time, text, error: null }
        : fail(
            `${column.header} is not a valid time.`,
            'Use the 24-hour format HH:mm, for example 08:30.',
          );
    }
    case 'boolean': {
      const flag = toBoolean(cell);
      return flag === null
        ? fail(`${column.header} must be Yes or No.`)
        : { value: flag, text, error: null };
    }
    case 'enum': {
      const allowed = column.allowed ?? [];
      const match = allowed.find((a) => a.toLowerCase() === text.toLowerCase());
      if (match) return { value: match, text, error: null };
      const closest = closestAllowed(text, allowed);
      return fail(
        `${column.header} "${text}" is not an allowed value.`,
        closest
          ? `Did you mean "${closest}"? Allowed: ${allowed.join(', ')}.`
          : `Allowed values: ${allowed.join(', ')}.`,
      );
    }
  }
}

/** Compares key columns of two rows (case-insensitive) — used for in-file duplicate detection. */
export function matchKeyOf(row: ParsedRow, keys: readonly string[]): string | null {
  const parts = keys.map((key) => row[key]);
  if (parts.some((p) => p === null || p === undefined || p === '')) return null;
  return parts.map((p) => String(p).trim().toLowerCase()).join('\u0001');
}

/** Cells starting with these characters can be interpreted as formulas by spreadsheet software. */
const FORMULA_START = /^[=+\-@\t\r]/;
// Numbers and phone-like text (digits with separators, no letters or operators) are not formulas.
const PLAIN_NUMBER = /^[+-]?\d[\d\s().,-]*$/;

/**
 * Makes text safe to write into a spreadsheet: values that could be run as a formula are prefixed
 * with an apostrophe so they stay plain text. Ordinary numbers are left alone.
 */
export function neutralizeFormula(text: string): string {
  return FORMULA_START.test(text) && !PLAIN_NUMBER.test(text.trim()) ? `'${text}` : text;
}
