import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { ScorecardDetail } from '../../../core/models/kpi.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { LateChipComponent } from './late-chip.component';
import { isLateSubmission } from './scorecard-rules';

/**
 * The header facts of a scorecard on the employee and reviewer screens: period (with its status
 * and due date), status, reviewer, submission (with the Late chip) and, once the server has scored
 * it, the overall score and rating. The score is always the server's.
 */
@Component({
  selector: 'app-scorecard-summary',
  imports: [AppDatePipe, DecimalPipe, LateChipComponent, StatusBadgeComponent],
  template: `
    @let c = card();
    <section class="card" aria-label="Scorecard summary">
      <dl class="facts">
        @if (showEmployee()) {
          <div>
            <dt>Employee</dt>
            <dd>{{ c.employeeCode }} · {{ c.employeeName }}</dd>
          </div>
          <div>
            <dt>Department</dt>
            <dd>{{ c.departmentName }}</dd>
          </div>
        }
        <div>
          <dt>Period</dt>
          <dd>{{ c.periodName }} <app-status-badge [status]="c.periodStatus" /></dd>
        </div>
        <div>
          <dt>Submission due</dt>
          <dd>{{ c.submissionDueDate ? (c.submissionDueDate | appDate) : 'No due date' }}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd data-testid="scorecard-status"><app-status-badge [status]="c.status" /></dd>
        </div>
        <div>
          <dt>Reviewer</dt>
          <dd>{{ c.reviewerName ?? 'None: HR reviews' }}</dd>
        </div>
        <div>
          <dt>Submitted</dt>
          <dd>
            @if (c.submittedOn) {
              {{ c.submittedOn | appDate }}
              @if (late()) {
                <app-late-chip />
              }
            } @else {
              Not yet
            }
          </dd>
        </div>
        <div>
          <dt>Overall score</dt>
          <dd data-testid="overall-score">
            @if (c.overallScore !== null) {
              <strong>{{ c.overallScore | number: '1.2-2' }}</strong>
              @if (c.ratingLabel) {
                <span class="label">{{ c.ratingLabel }}</span>
              }
            } @else {
              <span class="muted">Calculated by the server when submitted</span>
            }
          </dd>
        </div>
        @if (c.finalizedOn) {
          <div>
            <dt>Finalized</dt>
            <dd>{{ c.finalizedOn | appDate }}</dd>
          </div>
        }
      </dl>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .card {
      padding: 16px 20px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 14px 24px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    dd {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      margin: 2px 0 0;
      font: var(--mat-sys-body-large);
    }
    .label {
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class ScorecardSummaryComponent {
  readonly card = input.required<ScorecardDetail>();
  /** Adds the employee and department (the reviewer's view). */
  readonly showEmployee = input(false);

  protected readonly late = computed(() =>
    isLateSubmission(this.card().submittedOn, this.card().submissionDueDate),
  );
}
