import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

export type StatTone = 'primary' | 'success' | 'warning' | 'danger' | 'neutral';

/** KPI tile: label, large value and a hint on the left, a tinted round icon on the right. */
@Component({
  selector: 'app-stat-card',
  imports: [MatIconModule, RouterLink, NgTemplateOutlet],
  template: `
    <ng-template #content>
      <span class="stat__body">
        <span class="stat__label">{{ label() }}</span>
        <span class="stat__value" [title]="value()">{{ value() }}</span>
        @if (hint()) {
          <span class="stat__hint" [title]="hint()">{{ hint() }}</span>
        }
      </span>
      <span class="stat__icon" aria-hidden="true"
        ><mat-icon>{{ icon() }}</mat-icon></span
      >
    </ng-template>
    @if (link(); as href) {
      <a class="stat" [attr.data-tone]="tone()" [routerLink]="href"
        ><ng-container [ngTemplateOutlet]="content"
      /></a>
    } @else {
      <div class="stat" [attr.data-tone]="tone()">
        <ng-container [ngTemplateOutlet]="content" />
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
      container-type: inline-size;
    }
    .stat {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      height: 100%;
      box-sizing: border-box;
      padding: 14px 16px;
      border-radius: var(--app-radius-md);
      background: linear-gradient(
        110deg,
        color-mix(in srgb, var(--icon-bg) 70%, var(--app-card-bg)) 0%,
        var(--app-card-bg) 70%
      );
      box-shadow: var(--app-shadow-sm);
      color: inherit;
      text-decoration: none;
      transition:
        box-shadow 150ms ease,
        border-color 150ms ease,
        transform 150ms ease;
    }
    a.stat:hover {
      box-shadow: var(--app-shadow-md);
      transform: translateY(-1px);
    }
    .stat__body {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
      flex: 1;
    }
    .stat__label {
      font: var(--mat-sys-label-medium);
      font-weight: 600;
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .stat__value {
      // Scales down with the tile so long amounts ("MMK 709,484,766") never run into the icon.
      font-size: clamp(15px, 5.2cqi, 20px);
      font-weight: 700;
      letter-spacing: -0.02em;
      line-height: 1.25;
      color: var(--mat-sys-on-surface);
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .stat__hint {
      font: var(--mat-sys-label-small);
      font-weight: 500;
      color: var(--icon-fg);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .stat__icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      flex: none;
      border-radius: 50%;
      background: color-mix(in srgb, var(--icon-bg) 85%, var(--app-card-bg));
      color: var(--icon-fg);
      box-shadow: 0 0 0 5px color-mix(in srgb, var(--icon-bg) 35%, transparent);
    }
    .stat__icon mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    [data-tone='primary'] {
      --icon-bg: var(--mat-sys-primary-container);
      --icon-fg: var(--mat-sys-primary);
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
      --icon-fg: var(--mat-sys-error);
    }
    [data-tone='neutral'] {
      --icon-bg: var(--mat-sys-surface-container-highest);
      --icon-fg: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 479.98px) {
      .stat__icon {
        display: none;
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
