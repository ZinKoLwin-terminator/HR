import { DecimalPipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ScorecardItem } from '../../../core/models/kpi.models';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

/**
 * The KPIs of a scorecard as a table: code and name, category, how it is scored, target, weight
 * and dates. Read-only by default; with `editable` each row offers Edit and Remove (plan stage).
 * `showResults` adds the actual value, achievement and weighted score (for the result and review
 * screens). Wide tables scroll sideways; the less important columns hide on narrow screens.
 */
@Component({
  selector: 'app-scorecard-items-table',
  imports: [AppDatePipe, DecimalPipe, MatButtonModule, MatIconModule],
  template: `
    <div class="scroll" role="region" [attr.aria-label]="caption()" tabindex="0">
      <table>
        <caption class="cdk-visually-hidden">
          {{
            caption()
          }}
        </caption>
        <thead>
          <tr>
            <th scope="col">KPI</th>
            <th scope="col" class="hide-sm">Category</th>
            <th scope="col" class="hide-md">Direction · scoring</th>
            <th scope="col" class="num">Target</th>
            <th scope="col" class="num">Weight %</th>
            <th scope="col" class="hide-lg">Dates</th>
            @if (showResults()) {
              <th scope="col" class="num">Actual</th>
              <th scope="col" class="num">Achievement %</th>
              <th scope="col" class="num">Weighted</th>
            }
            @if (editable()) {
              <th scope="col"><span class="cdk-visually-hidden">Actions</span></th>
            }
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.id) {
            <tr>
              <td>
                <strong>{{ item.kpiCode }}</strong>
                <span class="sub">{{ item.kpiName }}</span>
              </td>
              <td class="hide-sm">{{ item.categoryName }}</td>
              <td class="hide-md">
                {{ item.direction }}
                <span class="sub">{{ item.scoringMethod }}</span>
              </td>
              <td class="num">
                {{ item.target | number: '1.0-4' }}
                @if (item.unit) {
                  <span class="sub">{{ item.unit }}</span>
                }
              </td>
              <td class="num">{{ item.weight | number: '1.0-2' }}</td>
              <td class="hide-lg nowrap">
                {{ item.startDate | appDate }} – {{ item.endDate | appDate }}
              </td>
              @if (showResults()) {
                <td class="num">{{ item.actualValue ?? '—' }}</td>
                <td class="num">
                  {{ item.achievementPct === null ? '—' : (item.achievementPct | number: '1.2-2') }}
                </td>
                <td class="num">
                  {{ item.weightedScore === null ? '—' : (item.weightedScore | number: '1.2-2') }}
                </td>
              }
              @if (editable()) {
                <td class="actions">
                  <button
                    mat-icon-button
                    type="button"
                    [attr.aria-label]="'Edit ' + item.kpiCode"
                    [disabled]="disabled()"
                    (click)="edit.emit(item)"
                  >
                    <mat-icon>edit</mat-icon>
                  </button>
                  <button
                    mat-icon-button
                    type="button"
                    [attr.aria-label]="'Remove ' + item.kpiCode"
                    [disabled]="disabled()"
                    (click)="remove.emit(item)"
                  >
                    <mat-icon>delete</mat-icon>
                  </button>
                </td>
              }
            </tr>
          } @empty {
            <tr>
              <td class="empty" [attr.colspan]="columnCount()">No KPIs on this scorecard yet.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .scroll {
      overflow-x: auto;
      border: 1px solid var(--app-card-border, var(--mat-sys-outline-variant));
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .scroll:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: -2px;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font: var(--mat-sys-body-medium);
    }
    th,
    td {
      padding: 10px 14px;
      text-align: start;
      vertical-align: top;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    thead th {
      font: var(--mat-sys-label-medium);
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
    }
    tbody tr:last-child td {
      border-bottom: 0;
    }
    .num {
      text-align: end;
      white-space: nowrap;
    }
    .nowrap {
      white-space: nowrap;
    }
    .sub {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .actions {
      white-space: nowrap;
      text-align: end;
    }
    .empty {
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 1200px) {
      .hide-lg {
        display: none;
      }
    }
    @media (max-width: 900px) {
      .hide-md {
        display: none;
      }
    }
    @media (max-width: 600px) {
      .hide-sm {
        display: none;
      }
    }
  `,
})
export class ScorecardItemsTableComponent {
  readonly items = input.required<readonly ScorecardItem[]>();
  readonly caption = input('KPIs on the scorecard');
  /** Offers Edit and Remove on each row (the plan stage). */
  readonly editable = input(false);
  /** Disables Edit and Remove (for example while a change is being saved). */
  readonly disabled = input(false);
  /** Adds the actual value, achievement and weighted score columns. */
  readonly showResults = input(false);
  readonly edit = output<ScorecardItem>();
  readonly remove = output<ScorecardItem>();

  protected readonly columnCount = computed(
    () => 6 + (this.showResults() ? 3 : 0) + (this.editable() ? 1 : 0),
  );
}
