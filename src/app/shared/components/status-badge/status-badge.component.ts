import { Component, computed, input } from '@angular/core';

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
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
      background: var(--badge-bg);
      color: var(--badge-fg);
    }
    .dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }
    [data-tone='success'] {
      --badge-bg: var(--app-success-container);
      --badge-fg: var(--app-on-success-container);
    }
    [data-tone='warning'] {
      --badge-bg: var(--app-warning-container);
      --badge-fg: var(--app-on-warning-container);
    }
    [data-tone='danger'] {
      --badge-bg: var(--mat-sys-error-container);
      --badge-fg: var(--mat-sys-on-error-container);
    }
    [data-tone='info'] {
      --badge-bg: var(--mat-sys-primary-container);
      --badge-fg: var(--mat-sys-on-primary-container);
    }
    [data-tone='neutral'] {
      --badge-bg: var(--mat-sys-surface-container-highest);
      --badge-fg: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class StatusBadgeComponent {
  readonly status = input.required<string>();
  protected readonly tone = computed(() => badgeTone(this.status()));
}
