import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { AppNotification } from '../../core/models/activity.models';
import { PagedResult, isAppError } from '../../core/models/common.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/** The mock returns every notification, so ask the API for a large page. */
export const NOTIFICATION_PAGE_SIZE = 200;

/**
 * HTTP implementation of the notification contract. The API returns the caller's own rows plus
 * broadcast rows. Limitation: a broadcast row has one read flag, so reading it marks it read for
 * everyone.
 */
@Injectable({ providedIn: 'root' })
export class NotificationHttpService implements NotificationDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getNotifications(): Observable<AppNotification[]> {
    const params = new HttpParams().set('pageIndex', 0).set('pageSize', NOTIFICATION_PAGE_SIZE);
    return this.http
      .get<PagedResult<AppNotification>>(`${this.baseUrl}/notifications/query`, { params })
      .pipe(
        map((page) => page.items),
        catchError((e) => this.fail(e)),
      );
  }

  markAsRead(id: number): Observable<void> {
    return this.http.post<AppNotification>(`${this.baseUrl}/notifications/${id}/read`, null).pipe(
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  markAllAsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/notifications/read-all`, null).pipe(
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}
