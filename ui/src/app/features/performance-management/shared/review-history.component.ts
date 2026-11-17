import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { ReviewAction } from '../../../core/models/kpi.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

/**
 * The approval history of a scorecard, oldest first: step, action, the status change, who did it,
 * when and the comment. Read-only: the history is append-only on the server.
 */
@Component({
  selector: 'app-review-history',
  imports: [DatePipe, StatusBadgeComponent],
  template: `
    @if (history().length === 0) {
      <p class="empty">No history yet. Steps appear here once the plan is finalized.</p>
    } @else {
      <ol class="history" aria-label="Review history">
        @for (entry of history(); track $index) {
          <li>
            <div class="head">
              <strong>{{ entry.action }}</strong>
              <span class="step">{{ entry.step }} step</span>
              <span class="change">
                <app-status-badge [status]="entry.fromStatus" />
                <span aria-label="to">→</span>
                <app-status-badge [status]="entry.toStatus" />
              </span>
            </div>
            <div class="meta">
              {{ entry.actorName }} · {{ entry.occurredAtUtc | date: 'dd MMM yyyy, HH:mm' }}
              @if (entry.overallScore !== null) {
                · score {{ entry.overallScore }}
              }
            </div>
            @if (entry.comment) {
              <p class="comment">{{ entry.comment }}</p>
            }
          </li>
        }
      </ol>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .empty {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .history {
      margin: 0;
      padding: 0;
      list-style: none;
      display: grid;
      gap: 12px;
    }
    li {
      padding: 12px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 12px;
    }
    .step {
      color: var(--mat-sys-on-surface-variant);
    }
    .change {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .meta {
      margin-top: 4px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .comment {
      margin: 8px 0 0;
      white-space: pre-line;
    }
  `,
})
export class ReviewHistoryComponent {
  readonly history = input.required<readonly ReviewAction[]>();
}
