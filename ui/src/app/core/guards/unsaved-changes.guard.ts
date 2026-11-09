import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmDialogService } from '../services/confirm-dialog.service';

/** Implemented by routed components that can hold unsaved edits. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/** Asks for confirmation before leaving a screen with unsaved changes. */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() ||
  inject(ConfirmDialogService).confirm({
    title: 'Discard unsaved changes?',
    message: 'You have changes that have not been saved. If you leave now they will be lost.',
    confirmText: 'Discard changes',
    cancelText: 'Keep editing',
    destructive: true,
  });
