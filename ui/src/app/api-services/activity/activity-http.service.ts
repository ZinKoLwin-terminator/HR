import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { ActivityEntry } from '../../core/models/activity.models';
import { PagedResult, PageRequest, isAppError } from '../../core/models/common.models';
import { ActivityDataService } from '../../core/services/data/activity-data.service';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

/** Sort keys the API accepts; others are dropped and the default (timestamp, newest first) applies. */
const SORT_KEYS: readonly string[] = ['timestamp', 'category', 'actor'];
const FILTER_KEYS: readonly string[] = ['category', 'actor', 'dateFrom', 'dateTo'];

/**
 * HTTP implementation of the activity feed contract. The log is append-only on the server:
 * this adapter only reads (`/activity/query`, `/activity/{id}`); there is no write route.
 * Entries are recorded by the API itself when other modules change data.
 */
@Injectable({ providedIn: 'root' })
export class ActivityHttpService implements ActivityDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** The newest `limit` entries (sort timestamp, newest first, is the API default). */
  getRecent(limit: number): Observable<ActivityEntry[]> {
    return this.query({ pageIndex: 0, pageSize: limit }).pipe(map((page) => page.items));
  }

  query(request: PageRequest<object>): Observable<PagedResult<ActivityEntry>> {
    let params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    if (request.sort?.active && request.sort.direction && SORT_KEYS.includes(request.sort.active)) {
      params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    const filters = (request.filters ?? {}) as Record<string, unknown>;
    for (const key of FILTER_KEYS) {
      const value = filters[key];
      if (value === null || value === undefined) {
        continue;
      }
      const text = String(value).trim();
      if (text !== '' && text.toLowerCase() !== 'all') {
        params = params.set(key, text);
      }
    }
    return this.http
      .get<PagedResult<ActivityEntry>>(`${this.baseUrl}/activity/query`, { params })
      .pipe(catchError((e) => this.fail(e)));
  }

  getById(id: number): Observable<ActivityEntry> {
    return this.http
      .get<ActivityEntry>(`${this.baseUrl}/activity/${id}`)
      .pipe(catchError((e) => this.fail(e)));
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}
