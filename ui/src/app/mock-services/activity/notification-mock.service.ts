import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AppNotification } from '../../core/models/activity.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import { MOCK_NOTIFICATIONS } from '../../mock-data/activity/activities.mock';
import { mockResponse } from '../mock-response';
import { persistedSignal } from '../persistence';
import { initialRecords } from '../seed-mode';

@Injectable({ providedIn: 'root' })
export class NotificationMockService implements NotificationDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly items = persistedSignal<AppNotification[]>('notifications', () =>
    structuredClone(initialRecords(MOCK_NOTIFICATIONS) as AppNotification[]),
  );

  getNotifications(): Observable<AppNotification[]> {
    return mockResponse(() => this.items(), this.latency);
  }

  markAsRead(id: number): Observable<void> {
    return mockResponse(() => {
      this.items.update((list) => list.map((n) => (n.id === id ? { ...n, read: true } : n)));
    }, this.latency);
  }

  markAllAsRead(): Observable<void> {
    return mockResponse(() => {
      this.items.update((list) => list.map((n) => ({ ...n, read: true })));
    }, this.latency);
  }
}
