import { Component, input, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';

/**
 * Card wrapper for a chart with a Chart/Table switch. The table view is the
 * accessible twin of every chart (exact values, no color dependency).
 * Content slots: `[chart]` and `[table]`.
 */
@Component({
  selector: 'app-chart-panel',
  imports: [MatButtonToggleModule, MatIconModule],
  template: `
    <section class="card chart-panel" [attr.aria-labelledby]="headingId">
      <div class="card__header">
        <div>
          <h2 [id]="headingId">{{ title() }}</h2>
          @if (subtitle()) {
            <p>{{ subtitle() }}</p>
          }
        </div>
        <mat-button-toggle-group
          hideSingleSelectionIndicator
          [value]="view()"
          (change)="view.set($event.value)"
          [attr.aria-label]="'View for ' + title()"
        >
          <mat-button-toggle value="chart" aria-label="Chart view"
            ><mat-icon>bar_chart</mat-icon></mat-button-toggle
          >
          <mat-button-toggle value="table" aria-label="Table view"
            ><mat-icon>table_rows</mat-icon></mat-button-toggle
          >
        </mat-button-toggle-group>
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
    mat-button-toggle-group {
      --mat-button-toggle-height: 32px;
      flex: none;
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
