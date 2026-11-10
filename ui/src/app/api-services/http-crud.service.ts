import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import {
  CreateModel,
  Entity,
  PagedResult,
  PageRequest,
  RecordStatus,
  UpdateModel,
} from '../core/models/common.models';
import { CrudDataService } from '../core/services/data/crud-data.service';
import { API_BASE_URL } from './api-base-url';
import { mapHttpError } from './http-error.mapper';

/** What the API returns for every organization resource. */
export interface ApiEntity extends Entity {
  status: RecordStatus;
  rowVersion?: string;
}

/** The API rejects search text longer than this. */
export const MAX_SEARCH_LENGTH = 100;

/**
 * Shared HTTP implementation of a CRUD contract against one API resource
 * (`{API_BASE_URL}/{resource}`). Subclasses only name the resource and its filters.
 *
 * Concurrency: the API requires the `rowVersion` of the record on update, delete and the status
 * commands. The adapter remembers the latest `rowVersion` it has seen for each id (from every
 * response), so a save made from a stale list is rejected by the API with 409 instead of silently
 * overwriting. When it has never seen the record it fetches it first.
 */
export abstract class HttpCrudService<T extends ApiEntity> implements CrudDataService<T> {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly rowVersions = new Map<number, string>();

  /** Route segment, for example `companies`. */
  protected abstract readonly resource: string;
  /** Filter keys the API accepts for this resource. Others are not sent. */
  protected abstract readonly filterKeys: readonly string[];
  /** When set, only these sort keys are sent (the API rejects unknown keys); others are dropped. */
  protected readonly sortKeys?: readonly string[];

  private get url(): string {
    return `${this.baseUrl}/${this.resource}`;
  }

  getAll(): Observable<T[]> {
    return this.http.get<T[]>(this.url).pipe(
      tap((items) => items.forEach((item) => this.remember(item))),
      catchError((e) => this.fail(e)),
    );
  }

  getById(id: number): Observable<T> {
    return this.http.get<T>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<Record<string, unknown>>): Observable<PagedResult<T>> {
    return this.http
      .get<PagedResult<T>>(`${this.url}/query`, { params: this.buildParams(request) })
      .pipe(
        tap((page) => page.items.forEach((item) => this.remember(item))),
        catchError((e) => this.fail(e)),
      );
  }

  create(model: CreateModel<T>): Observable<T> {
    // Create carries the starting status; the server assigns id and rowVersion.
    const body = { ...(model as Record<string, unknown>) };
    delete body['rowVersion'];
    return this.http.post<T>(this.url, body).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  /**
   * RULE: the API never accepts a partial PUT, and status is not part of the PUT body.
   *
   *  - Status-only patch (what the list's activate/deactivate action sends): calls
   *    `POST {id}/activate` or `POST {id}/deactivate` with the current rowVersion.
   *  - Patch with other fields: sends PUT with those fields (without `status`) plus the rowVersion.
   *    The UI edit form sends the whole record, so this is the full update the API expects.
   *    If the patch also carries a status that differs from the result, the matching command
   *    follows with the new rowVersion.
   */
  update(id: number, changes: UpdateModel<T>): Observable<T> {
    const { rowVersion, status, ...fields } = changes as Record<string, unknown> & {
      rowVersion?: string;
      status?: RecordStatus;
    };
    delete fields['id'];

    if (Object.keys(fields).length === 0) {
      return status === undefined ? this.getById(id) : this.changeStatus(id, status, rowVersion);
    }

    return this.versionFor(id, rowVersion).pipe(
      switchMap((version) =>
        this.http.put<T>(`${this.url}/${id}`, { ...fields, rowVersion: version }),
      ),
      tap((item) => this.remember(item)),
      switchMap((item) =>
        status === undefined || item.status === status
          ? of(item)
          : this.changeStatus(id, status, item.rowVersion),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  delete(id: number): Observable<void> {
    return this.versionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.delete<void>(`${this.url}/${id}`, { body: { rowVersion } }),
      ),
      tap(() => this.rowVersions.delete(id)),
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  private changeStatus(id: number, status: RecordStatus, rowVersion?: string): Observable<T> {
    const command = status === 'Active' ? 'activate' : 'deactivate';
    return this.versionFor(id, rowVersion).pipe(
      switchMap((version) =>
        this.http.post<T>(`${this.url}/${id}/${command}`, { rowVersion: version }),
      ),
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  /** Explicit version, else the last one seen, else fetch the record to learn it. */
  private versionFor(id: number, explicit?: string): Observable<string> {
    const known = explicit ?? this.rowVersions.get(id);
    if (known) {
      return of(known);
    }
    return this.http.get<T>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((item) => item.rowVersion ?? ''),
    );
  }

  private remember(item: T): void {
    if (item.rowVersion) {
      this.rowVersions.set(item.id, item.rowVersion);
    }
  }

  private buildParams(request: PageRequest<Record<string, unknown>>): HttpParams {
    let params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    if (
      request.sort?.active &&
      request.sort.direction &&
      (!this.sortKeys || this.sortKeys.includes(request.sort.active))
    ) {
      params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    for (const key of this.filterKeys) {
      const value = request.filters?.[key];
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

  private fail(error: unknown): Observable<never> {
    return throwError(() => mapHttpError(error));
  }
}
