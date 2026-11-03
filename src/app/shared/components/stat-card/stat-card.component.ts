import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

export type StatTone = 'primary' | 'success' | 'warning' | 'danger' | 'neutral';

/** KPI tile: label, large value, optional hint and link. */
@Component({
  selector: 'app-stat-card',
  imports: [MatIconModule, RouterLink, NgTemplateOutlet],
  template: `
    <ng-template #content>
      <span class="stat__icon" [attr.data-tone]="tone()" aria-hidden="true"
        ><mat-icon>{{ icon() }}</mat-icon></span
      >
      <span class="stat__body">
        <span class="stat__label">{{ label() }}</span>
        <span class="stat__value">{{ value() }}</span>
        @if (hint()) {
          <span class="stat__hint">{{ hint() }}</span>
        }
      </span>
    </ng-template>
    @if (link(); as href) {
      <a class="stat" [routerLink]="href"><ng-container [ngTemplateOutlet]="content" /></a>
    } @else {
      <div class="stat"><ng-container [ngTemplateOutlet]="content" /></div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .stat {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      height: 100%;
      box-sizing: border-box;
      padding: 18px 20px;
      border-radius: var(--app-radius-lg);
      background: var(--mat-sys-surface-container-lowest);
      border: 1px solid var(--mat-sys-outline-variant);
      color: inherit;
      text-decoration: none;
      transition:
        box-shadow 150ms ease,
        border-color 150ms ease;
    }
    a.stat:hover {
      border-color: var(--mat-sys-primary);
      box-shadow: var(--mat-sys-level2);
    }
    .stat__icon {
      display: grid;
      place-items: center;
      width: 44px;
      height: 44px;
      flex: none;
      border-radius: 12px;
      background: var(--icon-bg);
      color: var(--icon-fg);
    }
    [data-tone='primary'] {
      --icon-bg: var(--mat-sys-primary-container);
      --icon-fg: var(--mat-sys-on-primary-container);
    }
    [data-tone='success'] {
      --icon-bg: var(--app-success-container);
      --icon-fg: var(--app-on-success-container);
    }
    [data-tone='warning'] {
      --icon-bg: var(--app-warning-container);
      --icon-fg: var(--app-on-warning-container);
    }
    [data-tone='danger'] {
      --icon-bg: var(--mat-sys-error-container);
      --icon-fg: var(--mat-sys-on-error-container);
    }
    [data-tone='neutral'] {
      --icon-bg: var(--mat-sys-surface-container-highest);
      --icon-fg: var(--mat-sys-on-surface-variant);
    }
    .stat__body {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .stat__label {
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .stat__value {
      font: var(--mat-sys-headline-medium);
      font-weight: 600;
      color: var(--mat-sys-on-surface);
      font-variant-numeric: tabular-nums;
      line-height: 1.2;
      margin-top: 2px;
    }
    .stat__hint {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
      margin-top: 2px;
    }
    @media (max-width: 479.98px) {
      .stat {
        padding: 14px;
      }
      .stat__icon {
        display: none;
      }
      .stat__value {
        font-size: 24px;
      }
    }
  `,
})
export class StatCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly icon = input('insights');
  readonly hint = input('');
  readonly tone = input<StatTone>('primary');
  readonly link = input<string | null>(null);
}
