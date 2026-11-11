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

/** What the API sends: every text member may be null, and a row carries its concurrency token. */
export type ApiShape<T> = { [K in keyof T]: T[K] | null } & { rowVersion?: string };
/** A screen model that still carries the `rowVersion` it was read with. */
export type Versioned<T> = T & { rowVersion?: string };

/**
 * Shared HTTP side of the four recruitment resources (job openings, candidates, applications,
 * interviews). Each has an unpaged `GET`, a paged `/query`, `GET {id}`, and a `DELETE` that carries
 * the `rowVersion`.
 *
 * The adapter remembers the latest row it has seen for every id (from every response). Writes use
 * that `rowVersion`, so a save made from a stale list is rejected by the API with 409 instead of
 * silently overwriting. When it has never seen the record it reads it first. API `null` text is
 * mapped to `""` (see `normalise`) because the screens expect strings.
 */
export abstract class RecruitmentHttpBase<T extends Entity> {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = inject(API_BASE_URL);
  private readonly rows = new Map<number, Versioned<T>>();

  protected abstract readonly resource: string;
  /** Filter keys the API accepts. Others are not sent. */
  protected abstract readonly filterKeys: readonly string[];
  /** Sort keys the API accepts (it rejects unknown keys); other clicks keep the default order. */
  protected abstract readonly sortKeys: readonly string[];
  /** API row to screen model: null text becomes "", numbers are numbers, times are "HH:mm". */
  protected abstract normalise(raw: ApiShape<T>): Versioned<T>;

  protected get url(): string {
    return `${this.baseUrl}/${this.resource}`;
  }

  getAll(): Observable<T[]> {
    return this.http.get<ApiShape<T>[]>(this.url).pipe(
      map((items) => items.map((item) => this.keep(item))),
      catchError((e) => this.fail(e)),
    );
  }

  getById(id: number): Observable<T> {
    return this.http.get<ApiShape<T>>(`${this.url}/${id}`).pipe(
      map((item) => this.keep(item)),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<object>): Observable<PagedResult<T>> {
    return this.http
      .get<PagedResult<ApiShape<T>>>(`${this.url}/query`, { params: this.buildParams(request) })
      .pipe(
        map((page) => ({ ...page, items: page.items.map((item) => this.keep(item)) })),
        catchError((e) => this.fail(e)),
      );
  }

  delete(id: number): Observable<void> {
    return this.versionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.delete<void>(`${this.url}/${id}`, { body: { rowVersion } }),
      ),
      tap(() => this.rows.delete(id)),
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  /** The row as last seen, else read from the API. */
  protected current(id: number): Observable<Versioned<T>> {
    const known = this.rows.get(id);
    return known
      ? of(known)
      : this.http.get<ApiShape<T>>(`${this.url}/${id}`).pipe(map((item) => this.keep(item)));
  }

  /** Explicit version, else the last one seen, else read the record to learn it. */
  protected versionFor(id: number, explicit?: string): Observable<string> {
    return explicit ? of(explicit) : this.current(id).pipe(map((row) => row.rowVersion ?? ''));
  }

  protected post(path: string, body: object): Observable<Versioned<T>> {
    return this.http
      .post<ApiShape<T>>(`${this.url}${path}`, body)
      .pipe(map((item) => this.keep(item)));
  }

  protected put(id: number, body: object, rowVersion: string): Observable<Versioned<T>> {
    return this.http
      .put<ApiShape<T>>(`${this.url}/${id}`, { ...body, rowVersion })
      .pipe(map((item) => this.keep(item)));
  }

  /** POST `{id}/{name}` with `{ rowVersion, ...extra }`. */
  protected command(
    id: number,
    name: string,
    extra: object = {},
    explicitVersion?: string,
  ): Observable<Versioned<T>> {
    return this.versionFor(id, explicitVersion).pipe(
      switchMap((rowVersion) => this.post(`/${id}/${name}`, { rowVersion, ...extra })),
    );
  }

  protected keep(raw: ApiShape<T>): Versioned<T> {
    const row = this.normalise(raw);
    this.rows.set(row.id, row);
    return row;
  }

  protected fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }

  private buildParams(request: PageRequest<object>): HttpParams {
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
    const filters = (request.filters ?? {}) as Record<string, unknown>;
    for (const key of this.filterKeys) {
      const value = filters[key];
      if (value === null || value === undefined) {
        continue;
      }
      const text = String(value).trim();
      if (text !== '' && text.toLowerCase() !== 'all') {
        params = params.set(key, text);
      }
    }
    return params;
  }
}

/** Blank text is sent as null (the API stores no empty strings). */
export function textOrNull(value: unknown): string | null {
  const text = typeof value === 'string' ? value.trim() : '';
  return text === '' ? null : text;
}

/** "HH:mm" for the API (it rejects seconds); blank means not set. */
export function hhmm(value: unknown): string | null {
  const text = typeof value === 'string' ? value.trim() : '';
  return text === '' ? null : text.slice(0, 5);
}

/** A number from a form value; blank or missing is null. */
export function numberOrNull(value: unknown): number | null {
  return value === null || value === undefined || value === '' ? null : Number(value);
}

/** The listed keys that the source actually has. */
export function pick(source: object, keys: readonly string[]): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  const result: Record<string, unknown> = {};
  for (const key of keys) {
    if (key in from) {
      result[key] = from[key];
    }
  }
  return result;
}

export function validation(message: string, field: string, fieldMessage: string): AppError {
  return { code: 'Validation', message, fieldErrors: { [field]: fieldMessage } };
}

export function conflict(message: string): AppError {
  return { code: 'Conflict', message };
}
