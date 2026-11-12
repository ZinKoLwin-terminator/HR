import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import {
  BLACKLIST_REASON_MAX_LENGTH,
  BlacklistEntry,
  BlacklistFilter,
} from '../../core/models/blacklist.models';
import { AppError, PagedResult, PageRequest, isAppError } from '../../core/models/common.models';
import { BlacklistDataService } from '../../core/services/data/blacklist-data.service';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

const SORT_KEYS = ['id', 'employeeId', 'blacklistedOn', 'liftedOn'];

/**
 * HTTP implementation of the do-not-rehire contract (`/api/employee-blacklist`).
 *
 *  - `query` sends `pageIndex`, `pageSize`, `sort` (only the keys the API knows), `search` and
 *    `active` (`true` in force, `false` lifted, omitted both).
 *  - `getActiveForEmployee` is `GET /employees/{id}/blacklist`; its 404 means "not listed" and is
 *    returned as `null`.
 *  - `add` is `POST` with `{ employeeId, reason }`; `lift` is `POST {id}/lift` with
 *    `{ rowVersion, reason }`. A blank or over-long reason is refused locally, before any request,
 *    the way the API would (400 on `reason`). The `rowVersion` is the last one seen for the entry,
 *    or the one passed in; when neither is known the entry is read first.
 *  - The reason is sensitive: it is held only in the rows handed to the screens, never logged or
 *    kept in browser storage.
 */
@Injectable({ providedIn: 'root' })
export class BlacklistHttpService implements BlacklistDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly versions = new Map<number, string>();

  private get url(): string {
    return `${this.baseUrl}/employee-blacklist`;
  }

  query(request: PageRequest<BlacklistFilter>): Observable<PagedResult<BlacklistEntry>> {
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
    const active = String(request.filters?.active ?? '').toLowerCase();
    if (active === 'true' || active === 'false') {
      params = params.set('active', active);
    }
    return this.http.get<PagedResult<BlacklistEntry>>(`${this.url}/query`, { params }).pipe(
      tap((page) => page.items.forEach((entry) => this.remember(entry))),
      catchError((e) => this.fail(e)),
    );
  }

  getById(id: number): Observable<BlacklistEntry> {
    return this.http.get<BlacklistEntry>(`${this.url}/${id}`).pipe(
      tap((entry) => this.remember(entry)),
      catchError((e) => this.fail(e)),
    );
  }

  getActiveForEmployee(employeeId: number): Observable<BlacklistEntry | null> {
    return this.http.get<BlacklistEntry>(`${this.baseUrl}/employees/${employeeId}/blacklist`).pipe(
      tap((entry) => this.remember(entry)),
      catchError((e) => {
        const error = isAppError(e) ? e : mapHttpError(e);
        return error.code === 'NotFound' ? of(null) : throwError(() => error);
      }),
    );
  }

  add(employeeId: number, reason: string): Observable<BlacklistEntry> {
    const text = (reason ?? '').trim();
    const invalid = reasonError(text);
    if (invalid) {
      return throwError(() => invalid);
    }
    return this.http
      .post<BlacklistEntry>(this.url, { employeeId: Number(employeeId), reason: text })
      .pipe(
        tap((entry) => this.remember(entry)),
        catchError((e) => this.fail(e)),
      );
  }

  lift(id: number, reason: string, rowVersion?: string): Observable<BlacklistEntry> {
    const text = (reason ?? '').trim();
    const invalid = reasonError(text);
    if (invalid) {
      return throwError(() => invalid);
    }
    return this.versionFor(id, rowVersion).pipe(
      switchMap((version) =>
        this.http.post<BlacklistEntry>(`${this.url}/${id}/lift`, {
          rowVersion: version,
          reason: text,
        }),
      ),
      tap((entry) => this.remember(entry)),
      catchError((e) => this.fail(e)),
    );
  }

  private versionFor(id: number, explicit?: string): Observable<string> {
    const known = explicit ?? this.versions.get(id);
    return known
      ? of(known)
      : this.http
          .get<BlacklistEntry>(`${this.url}/${id}`)
          .pipe(map((entry) => entry.rowVersion ?? ''));
  }

  private remember(entry: BlacklistEntry): void {
    if (entry.rowVersion) {
      this.versions.set(entry.id, entry.rowVersion);
    }
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}

function reasonError(text: string): AppError | null {
  if (!text) {
    return {
      code: 'Validation',
      message: 'Enter a reason.',
      fieldErrors: { reason: 'A reason is required.' },
    };
  }
  if (text.length > BLACKLIST_REASON_MAX_LENGTH) {
    return {
      code: 'Validation',
      message: `The reason can have at most ${BLACKLIST_REASON_MAX_LENGTH} characters.`,
      fieldErrors: { reason: `At most ${BLACKLIST_REASON_MAX_LENGTH} characters.` },
    };
  }
  return null;
}
