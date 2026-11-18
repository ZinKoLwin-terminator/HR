import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AppNotification } from '../../core/models/activity.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import { MOCK_NOTIFICATIONS } from '../../mock-data/activity/activities.mock';
import { mockResponse } from '../mock-response';
import { persistedSignal } from '../persistence';
import { initialRecords } from '../seed-mode';

/** What another mock service asks to send: the notification without id, time and read flag. */
export type NewMockNotification = Pick<AppNotification, 'title' | 'message' | 'icon' | 'link'>;

/**
 * In-memory notifications. The seeded ones are shown to every signed-in user; one added by another
 * mock service through {@link add} is addressed to one user (as `app.Notification` rows are in the
 * API) and only that user's list shows it. The recipient is kept in a private map, never on the
 * returned objects. Simulation: the demo has no real recipients and enforces nothing.
 */
@Injectable({ providedIn: 'root' })
export class NotificationMockService implements NotificationDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly user = inject(CurrentUserService);
  private readonly items = persistedSignal<AppNotification[]>('notifications', () =>
    structuredClone(initialRecords(MOCK_NOTIFICATIONS) as AppNotification[]),
  );
  /** Notification id → recipient user id, for notifications added through {@link add}. */
  private readonly recipients = persistedSignal<Record<number, number>>(
    'notification_recipients',
    () => ({}),
  );

  /**
   * For OTHER MOCK services only (not on the contract): one notification per recipient user id,
   * newest first. Duplicate ids are sent once.
   */
  add(notification: NewMockNotification, recipientUserIds: readonly number[]): void {
    const unique = [...new Set(recipientUserIds)];
    if (unique.length === 0) {
      return;
    }
    const timestamp = new Date().toISOString();
    let nextId = Math.max(0, ...this.items().map((n) => n.id)) + 1;
    const added: AppNotification[] = [];
    const addressed: Record<number, number> = {};
    for (const userId of unique) {
      const id = nextId++;
      added.push({ ...notification, id, timestamp, read: false });
      addressed[id] = userId;
    }
    this.items.update((list) => [...added, ...list]);
    this.recipients.update((map) => ({ ...map, ...addressed }));
  }

  /** The notifications the signed-in user may see (seeded ones, and those addressed to them). */
  private mine(): AppNotification[] {
    const me = this.user.current().id;
    const recipients = this.recipients();
    return this.items().filter((n) => {
      const recipient = recipients[n.id];
      return recipient === undefined || recipient === me;
    });
  }

  getNotifications(): Observable<AppNotification[]> {
    return mockResponse(() => this.mine(), this.latency);
  }

  markAsRead(id: number): Observable<void> {
    return mockResponse(() => {
      if (!this.mine().some((n) => n.id === id)) {
        return;
      }
      this.items.update((list) => list.map((n) => (n.id === id ? { ...n, read: true } : n)));
    }, this.latency);
  }

  markAllAsRead(): Observable<void> {
    return mockResponse(() => {
      const visible = new Set(this.mine().map((n) => n.id));
      this.items.update((list) => list.map((n) => (visible.has(n.id) ? { ...n, read: true } : n)));
    }, this.latency);
  }
}
