import { mockError } from '../mock-response';

/**
 * Request checks shared by the grievance mock services, with the API's validation rules and status
 * codes (400 Validation, 409 Conflict). Simulation for the demo.
 */

/** Same wording as the HTTP adapters use for a stale `rowVersion`. */
export const GRIEVANCE_STALE_MESSAGE = 'The record changed. Reload and try again.';

export function validation(message: string, field: string): never {
  throw mockError('Validation', message, { [field]: message });
}

/** 400 when missing or malformed (the mock's tokens are counters). */
export function requireRowVersion(given: string | null | undefined, field = 'rowVersion'): string {
  if (!given || !/^\d+$/.test(given)) {
    validation('The row version is missing or not valid.', field);
  }
  return given;
}

/** 409 when the token is not the current one. */
export function ensureCurrent(current: string, given: string): void {
  if (given !== current) {
    throw mockError('Conflict', GRIEVANCE_STALE_MESSAGE);
  }
}

/** Trimmed text; `required` checks 1..max, else null for blank and at most max. */
export function text(
  value: string | null | undefined,
  field: string,
  label: string,
  max: number,
  required: boolean,
  min = 1,
): string | null {
  const trimmed = (value ?? '').trim();
  if (trimmed === '') {
    if (required) {
      validation(`${label} is required.`, field);
    }
    return null;
  }
  if (trimmed.length < min) {
    validation(`${label} must be at least ${min} characters.`, field);
  }
  if (trimmed.length > max) {
    validation(`${label} must be at most ${max} characters.`, field);
  }
  return trimmed;
}

export function requiredText(
  value: string | null | undefined,
  field: string,
  label: string,
  max: number,
  min = 1,
): string {
  return text(value, field, label, max, true, min)!;
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** A `yyyy-MM-dd` date (or null when allowed). */
export function isoDate(
  value: string | null | undefined,
  field: string,
  label: string,
  required: boolean,
): string | null {
  if (value === null || value === undefined || value === '') {
    if (required) {
      validation(`${label} is required.`, field);
    }
    return null;
  }
  if (!ISO_DATE.test(value) || Number.isNaN(Date.parse(value))) {
    validation(`${label} must be a date (yyyy-MM-dd).`, field);
  }
  return value;
}
