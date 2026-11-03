import { Component, input } from '@angular/core';

/** Page title + description with a projected actions area (`[pageActions]`). */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div class="page-header__text">
        <h1 class="page-header__title">{{ title() }}</h1>
        @if (description()) {
          <p class="page-header__description">{{ description() }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content select="[pageActions]" />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 24px;
    }
    .page-header__title {
      margin: 0;
      font: var(--mat-sys-headline-small);
      font-weight: 600;
      color: var(--mat-sys-on-surface);
    }
    .page-header__description {
      margin: 4px 0 0;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .page-header__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .page-header__actions:empty {
      display: none;
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('');
}
