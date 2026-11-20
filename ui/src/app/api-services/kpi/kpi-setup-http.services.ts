import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import {
  CreateModel,
  PagedResult,
  PageRequest,
  UpdateModel,
} from '../../core/models/common.models';
import {
  EvaluationPeriod,
  EvaluationPeriodOption,
  KpiCategory,
  KpiCategoryOption,
  KpiTemplate,
  RatingBand,
} from '../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiTemplateDataService,
  RatingBandDataService,
} from '../../core/services/data/kpi-data.services';
import { API_BASE_URL } from '../api-base-url';
import { HttpCrudService } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';
import { buildPageParams } from './kpi-http.util';

// HTTP implementations of the KPI master-data contracts. Categories, templates and rating bands use
// the shared CRUD adapter: status changes go through activate/deactivate and every PUT/DELETE carries
// the rowVersion (see HttpCrudService). Periods have their own adapter because their status moves
// through the activate, complete and close commands instead.

@Injectable({ providedIn: 'root' })
export class KpiCategoryHttpService
  extends HttpCrudService<KpiCategory>
  implements KpiCategoryDataService
{
  protected readonly resource = 'kpi/categories';
  protected readonly filterKeys = ['status'] as const;
  protected override readonly sortKeys = ['id', 'code', 'name', 'status'] as const;
  private readonly client = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);

  /** `GET /api/kpi/categories/options` (kpi:View). */
  options(): Observable<KpiCategoryOption[]> {
    return this.client
      .get<KpiCategoryOption[]>(`${this.apiBase}/kpi/categories/options`)
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }
}

@Injectable({ providedIn: 'root' })
export class KpiTemplateHttpService
  extends HttpCrudService<KpiTemplate>
  implements KpiTemplateDataService
{
  protected readonly resource = 'kpi/templates';
  protected readonly filterKeys = [
    'status',
    'categoryId',
    'departmentId',
    'scoringMethod',
  ] as const;
  protected override readonly sortKeys = [
    'id',
    'code',
    'name',
    'categoryId',
    'defaultWeight',
    'scoringMethod',
    'frequency',
    'status',
  ] as const;
}

@Injectable({ providedIn: 'root' })
export class RatingBandHttpService
  extends HttpCrudService<RatingBand>
  implements RatingBandDataService
{
  protected readonly resource = 'kpi/rating-bands';
  protected readonly filterKeys = ['status'] as const;
  protected override readonly sortKeys = ['id', 'label', 'minScore', 'status'] as const;
}

// ---- Evaluation periods ---------------------------------------------------------------------

const FILTER_KEYS = ['companyId', 'periodType', 'status', 'year'] as const;
const SORT_KEYS: readonly string[] = [
  'id',
  'name',
  'companyId',
  'periodType',
  'startDate',
  'endDate',
  'status',
];

/** The fields of a create/update body. `status` is never sent: the commands move it. */
const EDITABLE_KEYS = [
  'companyId',
  'name',
  'periodType',
  'startDate',
  'endDate',
  'submissionDueDate',
  'requiresHrReview',
] as const;

type EditableFields = Pick<EvaluationPeriod, (typeof EDITABLE_KEYS)[number]>;

function editable(source: object, partial = false): Partial<EditableFields> {
  const from = source as Record<string, unknown>;
  const result: Record<string, unknown> = {};
  for (const key of EDITABLE_KEYS) {
    if (partial && !(key in from)) {
      continue;
    }
    result[key] = key === 'submissionDueDate' ? ((from[key] as string | null) ?? null) : from[key];
  }
  return result as Partial<EditableFields>;
}

/**
 * HTTP implementation of the evaluation period contract (`/api/kpi/periods`).
 *
 *  - Create sends the editable fields only (a period always starts in Draft).
 *  - Update sends the whole editable set plus the rowVersion; a partial change is completed from the
 *    current record. The status is ignored: only `activate`, `complete` and `close` move it.
 *  - Commands send `{ rowVersion }`: the explicit one, else the last one seen, else the record is read first.
 */
@Injectable({ providedIn: 'root' })
export class EvaluationPeriodHttpService implements EvaluationPeriodDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly rowVersions = new Map<number, string>();

  private get url(): string {
    return `${this.baseUrl}/kpi/periods`;
  }

  getAll(): Observable<EvaluationPeriod[]> {
    return this.http.get<EvaluationPeriod[]>(this.url).pipe(
      tap((items) => items.forEach((item) => this.remember(item))),
      catchError((e) => this.fail(e)),
    );
  }

  getById(id: number): Observable<EvaluationPeriod> {
    return this.http.get<EvaluationPeriod>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  query(request: PageRequest<object>): Observable<PagedResult<EvaluationPeriod>> {
    return this.http
      .get<PagedResult<EvaluationPeriod>>(`${this.url}/query`, {
        params: buildPageParams(request, SORT_KEYS, FILTER_KEYS),
      })
      .pipe(
        tap((page) => page.items.forEach((item) => this.remember(item))),
        catchError((e) => this.fail(e)),
      );
  }

  create(model: CreateModel<EvaluationPeriod>): Observable<EvaluationPeriod> {
    return this.http.post<EvaluationPeriod>(this.url, editable(model)).pipe(
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  update(id: number, changes: UpdateModel<EvaluationPeriod>): Observable<EvaluationPeriod> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.versionFor(id, rowVersion).pipe(
      switchMap((version) => this.merged(id, changes).pipe(map((body) => ({ body, version })))),
      switchMap(({ body, version }) =>
        this.http.put<EvaluationPeriod>(`${this.url}/${id}`, { ...body, rowVersion: version }),
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

  activate(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.command(id, 'activate', rowVersion);
  }

  complete(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.command(id, 'complete', rowVersion);
  }

  close(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.command(id, 'close', rowVersion);
  }

  options(companyId?: number | null): Observable<EvaluationPeriodOption[]> {
    let params = new HttpParams();
    if (companyId !== undefined && companyId !== null) {
      params = params.set('companyId', companyId);
    }
    return this.http
      .get<EvaluationPeriodOption[]>(`${this.url}/options`, { params })
      .pipe(catchError((e) => this.fail(e)));
  }

  private command(
    id: number,
    name: 'activate' | 'complete' | 'close',
    explicit?: string,
  ): Observable<EvaluationPeriod> {
    return this.versionFor(id, explicit).pipe(
      switchMap((rowVersion) =>
        this.http.post<EvaluationPeriod>(`${this.url}/${id}/${name}`, { rowVersion }),
      ),
      tap((item) => this.remember(item)),
      catchError((e) => this.fail(e)),
    );
  }

  /** A PUT needs every field: fill what the caller left out from the current record. */
  private merged(id: number, changes: UpdateModel<EvaluationPeriod>): Observable<EditableFields> {
    const patch = editable(changes, true);
    if (EDITABLE_KEYS.every((key) => key in patch)) {
      return of(patch as EditableFields);
    }
    return this.http.get<EvaluationPeriod>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((current) => ({ ...editable(current), ...patch }) as EditableFields),
    );
  }

  private versionFor(id: number, explicit?: string): Observable<string> {
    const known = explicit ?? this.rowVersions.get(id);
    if (known) {
      return of(known);
    }
    return this.http.get<EvaluationPeriod>(`${this.url}/${id}`).pipe(
      tap((item) => this.remember(item)),
      map((item) => item.rowVersion ?? ''),
    );
  }

  private remember(item: EvaluationPeriod): void {
    if (item.rowVersion) {
      this.rowVersions.set(item.id, item.rowVersion);
    }
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => mapHttpError(error));
  }
}
