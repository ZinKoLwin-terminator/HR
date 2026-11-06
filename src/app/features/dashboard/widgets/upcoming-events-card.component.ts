import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { UpcomingEvent, UpcomingEventType } from '../../../core/models/dashboard.models';

const ICONS: Record<UpcomingEventType, string> = {
  Holiday: 'celebration',
  Birthday: 'cake',
  'Work Anniversary': 'workspace_premium',
  Leave: 'flight_takeoff',
  Probation: 'rate_review',
};

@Component({
  selector: 'app-upcoming-events-card',
  imports: [DatePipe, MatIconModule],
  template: `
    <section class="card" aria-labelledby="events-heading">
      <div class="card__header">
        <div>
          <h2 id="events-heading">Upcoming events</h2>
          <p>Next 30 days</p>
        </div>
      </div>
      <ul class="list">
        @for (e of events(); track e.date + e.title + e.type) {
          <li class="list__item">
            <span class="date-badge" aria-hidden="true">
              <span class="date-badge__month">{{ e.date | date: 'MMM' }}</span>
              <span class="date-badge__day">{{ e.date | date: 'dd' }}</span>
            </span>
            <div class="list__text">
              <span class="list__title">{{ e.title }}</span>
              <span class="list__meta">
                <span class="cdk-visually-hidden">{{ e.date | date: 'fullDate' }} · </span>
                <mat-icon class="type-icon" aria-hidden="true">{{ icon(e.type) }}</mat-icon>
                {{ e.subtitle }}
              </span>
            </div>
          </li>
        } @empty {
          <li class="muted">Nothing scheduled in the next 30 days.</li>
        }
      </ul>
    </section>
  `,
  styleUrl: './dashboard-list.scss',
  styles: `
    .date-badge {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 44px;
      flex: none;
      border-radius: 12px;
      background: linear-gradient(
        160deg,
        var(--app-color-primary-lighter),
        var(--app-color-primary)
      );
      color: #fff;
      line-height: 1;
    }
    .date-badge__month {
      font-size: 10px;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }
    .date-badge__day {
      font-size: 17px;
      font-weight: 600;
      margin-top: 2px;
    }
    .list__meta {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .type-icon {
      font-size: 15px;
      width: 15px;
      height: 15px;
    }
  `,
})
export class UpcomingEventsCardComponent {
  readonly events = input.required<UpcomingEvent[]>();

  protected icon(type: UpcomingEventType): string {
    return ICONS[type];
  }
}
