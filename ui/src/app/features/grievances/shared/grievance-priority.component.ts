import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { GrievancePriority } from '../../../core/models/grievance.models';

const ICONS: Record<GrievancePriority, string> = {
  Low: 'keyboard_arrow_down',
  Medium: 'drag_handle',
  High: 'keyboard_arrow_up',
  Critical: 'priority_high',
};

/**
 * A grievance priority: icon and word (color is never the only cue). "Not set" until the case is
 * acknowledged.
 */
@Component({
  selector: 'app-grievance-priority',
  imports: [MatIconModule],
  template: `
    @let value = priority();
    @if (value) {
      <span class="priority" [attr.data-priority]="value">
        <mat-icon aria-hidden="true">{{ icon() }}</mat-icon
        >{{ value }}
      </span>
    } @else {
      <span class="priority priority--none">Not set</span>
    }
  `,
  styles: `
    .priority {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      font: var(--mat-sys-label-large);
      white-space: nowrap;
    }
    .priority mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    [data-priority='High'] {
      color: var(--app-color-warning);
    }
    [data-priority='Critical'] {
      color: var(--app-color-error);
      font-weight: 700;
    }
    .priority--none {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class GrievancePriorityComponent {
  readonly priority = input<GrievancePriority | null>(null);
  protected readonly icon = computed(() => {
    const value = this.priority();
    return value ? ICONS[value] : '';
  });
}
