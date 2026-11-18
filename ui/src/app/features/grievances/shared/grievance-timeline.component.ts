import { Component, input } from '@angular/core';
import {
  GRIEVANCE_HISTORY_ACTION_LABELS,
  GRIEVANCE_STATUS_LABELS,
  GrievanceHistoryEntry,
  GrievanceStatus,
  MyGrievanceTimelineEntry,
} from '../../../core/models/grievance.models';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { RelativeTimePipe } from '../../../shared/pipes/relative-time.pipe';

/** One step as the timeline shows it (from the submitter's or the case team's history). */
export interface GrievanceTimelineItem {
  key: string;
  label: string;
  toStatus: GrievanceStatus;
  /** Who acted, already as the viewer may see it ("You", "HR team", a name). */
  actor: string;
  reason: string | null;
  occurredAtUtc: string;
}

/** The submitter's timeline: HR is always "HR team". */
export function myTimelineItems(
  entries: readonly MyGrievanceTimelineEntry[],
): GrievanceTimelineItem[] {
  return entries.map((e, i) => ({
    key: `${i}-${e.occurredAtUtc}`,
    label: GRIEVANCE_HISTORY_ACTION_LABELS[e.action],
    toStatus: e.toStatus,
    actor: e.actor,
    reason: e.reason,
    occurredAtUtc: e.occurredAtUtc,
  }));
}

/** The case team's history (actor names come redacted from the server). */
export function teamTimelineItems(
  entries: readonly GrievanceHistoryEntry[],
): GrievanceTimelineItem[] {
  return entries.map((e) => ({
    key: String(e.id),
    label: GRIEVANCE_HISTORY_ACTION_LABELS[e.action],
    toStatus: e.toStatus,
    actor: e.actorName,
    reason: e.reason,
    occurredAtUtc: e.occurredAtUtc,
  }));
}

/** The steps of a case, oldest first, as an ordered list. */
@Component({
  selector: 'app-grievance-timeline',
  imports: [AppDatePipe, RelativeTimePipe],
  template: `
    @if (items().length === 0) {
      <p class="muted">Nothing has happened on this grievance yet.</p>
    } @else {
      <ol class="timeline" [attr.aria-label]="label()">
        @for (item of items(); track item.key) {
          <li class="step">
            <span class="dot" aria-hidden="true"></span>
            <div class="body">
              <p class="title">
                <strong>{{ item.label }}</strong>
                <span class="status">· {{ statusLabels[item.toStatus] }}</span>
              </p>
              <p class="meta">
                {{ item.actor }} ·
                <time [attr.datetime]="item.occurredAtUtc" [title]="item.occurredAtUtc | appDate">{{
                  item.occurredAtUtc | relativeTime
                }}</time>
              </p>
              @if (item.reason) {
                <p class="reason">{{ item.reason }}</p>
              }
            </div>
          </li>
        }
      </ol>
    }
  `,
  styles: `
    .timeline {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .step {
      position: relative;
      display: flex;
      gap: 12px;
      padding-bottom: 14px;
    }
    .step:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 5px;
      top: 14px;
      bottom: 0;
      width: 2px;
      background: var(--mat-sys-outline-variant);
    }
    .dot {
      flex: none;
      width: 12px;
      height: 12px;
      margin-top: 4px;
      border-radius: 50%;
      background: var(--mat-sys-primary);
    }
    .title,
    .meta,
    .reason {
      margin: 0;
    }
    .status,
    .meta,
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .reason {
      margin-top: 4px;
      white-space: pre-line;
      font: var(--mat-sys-body-medium);
    }
  `,
})
export class GrievanceTimelineComponent {
  readonly items = input.required<GrievanceTimelineItem[]>();
  readonly label = input('Grievance timeline');
  protected readonly statusLabels = GRIEVANCE_STATUS_LABELS;
}
