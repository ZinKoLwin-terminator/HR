import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  PromptDialogComponent,
  PromptDialogData,
} from '../../shared/components/prompt-dialog/prompt-dialog.component';

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialog = inject(MatDialog);

  /** Emits `true` only when the user explicitly confirms. */
  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data,
        width: '440px',
        maxWidth: '95vw',
        autoFocus: 'dialog',
        role: data.destructive ? 'alertdialog' : 'dialog',
      })
      .afterClosed()
      .pipe(map((result) => result === true));
  }

  /** Asks for a piece of text. Emits the trimmed text, or `null` when cancelled. */
  prompt(data: PromptDialogData): Observable<string | null> {
    return this.dialog
      .open<PromptDialogComponent, PromptDialogData, string | null>(PromptDialogComponent, {
        data,
        width: '480px',
        maxWidth: '95vw',
        role: data.destructive ? 'alertdialog' : 'dialog',
      })
      .afterClosed()
      .pipe(map((result) => result ?? null));
  }

  /** Convenience for delete confirmations. */
  confirmDelete(entityLabel: string, name: string): Observable<boolean> {
    return this.confirm({
      title: `Delete ${entityLabel.toLowerCase()}?`,
      message: `"${name}" will be permanently removed. This cannot be undone.`,
      confirmText: 'Delete',
      destructive: true,
    });
  }
}
