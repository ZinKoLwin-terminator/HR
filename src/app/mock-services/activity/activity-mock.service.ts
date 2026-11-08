import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { ActivityCategory, ActivityEntry } from '../../core/models/activity.models';
import { ActivityDataService } from '../../core/services/data/activity-data.service';
import { CurrentUserService } from '../../core/services/current-user.service';
import { MOCK_ACTIVITIES } from '../../mock-data/activity/activities.mock';
import { mockResponse } from '../mock-response';
import { persistedSignal } from '../persistence';
import { initialRecords } from '../seed-mode';

/** In-memory activity feed. Mock CRUD services append entries here. */
@Injectable({ providedIn: 'root' })
export class ActivityMockService implements ActivityDataService {
  private readonly currentUser = inject(CurrentUserService);
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly entries = persistedSignal<ActivityEntry[]>('activity', () =>
    structuredClone(initialRecords(MOCK_ACTIVITIES) as ActivityEntry[]),
  );

  record(category: ActivityCategory, message: string): void {
    this.entries.update((items) => [
      {
        id: Math.max(0, ...items.map((i) => i.id)) + 1,
        category,
        message,
        actor: this.currentUser.current().displayName,
        timestamp: new Date().toISOString(),
      },
      ...items,
    ]);
  }

  snapshot(limit: number): ActivityEntry[] {
    return [...this.entries()]
      .sort((a, b) => b.timestamp.localeCompare(a.timestamp))
      .slice(0, limit);
  }

  getRecent(limit: number): Observable<ActivityEntry[]> {
    return mockResponse(() => this.snapshot(limit), this.latency);
  }
}
