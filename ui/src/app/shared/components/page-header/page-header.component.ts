import { Component, input } from '@angular/core';

/** Page title + description with a projected actions area (`[pageActions]`). */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div class="page-header__top">
        <div class="page-header__text">
          <h1 class="page-header__title">{{ title() }}</h1>
          @if (description()) {
            <p class="page-header__description">{{ description() }}</p>
          }
        </div>
        <div class="page-header__actions">
          <ng-content select="[pageActions]" />
        </div>
      </div>
      <div class="page-header__bar">
        <div class="page-header__search">
          <ng-content select="[pageSearch]" />
        </div>
        <div class="page-header__tools">
          <ng-content select="[pageTools]" />
        </div>
      </div>
    </header>
  `,
  styles: `
    /* One row: title | search + filter | tools (Export/Import) | primary action. */
    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--app-spacing-md);
      margin-bottom: var(--app-spacing-lg);
    }
    .page-header__top,
    .page-header__bar {
      display: contents;
    }
    /* Title takes the leftover width so the primary action stays on the right. */
    .page-header__text {
      flex: 1 1 0;
      min-width: 180px;
    }
    .page-header__title {
      margin: 0;
      font-size: 22px;
      line-height: 1.2;
      font-weight: 700;
      letter-spacing: -0.02em;
      color: var(--app-color-primary);
    }
    .page-header__description {
      margin: 2px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .page-header__actions,
    .page-header__search,
    .page-header__tools {
      display: flex;
      align-items: center;
      gap: var(--app-spacing-md);
    }
    .page-header__actions {
      flex-wrap: nowrap;
    }
    .page-header__search ::ng-deep app-search-field {
      width: 300px;
      min-width: 200px;
    }
    .page-header__text {
      order: 0;
    }
    .page-header__search {
      order: 1;
    }
    .page-header__tools {
      order: 2;
    }
    .page-header__actions {
      order: 3;
    }
    .page-header__actions:empty,
    .page-header__search:empty,
    .page-header__tools:empty {
      display: none;
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('');
}
