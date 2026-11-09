import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Destructive actions get a red confirm button and warning icon. */
  destructive?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <div class="confirm-header">
      <span class="confirm-icon" [class.danger]="data.destructive" aria-hidden="true">
        <mat-icon>{{ data.destructive ? 'warning' : 'help' }}</mat-icon>
      </span>
      <h2 mat-dialog-title>{{ data.title }}</h2>
    </div>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">{{ data.cancelText ?? 'Cancel' }}</button>
      <button mat-flat-button [class.danger-button]="data.destructive" [mat-dialog-close]="true">
        {{ data.confirmText ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .confirm-header {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 24px 24px 0;
    }
    .confirm-header h2 {
      padding: 0;
      margin: 0;
    }
    .confirm-icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      flex: none;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .confirm-icon.danger {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
