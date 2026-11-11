import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import {
  AppError,
  Entity,
  PagedResult,
  PageRequest,
  isAppError,
} from '../../core/models/common.models';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

/** The API serves at most 200 rows per page. */
const PAGE_SIZE = 200;
/** Safety stop when paging through everything: 50 pages = 10,000 rows. */
const MAX_PAGES = 50;

type ApiRow = Entity & { rowVersion?: string };

/**
 * Shared read side of the attendance resources that have no unpaged GET (records, adjustments,
 * overtime): `getAll` pages `/query`; `getById` and `query` map straight onto the API. The adapter
 * remembers the latest `rowVersion` of every row it has seen, for the commands and deletes.
 */
export abstract class AttendanceQueryHttpBase<T extends Entity> {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = inject(API_BASE_URL);
  private readonly rowVersions = new Map<number, string>();

  protected abstract readonly resource: string;
  protected abstract readonly filterKeys: readonly string[];
  protected abstract readonly sortKeys: readonly string[];

  protected get url(): string {
    return `${this.baseUrl}/${this.resource}`;
  }

  getAll(): Observable<T[]> {
    const loadPage = (pageIndex: number, acc: T[]): Observable<T[]> =>
      this.fetchPage(new HttpParams().set('pageIndex', pageIndex).set('pageSize', PAGE_SIZE)).pipe(
        switchMap((page) => {
          const all = [...acc, ...page.items];
          return page.items.length === 0 || all.length >= page.total || pageIndex + 1 >= MAX_PAGES
            ? of(all)
            : loadPage(pageIndex + 1, all);
        }),
      );
    return loadPage(0, []).pipe(catchError((e) => this.fail(e)));
  }

  getById(id: number): Observable<T> {
    return this.http.get<T & ApiRow>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<Record<string, unknown>>): Observable<PagedResult<T>> {
    return this.fetchPage(this.buildParams(request)).pipe(catchError((e) => this.fail(e)));
  }

  protected fetchPage(params: HttpParams): Observable<PagedResult<T>> {
    return this.http
      .get<PagedResult<T & ApiRow>>(`${this.url}/query`, { params })
      .pipe(tap((page) => page.items.forEach((item) => this.remember(item))));
  }

  /** POST `{id}/{name}` with `{ rowVersion, remarks? }`. */
  protected command(id: number, name: string, remarks?: string): Observable<T> {
    return this.versionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.post<T & ApiRow>(`${this.url}/${id}/${name}`, {
          rowVersion,
          ...(remarks === undefined ? {} : { remarks }),
        }),
      ),
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  protected removeById(id: number): Observable<void> {
    return this.versionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.delete<void>(`${this.url}/${id}`, { body: { rowVersion } }),
      ),
      tap(() => this.rowVersions.delete(id)),
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  protected remember(item: ApiRow): void {
    if (item.rowVersion) {
      this.rowVersions.set(item.id, item.rowVersion);
    }
  }

  /** The last version seen, else read the row to learn it. */
  private versionFor(id: number): Observable<string> {
    const known = this.rowVersions.get(id);
    if (known) {
      return of(known);
    }
    return this.http.get<T & ApiRow>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((item) => item.rowVersion ?? ''),
    );
  }

  private buildParams(request: PageRequest<Record<string, unknown>>): HttpParams {
    let params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    if (
      request.sort?.active &&
      request.sort.direction &&
      this.sortKeys.includes(request.sort.active)
    ) {
      params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    for (const key of this.filterKeys) {
      const value = request.filters?.[key];
      // "Only" switches are sent when on; off (false or null) means no filter.
      if (value === null || value === undefined || value === false) {
        continue;
      }
      const text = String(value).trim();
      if (text !== '' && text.toLowerCase() !== 'all') {
        params = params.set(key, text);
      }
    }
    return params;
  }

  protected fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}

/** "HH:mm" for the API (it rejects seconds); blank means not set. */
export function hhmm(value: unknown): string | null {
  const text = typeof value === 'string' ? value.trim() : '';
  return text === '' ? null : text.slice(0, 5);
}

export function remarksRequired(): AppError {
  return {
    code: 'Validation',
    message: 'Give a reason for rejecting the request.',
    fieldErrors: { remarks: 'A reason is required.' },
  };
}

export function notSupported(message: string): AppError {
  return { code: 'Forbidden', message };
}
