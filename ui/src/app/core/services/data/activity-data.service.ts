import { Observable } from 'rxjs';
import { ActivityEntry, AppNotification } from '../../models/activity.models';

export abstract class ActivityDataService {
  abstract getRecent(limit: number): Observable<ActivityEntry[]>;
}

export abstract class NotificationDataService {
  abstract getNotifications(): Observable<AppNotification[]>;
  abstract markAsRead(id: number): Observable<void>;
  abstract markAllAsRead(): Observable<void>;
}
