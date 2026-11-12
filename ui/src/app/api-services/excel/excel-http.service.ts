import { HttpClient, HttpErrorResponse, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  EMPTY,
  Observable,
  catchError,
  expand,
  from,
  map,
  of,
  reduce,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import { AppError, PagedResult, isAppError } from '../../core/models/common.models';
import {
  ExcelFile,
  ExcelModuleInfo,
  ExportRequest,
  FileInspection,
  ImportBatchDetail,
  ImportBatchSummary,
  ImportMode,
  ImportPreview,
  ImportResult,
  ImportValidateRequest,
} from '../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../core/services/data/excel-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

const HISTORY_PAGE_SIZE = 100;
/** Safety stop for the history loop (the screen shows one flat list). */
const HISTORY_MAX_PAGES = 20;

/** Reads the file name from a Content-Disposition header (RFC 5987 `filename*` first). */
export function fileNameFromDisposition(header: string | null, fallback: string): string {
  if (!header) {
    return fallback;
  }
  const encoded = /filename\*\s*=\s*utf-8''([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // fall through to the plain form
    }
  }
  const plain = /filename\s*=\s*(?:"([^"]+)"|([^;]+))/i.exec(header);
  const name = (plain?.[1] ?? plain?.[2] ?? '').trim();
  return name.length > 0 ? name : fallback;
}

function toAppError(error: unknown): AppError {
  const mapped = mapHttpError(error);
  // A rejected file or mapping explains itself better than the generic validation text.
  const specific = mapped.fieldErrors?.['file'] ?? mapped.fieldErrors?.['mapping'];
  return specific ? { ...mapped, message: specific } : mapped;
}

/**
 * Maps a failed Excel call to an `AppError`. A failed blob request carries the problem+json as a
 * Blob, so it is read as text first. The request body (the uploaded file) is never read or kept.
 */
export function excelError(error: unknown): Observable<never> {
  if (isAppError(error)) {
    return throwError(() => error);
  }
  if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
    return from(error.error.text()).pipe(
      map((text): unknown => {
        let body: unknown = null;
        try {
          body = JSON.parse(text);
        } catch {
          // not JSON: the status alone decides the message
        }
        return new HttpErrorResponse({
          error: body,
          status: error.status,
          statusText: error.statusText,
        });
      }),
      catchError(() => of<unknown>(new HttpErrorResponse({ status: error.status }))),
      switchMap((rebuilt) => throwError(() => toAppError(rebuilt))),
    );
  }
  return throwError(() => toAppError(error));
}

const TOKEN = /^[A-Za-z0-9_-]+$/;
const NOT_FOUND: AppError = { code: 'NotFound', message: 'This item was not found.' };

/** Module codes and batch ids go into the path: anything but a plain token is refused. */
function segment(value: string): string | null {
  return TOKEN.test(value) ? value : null;
}

function download(
  response: Observable<HttpResponse<Blob>>,
  fallbackName: string,
): Observable<ExcelFile> {
  return response.pipe(
    map((r) => ({
      fileName: fileNameFromDisposition(r.headers.get('Content-Disposition'), fallbackName),
      blob: r.body ?? new Blob(),
    })),
    catchError(excelError),
  );
}

interface ApiCatalog {
  modules?: ExcelModuleInfo[];
}

/** Modules the API offers. Its `unavailable` list is not used (no screen lists modules). */
@Injectable({ providedIn: 'root' })
export class ExcelCatalogHttpService implements ExcelModuleCatalogDataService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/excel`;

  getModules(): Observable<ExcelModuleInfo[]> {
    return this.http.get<ApiCatalog>(`${this.url}/modules`).pipe(
      map((catalog) => catalog.modules ?? []),
      catchError(excelError),
    );
  }
}

/**
 * HTTP import. The validate route is per module but the contract's request only carries the
 * upload id, so the module of every upload is remembered from `inspect` (in memory only).
 */
@Injectable({ providedIn: 'root' })
export class ExcelImportHttpService implements ExcelImportDataService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/excel`;
  private readonly moduleByUpload = new Map<string, string>();

  getTemplate(moduleCode: string): Observable<ExcelFile> {
    const code = segment(moduleCode);
    if (!code) {
      return throwError(() => NOT_FOUND);
    }
    return download(
      this.http.get(`${this.url}/templates/${code}`, {
        observe: 'response',
        responseType: 'blob',
      }),
      `${code}-import-template.xlsx`,
    );
  }

  inspect(moduleCode: string, file: File): Observable<FileInspection> {
    const code = segment(moduleCode);
    if (!code) {
      return throwError(() => NOT_FOUND);
    }
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<FileInspection>(`${this.url}/imports/${code}/inspect`, body).pipe(
      tap((inspection) => this.moduleByUpload.set(inspection.uploadId, code)),
      catchError(excelError),
    );
  }

  validate(request: ImportValidateRequest): Observable<ImportPreview> {
    const code = this.moduleByUpload.get(request.uploadId);
    if (!code) {
      return throwError(
        () =>
          ({
            code: 'NotFound',
            message: 'This upload has expired. Upload the file again.',
          }) satisfies AppError,
      );
    }
    return this.http
      .post<ImportPreview>(`${this.url}/imports/${code}/validate`, {
        uploadId: request.uploadId,
        strategy: request.strategy,
        mapping: request.mapping,
      })
      .pipe(catchError(excelError));
  }

  execute(batchId: string, mode: ImportMode): Observable<ImportResult> {
    const id = segment(batchId);
    if (!id) {
      return throwError(() => NOT_FOUND);
    }
    return this.http
      .post<ImportResult>(`${this.url}/imports/${id}/execute`, { mode })
      .pipe(catchError(excelError));
  }

  /** The screen shows one flat list, so every page is read (newest first, as served). */
  getHistory(): Observable<ImportBatchSummary[]> {
    const page = (pageIndex: number) =>
      this.http.get<PagedResult<ImportBatchSummary>>(`${this.url}/imports/history`, {
        params: new HttpParams().set('pageIndex', pageIndex).set('pageSize', HISTORY_PAGE_SIZE),
      });
    return page(0).pipe(
      expand((result) => {
        const items = result.items ?? [];
        const next = result.pageIndex + 1;
        const more = items.length > 0 && next * HISTORY_PAGE_SIZE < result.total;
        return more && next < HISTORY_MAX_PAGES ? page(next) : EMPTY;
      }),
      reduce((all: ImportBatchSummary[], result) => all.concat(result.items ?? []), []),
      catchError(excelError),
    );
  }

  getBatch(batchId: string): Observable<ImportBatchDetail> {
    const id = segment(batchId);
    if (!id) {
      return throwError(() => NOT_FOUND);
    }
    return this.http
      .get<ImportBatchDetail>(`${this.url}/imports/${id}`)
      .pipe(catchError(excelError));
  }

  getErrorReport(batchId: string): Observable<ExcelFile> {
    const id = segment(batchId);
    if (!id) {
      return throwError(() => NOT_FOUND);
    }
    return download(
      this.http.get(`${this.url}/imports/${id}/error-report`, {
        observe: 'response',
        responseType: 'blob',
      }),
      `import-report-${id}.xlsx`,
    );
  }
}

const SCOPES = { filtered: 'Filtered', all: 'All', selected: 'Selected' } as const;

@Injectable({ providedIn: 'root' })
export class ExcelExportHttpService implements ExcelExportDataService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/excel`;

  /** A too-large export is a 400 with a message: there is no background export. */
  export(request: ExportRequest): Observable<ExcelFile> {
    const code = segment(request.moduleCode);
    if (!code) {
      return throwError(() => NOT_FOUND);
    }
    const { moduleCode: _module, scope, ...rest } = request;
    void _module;
    return download(
      this.http.post(
        `${this.url}/exports/${code}`,
        { ...rest, scope: SCOPES[scope] },
        { observe: 'response', responseType: 'blob' },
      ),
      `${code}-export.xlsx`,
    );
  }
}
