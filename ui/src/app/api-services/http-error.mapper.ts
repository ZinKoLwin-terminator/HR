import { HttpErrorResponse } from '@angular/common/http';
import { AppError } from '../core/models/common.models';

/** Reload message used when the API reports a stale row version and gives no better text. */
export const CONFLICT_RELOAD_MESSAGE = 'The record changed. Reload and try again.';

const CODES: readonly AppError['code'][] = [
  'NotFound',
  'Validation',
  'Conflict',
  'Unauthorized',
  'Forbidden',
  'Unknown',
];

/** The application/problem+json body the API returns. Every member is optional on purpose. */
interface ProblemBody {
  detail?: unknown;
  title?: unknown;
  code?: unknown;
  errors?: unknown;
  traceId?: unknown;
}

function codeFromStatus(status: number): AppError['code'] {
  switch (status) {
    case 400:
      return 'Validation';
    case 401:
      return 'Unauthorized';
    case 403:
      return 'Forbidden';
    case 404:
      return 'NotFound';
    case 409:
      return 'Conflict';
    default:
      return 'Unknown';
  }
}

function defaultMessage(code: AppError['code']): string {
  switch (code) {
    case 'Validation':
      return 'Please correct the highlighted fields.';
    case 'NotFound':
      return 'The record no longer exists.';
    case 'Conflict':
      return CONFLICT_RELOAD_MESSAGE;
    case 'Unauthorized':
      return 'You need to sign in to continue.';
    case 'Forbidden':
      return 'You do not have permission to do that.';
    default:
      return 'Something went wrong. Please try again.';
  }
}

/** API field names may arrive as `Code`; form controls use `code`. */
function toControlName(field: string): string {
  return field.length > 0 ? field[0].toLowerCase() + field.slice(1) : field;
}

function toFieldErrors(errors: unknown): Record<string, string> | undefined {
  if (typeof errors !== 'object' || errors === null) {
    return undefined;
  }
  const result: Record<string, string> = {};
  for (const [field, messages] of Object.entries(errors)) {
    const first = Array.isArray(messages) ? messages[0] : messages;
    if (typeof first === 'string' && first.length > 0) {
      result[toControlName(field)] = first;
    }
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

/**
 * Pure mapping from an HTTP failure to the uniform `AppError` the UI already understands.
 *
 *  - `code`: the API's `code` when known, otherwise derived from the HTTP status.
 *  - `message`: the API's `detail` (then `title`), otherwise a default for the code.
 *    A 409 without a message tells the user to reload.
 *  - `fieldErrors`: first message of each entry in the API's `errors`.
 *  - `traceId`: copied through.
 *  - status 0 (no response) and anything that is not an HTTP error become `Unknown`.
 */
export function mapHttpError(error: unknown): AppError {
  if (!(error instanceof HttpErrorResponse)) {
    return { code: 'Unknown', message: defaultMessage('Unknown') };
  }
  if (error.status === 0) {
    return { code: 'Unknown', message: 'The server could not be reached. Check the connection.' };
  }

  const body: ProblemBody =
    typeof error.error === 'object' && error.error !== null ? (error.error as ProblemBody) : {};
  const apiCode = CODES.find((c) => c === body.code);
  const code = apiCode ?? codeFromStatus(error.status);
  const text = [body.detail, body.title].find((v) => typeof v === 'string' && v.trim().length > 0);

  const mapped: AppError = {
    code,
    message: typeof text === 'string' ? text : defaultMessage(code),
  };
  const fieldErrors = toFieldErrors(body.errors);
  if (fieldErrors) {
    mapped.fieldErrors = fieldErrors;
  }
  if (typeof body.traceId === 'string' && body.traceId.length > 0) {
    mapped.traceId = body.traceId;
  }
  return mapped;
}
