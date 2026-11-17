import { HttpClient, HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, switchMap, throwError } from 'rxjs';
import { AppError, PagedResult, PageRequest } from '../../core/models/common.models';
import {
  AssignKpiRequest,
  AssignKpiResult,
  BulkAssignKpiRequest,
  BulkAssignmentResult,
  EvidenceDownload,
  EvidenceRemovalResult,
  EvidenceUploadResult,
  ReviewDecision,
  SaveResultsRequest,
  SaveReviewRequest,
  Scorecard,
  ScorecardDetail,
  ScorecardFilter,
  ScorecardItem,
  ScorecardItemUpdate,
} from '../../core/models/kpi.models';
import { ScorecardDataService } from '../../core/services/data/kpi-data.services';
import { readFileBytes } from '../../core/utilities/kpi-evidence';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';
import { buildPageParams, fileNameFromDisposition } from './kpi-http.util';

const SORT_KEYS: readonly string[] = [
  'id',
  'employeeCode',
  'employeeName',
  'periodName',
  'departmentName',
  'status',
  'overallScore',
  'planFinalizedOn',
  'modifiedAt',
];
const FILTER_KEYS = ['periodId', 'employeeId', 'departmentId', 'companyId', 'status'] as const;
const REVIEW_QUEUE_FILTER_KEYS = ['periodId', 'departmentId', 'status'] as const;

/** Used when the download answer carries no usable `Content-Disposition`. */
const FALLBACK_FILE_NAME = 'evidence';

/**
 * HTTP implementation of the scorecard contract (`/api/kpi/...`).
 *
 * Every write sends the scorecard's `rowVersion` that the caller passes in (the caller always holds
 * the latest one from the last answer), so nothing is cached here. Notes:
 *  - `assign` reads the status code: 201 means the call created the scorecard, 200 that it added items.
 *  - `returnForRevision` and `reject` refuse a blank reason locally (field error on `comment`).
 *  - Evidence is sent as `multipart/form-data` (`rowVersion`, `file`); the browser sets the
 *    Content-Type with its boundary, so it is never set here.
 *  - A download is read as a Blob; the name comes from `Content-Disposition`.
 */
@Injectable({ providedIn: 'root' })
export class ScorecardHttpService implements ScorecardDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/kpi`;
  }

  // ---- Reads ----------------------------------------------------------------------------------

  query(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.http
      .get<PagedResult<Scorecard>>(`${this.url}/scorecards/query`, {
        params: buildPageParams(request, SORT_KEYS, FILTER_KEYS),
      })
      .pipe(catchError((e) => this.fail(e)));
  }

  getById(id: number): Observable<ScorecardDetail> {
    return this.http
      .get<ScorecardDetail>(`${this.url}/scorecards/${id}`)
      .pipe(catchError((e) => this.fail(e)));
  }

  myScorecards(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.http
      .get<PagedResult<Scorecard>>(`${this.url}/my-scorecards`, {
        params: buildPageParams(request, SORT_KEYS, FILTER_KEYS),
      })
      .pipe(catchError((e) => this.fail(e)));
  }

  pendingReviews(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.http
      .get<PagedResult<Scorecard>>(`${this.url}/reviews/pending`, {
        params: buildPageParams(request, SORT_KEYS, REVIEW_QUEUE_FILTER_KEYS),
      })
      .pipe(catchError((e) => this.fail(e)));
  }

  // ---- Assignment and plan --------------------------------------------------------------------

  assign(request: AssignKpiRequest): Observable<AssignKpiResult> {
    return this.http
      .post<ScorecardDetail>(`${this.url}/assignments`, request, { observe: 'response' })
      .pipe(
        map((response: HttpResponse<ScorecardDetail>) => ({
          scorecard: response.body as ScorecardDetail,
          created: response.status === 201,
        })),
        catchError((e) => this.fail(e)),
      );
  }

  bulkAssign(request: BulkAssignKpiRequest): Observable<BulkAssignmentResult> {
    return this.http
      .post<BulkAssignmentResult>(`${this.url}/assignments/bulk`, request)
      .pipe(catchError((e) => this.fail(e)));
  }

  updateItem(itemId: number, changes: ScorecardItemUpdate): Observable<ScorecardItem> {
    const { target, weight, startDate, endDate, sortOrder, rowVersion } = changes;
    return this.http
      .put<ScorecardItem>(`${this.url}/scorecard-items/${itemId}`, {
        target,
        weight,
        startDate,
        endDate,
        ...(sortOrder === undefined || sortOrder === null ? {} : { sortOrder }),
        rowVersion,
      })
      .pipe(
        // The item answer carries no evidence list: it is only filled in the scorecard detail.
        map((item) => ({ ...item, attachments: item.attachments ?? [] })),
        catchError((e) => this.fail(e)),
      );
  }

  deleteItem(itemId: number, rowVersion: string): Observable<void> {
    return this.http
      .delete<void>(`${this.url}/scorecard-items/${itemId}`, { body: { rowVersion } })
      .pipe(
        map(() => undefined),
        catchError((e) => this.fail(e)),
      );
  }

  deleteScorecard(id: number, rowVersion: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/scorecards/${id}`, { body: { rowVersion } }).pipe(
      map(() => undefined),
      catchError((e) => this.fail(e)),
    );
  }

  finalizePlan(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.command(id, 'finalize-plan', { rowVersion });
  }

  // ---- Employee -------------------------------------------------------------------------------

  saveResults(id: number, request: SaveResultsRequest): Observable<ScorecardDetail> {
    return this.http
      .put<ScorecardDetail>(`${this.url}/scorecards/${id}/results`, request)
      .pipe(catchError((e) => this.fail(e)));
  }

  submit(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.command(id, 'submit', { rowVersion });
  }

  // ---- Review ---------------------------------------------------------------------------------

  startReview(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.command(id, 'start-review', { rowVersion });
  }

  saveReview(id: number, request: SaveReviewRequest): Observable<ScorecardDetail> {
    return this.http
      .put<ScorecardDetail>(`${this.url}/scorecards/${id}/review`, request)
      .pipe(catchError((e) => this.fail(e)));
  }

  approve(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.command(id, 'approve', this.decisionBody(decision));
  }

  returnForRevision(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.reasoned(id, 'return', decision);
  }

  reject(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.reasoned(id, 'reject', decision);
  }

  finalize(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.command(id, 'finalize', this.decisionBody(decision));
  }

  // ---- Evidence -------------------------------------------------------------------------------

  uploadEvidence(itemId: number, rowVersion: string, file: File): Observable<EvidenceUploadResult> {
    const body = new FormData();
    body.append('rowVersion', rowVersion);
    body.append('file', file, file.name);
    return this.http
      .post<EvidenceUploadResult>(`${this.url}/scorecard-items/${itemId}/attachments`, body)
      .pipe(catchError((e) => this.fail(e)));
  }

  downloadEvidence(attachmentId: number): Observable<EvidenceDownload> {
    return this.http
      .get(`${this.url}/attachments/${attachmentId}`, { observe: 'response', responseType: 'blob' })
      .pipe(
        map((response) => ({
          blob: response.body as Blob,
          fileName:
            fileNameFromDisposition(response.headers.get('Content-Disposition')) ??
            FALLBACK_FILE_NAME,
        })),
        // An error answer to a blob request is a Blob too: read its problem body first.
        catchError((e) => this.failFromBlob(e)),
      );
  }

  deleteEvidence(attachmentId: number, rowVersion: string): Observable<EvidenceRemovalResult> {
    return this.http
      .delete<EvidenceRemovalResult>(`${this.url}/attachments/${attachmentId}`, {
        body: { rowVersion },
      })
      .pipe(catchError((e) => this.fail(e)));
  }

  // ---- Helpers --------------------------------------------------------------------------------

  private command(id: number, name: string, body: object): Observable<ScorecardDetail> {
    return this.http
      .post<ScorecardDetail>(`${this.url}/scorecards/${id}/${name}`, body)
      .pipe(catchError((e) => this.fail(e)));
  }

  /** The comment is optional here: only a non-blank one is sent. */
  private decisionBody(decision: ReviewDecision): { rowVersion: string; comment?: string } {
    const comment = decision.comment?.trim();
    return { rowVersion: decision.rowVersion, ...(comment ? { comment } : {}) };
  }

  /** Return and reject need a reason: a blank one is refused here, as the API would. */
  private reasoned(
    id: number,
    name: 'return' | 'reject',
    decision: ReviewDecision,
  ): Observable<ScorecardDetail> {
    const comment = decision.comment?.trim();
    if (!comment) {
      return throwError(() => reasonRequired());
    }
    return this.command(id, name, { rowVersion: decision.rowVersion, comment });
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => mapHttpError(error));
  }

  private failFromBlob(error: unknown): Observable<never> {
    if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
      const original = error;
      return from(readFileBytes(error.error)).pipe(
        map((bytes) => {
          try {
            return JSON.parse(new TextDecoder().decode(bytes)) as unknown;
          } catch {
            return null;
          }
        }),
        switchMap((body) =>
          this.fail(
            new HttpErrorResponse({
              error: body,
              status: original.status,
              statusText: original.statusText,
              url: original.url ?? undefined,
              headers: original.headers,
            }),
          ),
        ),
      );
    }
    return this.fail(error);
  }
}

function reasonRequired(): AppError {
  return {
    code: 'Validation',
    message: 'A reason is required.',
    fieldErrors: { comment: 'A reason is required.' },
  };
}
