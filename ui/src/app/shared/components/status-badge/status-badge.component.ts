import { Component, computed, input } from '@angular/core';
import { BLACKLIST_MATCH_LABEL } from '../../../core/models/blacklist.models';

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const TONES: Record<string, BadgeTone> = {
  active: 'success',
  present: 'success',
  approved: 'success',
  paid: 'success',
  selected: 'success',
  hired: 'success',
  open: 'success',
  probation: 'warning',
  late: 'warning',
  'half day': 'warning',
  submitted: 'warning',
  pending: 'warning',
  'pending approval': 'warning',
  screening: 'warning',
  absent: 'danger',
  rejected: 'danger',
  terminated: 'danger',
  suspended: 'danger',
  'on leave': 'info',
  processing: 'info',
  interview: 'info',
  applied: 'info',
  holiday: 'info',
  inactive: 'neutral',
  cancelled: 'neutral',
  draft: 'neutral',
  weekend: 'neutral',
  resigned: 'neutral',
  closed: 'neutral',
  'on hold': 'warning',
  filled: 'success',
  scheduled: 'info',
  completed: 'success',
  valid: 'success',
  warning: 'warning',
  invalid: 'danger',
  duplicate: 'warning',
  failed: 'danger',
  'completed with errors': 'warning',
  'failed (rolled back)': 'danger',
  'validated (not imported)': 'info',
  'in force': 'danger',
  lifted: 'neutral',
  [BLACKLIST_MATCH_LABEL.toLowerCase()]: 'danger',
};

export function badgeTone(status: string): BadgeTone {
  return TONES[status.trim().toLowerCase()] ?? 'neutral';
}

/** Colored pill for workflow / record statuses. Color is never the only cue: the label is always shown. */
@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [attr.data-tone]="tone()"
    ><span class="dot" aria-hidden="true"></span>{{ status() }}</span
  >`,
  styles: `
    :host {
      display: inline-flex;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px 3px 8px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
      background: var(--badge-bg);
      color: var(--badge-fg);
      font-weight: 600;
      box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--badge-fg) 18%, transparent);
    }
    .dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }
    [data-tone='success'] {
      --badge-bg: light-dark(
        color-mix(in srgb, var(--app-color-success) 15%, #f0fdf4),
        color-mix(in srgb, var(--app-color-success) 20%, #064e3b)
      );
      --badge-fg: light-dark(
        color-mix(in srgb, var(--app-color-success) 60%, #000),
        color-mix(in srgb, var(--app-color-success) 40%, #ffffff)
      );
    }
    [data-tone='warning'] {
      --badge-bg: light-dark(
        color-mix(in srgb, var(--app-color-warning) 15%, #fffbeb),
        color-mix(in srgb, var(--app-color-warning) 20%, #78350f)
      );
      --badge-fg: light-dark(
        color-mix(in srgb, var(--app-color-warning) 60%, #000),
        color-mix(in srgb, var(--app-color-warning) 40%, #ffffff)
      );
    }
    [data-tone='danger'] {
      --badge-bg: light-dark(
        color-mix(in srgb, var(--app-color-error) 15%, #fef2f2),
        color-mix(in srgb, var(--app-color-error) 20%, #7f1d1d)
      );
      --badge-fg: light-dark(
        color-mix(in srgb, var(--app-color-error) 60%, #000),
        color-mix(in srgb, var(--app-color-error) 40%, #ffffff)
      );
    }
    [data-tone='info'] {
      --badge-bg: light-dark(
        color-mix(in srgb, var(--app-color-primary) 12%, #f0f9ff),
        color-mix(in srgb, var(--app-color-accent) 20%, #082f49)
      );
      --badge-fg: light-dark(
        var(--app-color-primary),
        color-mix(in srgb, var(--app-color-accent) 40%, #ffffff)
      );
    }
    [data-tone='neutral'] {
      --badge-bg: light-dark(
        color-mix(in srgb, var(--app-color-primary) 8%, #f3f4f6),
        color-mix(in srgb, var(--app-color-primary) 12%, #1f2937)
      );
      --badge-fg: light-dark(
        var(--mat-sys-on-surface-variant),
        color-mix(in srgb, var(--mat-sys-on-surface) 80%, #ffffff)
      );
    }
  `,
})
export class StatusBadgeComponent {
  readonly status = input.required<string>();
  protected readonly tone = computed(() => badgeTone(this.status()));
}
