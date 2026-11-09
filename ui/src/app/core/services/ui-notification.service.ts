import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { isAppError } from '../models/common.models';

/** User-facing toast notifications (success / error / info). */
@Injectable({ providedIn: 'root' })
export class UiNotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.open(message, 'toast-success', 3500);
  }

  info(message: string): void {
    this.open(message, 'toast-info', 3500);
  }

  error(messageOrError: unknown, fallback = 'Something went wrong. Please try again.'): void {
    this.open(errorMessage(messageOrError, fallback), 'toast-error', 6000);
  }

  private open(message: string, panelClass: string, duration: number): void {
    this.snackBar.open(message, 'Dismiss', {
      duration,
      panelClass: ['app-toast', panelClass],
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      politeness: panelClass === 'toast-error' ? 'assertive' : 'polite',
    });
  }
}

/** Extracts a readable message from an AppError, Error or string. */
export function errorMessage(
  error: unknown,
  fallback = 'Something went wrong. Please try again.',
): string {
  if (typeof error === 'string') {
    return error;
  }
  if (isAppError(error)) {
    return error.message;
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return fallback;
}
