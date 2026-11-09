import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ActivityCategory, ActivityEntry } from '../../../core/models/activity.models';
import { RelativeTimePipe } from '../../../shared/pipes/relative-time.pipe';

const ICONS: Record<ActivityCategory, string> = {
  employee: 'badge',
  organization: 'corporate_fare',
  attendance: 'schedule',
  leave: 'beach_access',
  payroll: 'payments',
  recruitment: 'person_search',
  system: 'settings',
};

@Component({
  selector: 'app-activity-feed-card',
  imports: [MatIconModule, RelativeTimePipe],
  template: `
    <section class="card" aria-labelledby="activity-heading">
      <div class="card__header">
        <div>
          <h2 id="activity-heading">Recent activities</h2>
          <p>Latest changes across modules</p>
        </div>
      </div>
      <ul class="list">
        @for (a of activities(); track a.id) {
          <li class="list__item">
            <span class="list__icon" aria-hidden="true"
              ><mat-icon>{{ icon(a.category) }}</mat-icon></span
            >
            <div class="list__text">
              <span class="list__title">{{ a.message }}</span>
              <span class="list__meta">{{ a.actor }} · {{ a.timestamp | relativeTime }}</span>
            </div>
          </li>
        } @empty {
          <li class="muted">No recent activity.</li>
        }
      </ul>
    </section>
  `,
  styleUrl: './dashboard-list.scss',
  styles: `
    .list__title {
      font: var(--mat-sys-body-medium);
    }
  `,
})
export class ActivityFeedCardComponent {
  readonly activities = input.required<ActivityEntry[]>();

  protected icon(category: ActivityCategory): string {
    return ICONS[category];
  }
}
