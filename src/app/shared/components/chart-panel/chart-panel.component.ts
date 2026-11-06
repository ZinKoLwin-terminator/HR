import { Component, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Card wrapper for a chart with a Chart/Table switch. The table view is the
 * accessible twin of every chart (exact values, no color dependency).
 * Content slots: `[chart]` and `[table]`.
 */
@Component({
  selector: 'app-chart-panel',
  imports: [MatIconModule],
  template: `
    <section class="card chart-panel" [attr.aria-labelledby]="headingId">
      <div class="card__header">
        <div>
          <h2 [id]="headingId">{{ title() }}</h2>
          @if (subtitle()) {
            <p>{{ subtitle() }}</p>
          }
        </div>
        <div class="seg" role="radiogroup" [attr.aria-label]="'View for ' + title()">
          <button
            type="button"
            class="seg__btn"
            role="radio"
            aria-label="Chart view"
            title="Chart view"
            [attr.aria-checked]="view() === 'chart'"
            [class.on]="view() === 'chart'"
            (click)="view.set('chart')"
          >
            <mat-icon>bar_chart</mat-icon>
          </button>
          <button
            type="button"
            class="seg__btn"
            role="radio"
            aria-label="Table view"
            title="Table view"
            [attr.aria-checked]="view() === 'table'"
            [class.on]="view() === 'table'"
            (click)="view.set('table')"
          >
            <mat-icon>table_rows</mat-icon>
          </button>
        </div>
      </div>
      @if (view() === 'chart') {
        <div class="chart-panel__chart" [style.height.px]="height()">
          <ng-content select="[chart]" />
        </div>
      } @else {
        <div class="chart-panel__table">
          <ng-content select="[table]" />
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
      height: 100%;
    }
    .chart-panel {
      height: 100%;
    }
    .chart-panel__chart {
      position: relative;
    }
    .chart-panel__table {
      max-height: 320px;
      overflow: auto;
    }
    /* Segmented icon switch: soft track, raised white active segment. */
    .seg {
      display: inline-flex;
      flex: none;
      gap: 2px;
      padding: 3px;
      border-radius: 10px;
      background: var(--mat-sys-surface-container-high);
    }
    .seg__btn {
      display: grid;
      place-items: center;
      width: 32px;
      height: 28px;
      padding: 0;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: var(--mat-sys-on-surface-variant);
      cursor: pointer;
      transition:
        background-color 120ms ease,
        color 120ms ease,
        box-shadow 120ms ease;
    }
    .seg__btn:hover:not(.on) {
      color: var(--mat-sys-on-surface);
    }
    .seg__btn.on {
      background: var(--app-card-bg);
      color: light-dark(var(--app-color-primary), var(--app-color-accent));
      box-shadow:
        0 1px 2px rgb(16 24 40 / 12%),
        0 0 0 1px rgb(16 24 40 / 4%);
    }
    .seg__btn:focus-visible {
      outline: 2px solid var(--app-color-accent);
      outline-offset: 1px;
    }
    .seg__btn mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    .card__header {
      margin-bottom: 8px;
      padding-bottom: 10px;
      align-items: center;
    }
    .card__header > div {
      min-width: 0;
    }
    .card__header h2 {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .card__header p {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `,
})
export class ChartPanelComponent {
  private static nextId = 0;
  readonly title = input.required<string>();
  readonly subtitle = input('');
  /** Includes axis labels (they render inside the canvas). */
  readonly height = input(280);
  protected readonly view = signal<'chart' | 'table'>('chart');
  protected readonly headingId = `chart-panel-${ChartPanelComponent.nextId++}`;
}
