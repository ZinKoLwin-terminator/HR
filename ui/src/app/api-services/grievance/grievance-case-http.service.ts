import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import {
  AcknowledgeGrievanceRequest,
  ApproveResolutionRequest,
  AssignInvestigatorRequest,
  EscalateGrievanceRequest,
  GRIEVANCE_CASE_FILTER_KEYS,
  GRIEVANCE_CASE_SORT_KEYS,
  GrievanceAttachmentDownload,
  GrievanceAttachmentUploadResult,
  GrievanceAttachmentVisibility,
  GrievanceAuditEntry,
  GrievanceAuditFilter,
  GrievanceCase,
  GrievanceCaseDetail,
  GrievanceCaseFilter,
  GrievanceCaseMessageRequest,
  GrievanceCommand,
  GrievanceCorrectiveAction,
  GrievanceCorrectiveActionInput,
  GrievanceCorrectiveActionUpdate,
  GrievanceEmployeeOption,
  GrievanceHistoryEntry,
  GrievanceInvestigatorOption,
  GrievanceNoteRequest,
  GrievanceReasonCommand,
  GrievanceTriageRequest,
  ProposeResolutionRequest,
  RequestGrievanceInfoRequest,
  UnassignInvestigatorRequest,
} from '../../core/models/grievance.models';
import { GrievanceCaseDataService } from '../../core/services/data/grievance-data.services';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { buildPageParams } from '../kpi/kpi-http.util';
import { downloadGrievanceFile, failGrievance, uploadForm } from './grievance-http.support';

const AUDIT_FILTER_KEYS = ['action', 'from', 'to'] as const;

/** `?search=` when there is a search text (cut to what the API accepts), else nothing. */
function searchParams(search: string | null | undefined): HttpParams {
  const text = search?.trim().slice(0, MAX_SEARCH_LENGTH);
  return text ? new HttpParams().set('search', text) : new HttpParams();
}

/**
 * HTTP implementation of the case-team contract (`/api/grievances/cases...`, `/investigations`,
 * `/reviews/pending`, `/attachments`, `/case-actions`, `/investigators`, `/employees`). The 14
 * workflow commands are `POST /cases/{id}/{action}` with the request body as it is (camelCase, the
 * API's bodies); every answer is the case detail with its new rowVersion. Uploads are multipart
 * (`rowVersion`, `visibility`, `file`), downloads Blobs.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceCaseHttpService implements GrievanceCaseDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/grievances`;
  }

  // ---- Reads ----------------------------------------------------------------------------------

  query(request: PageRequest<GrievanceCaseFilter>): Observable<PagedResult<GrievanceCase>> {
    return this.list('cases/query', request);
  }

  investigations(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>> {
    return this.list('investigations', request);
  }

  pendingReviews(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>> {
    return this.list('reviews/pending', request);
  }

  getById(id: number): Observable<GrievanceCaseDetail> {
    return this.http
      .get<GrievanceCaseDetail>(`${this.url}/cases/${id}`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  history(id: number): Observable<GrievanceHistoryEntry[]> {
    return this.http
      .get<GrievanceHistoryEntry[]>(`${this.url}/cases/${id}/history`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  audit(
    id: number,
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>> {
    return this.http
      .get<PagedResult<GrievanceAuditEntry>>(`${this.url}/cases/${id}/audit`, {
        // The access log's order is fixed (newest first): no sort is sent.
        params: buildPageParams({ ...request, sort: undefined }, [], AUDIT_FILTER_KEYS),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  auditLog(
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>> {
    return this.http
      .get<PagedResult<GrievanceAuditEntry>>(`${this.url}/audit`, {
        // The access log's order is fixed (newest first): no sort is sent.
        params: buildPageParams({ ...request, sort: undefined }, [], AUDIT_FILTER_KEYS),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  investigators(search?: string | null): Observable<GrievanceInvestigatorOption[]> {
    return this.http
      .get<GrievanceInvestigatorOption[]>(`${this.url}/investigators`, {
        params: searchParams(search),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  employeeOptions(search: string): Observable<GrievanceEmployeeOption[]> {
    return this.http
      .get<GrievanceEmployeeOption[]>(`${this.url}/employees`, { params: searchParams(search) })
      .pipe(catchError((e) => failGrievance(e)));
  }

  // ---- State machine --------------------------------------------------------------------------

  acknowledge(id: number, request: AcknowledgeGrievanceRequest): Observable<GrievanceCaseDetail> {
    return this.command(id, 'acknowledge', request);
  }

  startReview(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'start-review', request);
  }

  assign(id: number, request: AssignInvestigatorRequest): Observable<GrievanceCaseDetail> {
    return this.command(id, 'assign', request);
  }

  unassign(id: number, request: UnassignInvestigatorRequest): Observable<GrievanceCaseDetail> {
    return this.command(id, 'unassign', request);
  }

  startInvestigation(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'start-investigation', request);
  }

  requestInfo(id: number, request: RequestGrievanceInfoRequest): Observable<GrievanceCaseDetail> {
    return this.command(id, 'request-info', request);
  }

  proposeResolution(
    id: number,
    request: ProposeResolutionRequest,
  ): Observable<GrievanceCaseDetail> {
    return this.command(id, 'propose-resolution', request);
  }

  approveResolution(
    id: number,
    request: ApproveResolutionRequest,
  ): Observable<GrievanceCaseDetail> {
    return this.command(id, 'approve-resolution', request);
  }

  rejectResolution(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'reject-resolution', request);
  }

  reject(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'reject', request);
  }

  close(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'close', request);
  }

  reopen(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'reopen', request);
  }

  escalate(id: number, request: EscalateGrievanceRequest): Observable<GrievanceCaseDetail> {
    return this.command(id, 'escalate', request);
  }

  archive(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.command(id, 'archive', request);
  }

  // ---- Case work ------------------------------------------------------------------------------

  triage(id: number, request: GrievanceTriageRequest): Observable<GrievanceCaseDetail> {
    return this.http
      .put<GrievanceCaseDetail>(`${this.url}/cases/${id}/triage`, request)
      .pipe(catchError((e) => failGrievance(e)));
  }

  sendMessage(id: number, request: GrievanceCaseMessageRequest): Observable<GrievanceCaseDetail> {
    return this.http
      .post<GrievanceCaseDetail>(`${this.url}/cases/${id}/messages`, request)
      .pipe(catchError((e) => failGrievance(e)));
  }

  addNote(id: number, request: GrievanceNoteRequest): Observable<GrievanceCaseDetail> {
    return this.http
      .post<GrievanceCaseDetail>(`${this.url}/cases/${id}/notes`, request)
      .pipe(catchError((e) => failGrievance(e)));
  }

  uploadAttachment(
    id: number,
    rowVersion: string,
    visibility: GrievanceAttachmentVisibility,
    file: File,
  ): Observable<GrievanceAttachmentUploadResult> {
    return this.http
      .post<GrievanceAttachmentUploadResult>(
        `${this.url}/cases/${id}/attachments`,
        uploadForm(rowVersion, file, { visibility }),
      )
      .pipe(catchError((e) => failGrievance(e)));
  }

  downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload> {
    return downloadGrievanceFile(this.http, `${this.url}/attachments/${attachmentId}`);
  }

  addCorrectiveAction(
    id: number,
    input: GrievanceCorrectiveActionInput,
  ): Observable<GrievanceCorrectiveAction> {
    return this.http
      .post<GrievanceCorrectiveAction>(`${this.url}/cases/${id}/actions`, input)
      .pipe(catchError((e) => failGrievance(e)));
  }

  updateCorrectiveAction(
    actionId: number,
    changes: GrievanceCorrectiveActionUpdate,
  ): Observable<GrievanceCorrectiveAction> {
    return this.http
      .put<GrievanceCorrectiveAction>(`${this.url}/case-actions/${actionId}`, changes)
      .pipe(catchError((e) => failGrievance(e)));
  }

  // ---- Helpers --------------------------------------------------------------------------------

  private list(
    path: string,
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>> {
    return this.http
      .get<PagedResult<GrievanceCase>>(`${this.url}/${path}`, {
        params: buildPageParams(request, GRIEVANCE_CASE_SORT_KEYS, GRIEVANCE_CASE_FILTER_KEYS),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  private command(id: number, action: string, body: object): Observable<GrievanceCaseDetail> {
    return this.http
      .post<GrievanceCaseDetail>(`${this.url}/cases/${id}/${action}`, body)
      .pipe(catchError((e) => failGrievance(e)));
  }
}
