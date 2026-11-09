import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty" role="status">
      <span class="empty__icon" aria-hidden="true"
        ><mat-icon>{{ icon() }}</mat-icon></span
      >
      <p class="empty__title">{{ title() }}</p>
      @if (message()) {
        <p class="empty__message">{{ message() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 40px 16px;
      gap: 4px;
    }
    .empty__icon {
      display: grid;
      place-items: center;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      margin-bottom: 8px;
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    .empty__title {
      margin: 0;
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
    }
    .empty__message {
      margin: 0 0 8px;
      max-width: 420px;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('No records found');
  readonly message = input('');
}
