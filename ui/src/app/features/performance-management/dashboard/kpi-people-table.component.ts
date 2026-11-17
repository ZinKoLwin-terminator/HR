import { Component, input } from '@angular/core';
import { KpiEmployeeScore } from '../../../core/models/kpi-insight.models';
import { scoreText } from './kpi-dashboard-views';

/** A short list of employees with their score and rating (top performers, attention list). */
@Component({
  selector: 'app-kpi-people-table',
  template: `
    @if (rows().length) {
      <div class="scroll" role="region" [attr.aria-label]="caption()" tabindex="0">
        <table class="simple-table">
          <caption class="cdk-visually-hidden">
            {{
              caption()
            }}
          </caption>
          <thead>
            <tr>
              <th scope="col">Code</th>
              <th scope="col">Employee</th>
              <th scope="col" class="hide-sm">Department</th>
              <th scope="col" class="num">Score</th>
            </tr>
          </thead>
          <tbody>
            @for (e of rows(); track e.scorecardId) {
              <tr>
                <th scope="row">{{ e.employeeCode }}</th>
                <td>{{ e.employeeName }}</td>
                <td class="hide-sm">{{ e.departmentName }}</td>
                <td class="num">{{ score(e.overallScore, e.ratingLabel) }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <p class="muted">{{ none() }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .scroll {
      overflow-x: auto;
    }
    .muted {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 599.98px) {
      .hide-sm {
        display: none;
      }
    }
  `,
})
export class KpiPeopleTableComponent {
  readonly rows = input.required<readonly KpiEmployeeScore[]>();
  readonly caption = input.required<string>();
  /** Shown instead of an empty table. */
  readonly none = input('Nobody to list.');

  protected readonly score = scoreText;
}
