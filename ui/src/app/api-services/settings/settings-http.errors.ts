import { HttpErrorResponse } from '@angular/common/http';
import { AppError, isAppError } from '../../core/models/common.models';
import { mapHttpError } from '../http-error.mapper';

export const ADMIN_ONLY_MESSAGE = 'Only HR Administrators can manage users and roles.';
export const ADMIN_ONLY_GENERAL_MESSAGE = 'Only HR Administrators can change general settings.';
export const EMAIL_NOT_CONFIGURED_MESSAGE = 'Email is not configured.';

/**
 * Maps a failed settings call to an `AppError`. 403 becomes a plain "administrators only"
 * message (these routes are admin-only) and everything else keeps the shared mapping, so 409
 * keeps the server's text or the reload message and field errors reach the form.
 * Nothing here reads or keeps a request body, so a password can never end up in an error.
 */
export function settingsError(error: unknown, forbiddenMessage = ADMIN_ONLY_MESSAGE): AppError {
  if (isAppError(error)) {
    return error;
  }
  const mapped = mapHttpError(error);
  return mapped.code === 'Forbidden' ? { ...mapped, message: forbiddenMessage } : mapped;
}

/** Password reset: 503 means the server has no mail settings. */
export function passwordResetError(error: unknown): AppError {
  if (error instanceof HttpErrorResponse && error.status === 503) {
    return { code: 'Unknown', message: EMAIL_NOT_CONFIGURED_MESSAGE };
  }
  return settingsError(error);
}
