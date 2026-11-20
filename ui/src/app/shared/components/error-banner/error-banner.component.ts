import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Inline error above a list that still shows its previous rows: a reload, filter or page change
 * failed after an earlier success. Without it the old rows stay and the failure is silent.
 */
@Component({
  selector: 'app-error-banner',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="banner" role="alert">
      <mat-icon aria-hidden="true">error</mat-icon>
      <span class="banner__text">{{ message() }}</span>
      <button mat-button type="button" (click)="retry.emit()">Try again</button>
    </div>
  `,
  styles: `
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 12px;
      padding: 6px 8px 6px 14px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .banner__text {
      flex: 1;
      font: var(--mat-sys-body-medium);
    }
    .banner button {
      color: inherit;
    }
  `,
})
export class ErrorBannerComponent {
  /** The server's message (use `errorMessage()`). */
  readonly message = input.required<string>();
  /** Try again was pressed. */
  readonly retry = output<void>();
}
