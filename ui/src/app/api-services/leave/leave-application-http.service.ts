import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap, tap, throwError } from 'rxjs';
import {
  AppError,
  CreateModel,
  PagedResult,
  PageRequest,
  UpdateModel,
  isAppError,
} from '../../core/models/common.models';
import { LeaveApplication } from '../../core/models/leave.models';
import {
  LeaveApplicationDataService,
  LeaveDecision,
} from '../../core/services/data/leave-data.services';
import { paginate, sortItems } from '../../shared/utilities/query.util';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

/** The API serves at most 200 rows per page. */
const PAGE_SIZE = 200;
/** Safety stop when paging through everything: 50 pages = 10,000 applications. */
const MAX_PAGES = 50;

const FILTER_KEYS = ['employeeId', 'leaveTypeId', 'dateFrom', 'dateTo'] as const;
const SORT_KEYS: readonly string[] = [
  'id',
  'applicationNo',
  'employeeId',
  'leaveTypeId',
  'startDate',
  'endDate',
  'days',
  'status',
  'appliedOn',
  'actionedOn',
];

const EDITABLE_KEYS = [
  'employeeId',
  'leaveTypeId',
  'startDate',
  'endDate',
  'halfDay',
  'reason',
  'documentName',
] as const;

type EditableFields = Pick<LeaveApplication, (typeof EDITABLE_KEYS)[number]>;

/** What the API returns: the screen model plus the concurrency token and the approver's name. */
type ApplicationApi = LeaveApplication & { rowVersion?: string; actionedByName?: string | null };

/**
 * HTTP implementation of the leave application contract.
 *
 *  - There is no unpaged endpoint: `getAll` pages `/query` (200 per page) until everything is read.
 *  - Create and update send only the fields the user owns. `days`, `applicationNo`, `status`,
 *    `appliedOn` and the approver fields are computed by the server and are never sent.
 *  - Update and delete are for drafts. They carry the `rowVersion` the adapter last saw (or reads it).
 *  - `submit`, `decide` and `cancel` are commands sent with `{ rowVersion, remarks }`. A rejection
 *    without remarks is refused locally with a field error on `remarks`.
 *  - The list screens lock `status` to several values (decision history). The API takes one status,
 *    so for such a list the adapter reads each status and merges, sorts and pages the result itself.
 */
@Injectable({ providedIn: 'root' })
export class LeaveApplicationHttpService implements LeaveApplicationDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly rowVersions = new Map<number, string>();

  private get url(): string {
    return `${this.baseUrl}/leave-applications`;
  }

  // ---- Reads --------------------------------------------------------------------------------

  getAll(): Observable<LeaveApplication[]> {
    return this.readAll(new HttpParams()).pipe(catchError((e) => this.fail(e)));
  }

  getById(id: number): Observable<LeaveApplication> {
    return this.http.get<ApplicationApi>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<Record<string, unknown>>): Observable<PagedResult<LeaveApplication>> {
    const statuses = statusList(request.filters?.['status']);
    if (statuses.length > 1) {
      return this.queryMany(request, statuses).pipe(catchError((e) => this.fail(e)));
    }
    const params = this.buildParams(request, statuses[0])
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    return this.fetchPage(params).pipe(catchError((e) => this.fail(e)));
  }

  // ---- Writes -------------------------------------------------------------------------------

  create(model: CreateModel<LeaveApplication>): Observable<LeaveApplication> {
    return this.http.post<ApplicationApi>(this.url, editableFields(model)).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  update(id: number, changes: UpdateModel<LeaveApplication>): Observable<LeaveApplication> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.versionFor(id, rowVersion).pipe(
      switchMap((version) => this.merged(id, changes).pipe(map((body) => ({ body, version })))),
      switchMap(({ body, version }) =>
        this.http.put<ApplicationApi>(`${this.url}/${id}`, { ...body, rowVersion: version }),
      ),
      tap((item) => this.remember(item)),
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

  submit(id: number): Observable<LeaveApplication> {
    return this.command(id, 'submit');
  }

  decide(id: number, decision: LeaveDecision, remarks: string): Observable<LeaveApplication> {
    const text = remarks.trim();
    if (decision === 'Rejected') {
      return text ? this.command(id, 'reject', text) : throwError(() => remarksRequired());
    }
    return this.command(id, 'approve', text || undefined);
  }

  cancel(id: number, remarks: string): Observable<LeaveApplication> {
    return this.command(id, 'cancel', remarks.trim() || undefined);
  }

  // ---- Helpers ------------------------------------------------------------------------------

  private command(id: number, name: string, remarks?: string): Observable<LeaveApplication> {
    return this.versionFor(id).pipe(
      switchMap((rowVersion) =>
        this.http.post<ApplicationApi>(`${this.url}/${id}/${name}`, {
          rowVersion,
          ...(remarks === undefined ? {} : { remarks }),
        }),
      ),
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  /** A PUT needs every create field: fill what the form did not send from the current record. */
  private merged(id: number, changes: UpdateModel<LeaveApplication>): Observable<EditableFields> {
    const patch = editableFields(changes, true);
    if (EDITABLE_KEYS.every((key) => key in patch)) {
      return of(patch as EditableFields);
    }
    return this.http.get<ApplicationApi>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((current) => ({ ...editableFields(current), ...patch }) as EditableFields),
    );
  }

  /** Explicit version, else the last one seen, else read the record to learn it. */
  private versionFor(id: number, explicit?: string): Observable<string> {
    const known = explicit ?? this.rowVersions.get(id);
    if (known) {
      return of(known);
    }
    return this.http.get<ApplicationApi>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((item) => item.rowVersion ?? ''),
    );
  }

  private remember(item: ApplicationApi): void {
    if (item.rowVersion) {
      this.rowVersions.set(item.id, item.rowVersion);
    }
  }

  private fetchPage(params: HttpParams): Observable<PagedResult<LeaveApplication>> {
    return this.http
      .get<PagedResult<ApplicationApi>>(`${this.url}/query`, { params })
      .pipe(tap((page) => page.items.forEach((item) => this.remember(item))));
  }

  /** Reads every page of a filtered query. */
  private readAll(base: HttpParams): Observable<LeaveApplication[]> {
    const loadPage = (pageIndex: number, acc: LeaveApplication[]): Observable<LeaveApplication[]> =>
      this.fetchPage(base.set('pageIndex', pageIndex).set('pageSize', PAGE_SIZE)).pipe(
        switchMap((page) => {
          const all = [...acc, ...page.items];
          return page.items.length === 0 || all.length >= page.total || pageIndex + 1 >= MAX_PAGES
            ? of(all)
            : loadPage(pageIndex + 1, all);
        }),
      );
    return loadPage(0, []);
  }

  /** Several statuses: read each, then merge, sort and page here. */
  private queryMany(
    request: PageRequest<Record<string, unknown>>,
    statuses: string[],
  ): Observable<PagedResult<LeaveApplication>> {
    const reads = statuses.map((status) => this.readAll(this.buildParams(request, status)));
    return forkJoin(reads).pipe(
      map((lists) =>
        paginate(sortItems(lists.flat(), request.sort), request.pageIndex, request.pageSize),
      ),
    );
  }

  private buildParams(
    request: PageRequest<Record<string, unknown>>,
    status: string | undefined,
  ): HttpParams {
    let params = new HttpParams();
    if (request.sort?.active && request.sort.direction && SORT_KEYS.includes(request.sort.active)) {
      params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    const values: [string, unknown][] = [
      ...FILTER_KEYS.map((key) => [key, request.filters?.[key]] as [string, unknown]),
      ['status', status],
    ];
    for (const [key, value] of values) {
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
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}

/**
 * The fields of a create/update body. With `partial`, only keys present in the source are kept.
 * Everything else (days, applicationNo, status, appliedOn, approver fields, rowVersion) is dropped.
 */
function editableFields(source: object, partial = false): Partial<EditableFields> {
  const from = source as Record<string, unknown>;
  const result: Record<string, unknown> = {};
  for (const key of EDITABLE_KEYS) {
    if (partial && !(key in from)) {
      continue;
    }
    result[key] = key === 'documentName' ? (from[key] as string | null) || null : from[key];
  }
  return result as Partial<EditableFields>;
}

/** The status filter as a list: a single value, an array (locked screens) or nothing. */
function statusList(value: unknown): string[] {
  const values = Array.isArray(value)
    ? value
    : value === undefined || value === null
      ? []
      : [value];
  return values.map((v) => String(v).trim()).filter((v) => v !== '' && v.toLowerCase() !== 'all');
}

function remarksRequired(): AppError {
  return {
    code: 'Validation',
    message: 'Give a reason for rejecting the request.',
    fieldErrors: { remarks: 'A reason is required.' },
  };
}
