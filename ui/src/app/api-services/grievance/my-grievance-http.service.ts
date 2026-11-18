import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import {
  GRIEVANCE_MY_SORT_KEYS,
  GrievanceAttachmentDownload,
  GrievanceDraftInput,
  GrievanceDraftUpdate,
  GrievanceOptions,
  MyGrievance,
  MyGrievanceAttachmentUploadResult,
  MyGrievanceDetail,
  MyGrievanceFilter,
  MyGrievanceMessageRequest,
  SubmitGrievanceRequest,
} from '../../core/models/grievance.models';
import { MyGrievanceDashboard } from '../../core/models/grievance-insight.models';
import { MyGrievanceDataService } from '../../core/services/data/grievance-data.services';
import { API_BASE_URL } from '../api-base-url';
import { buildPageParams } from '../kpi/kpi-http.util';
import { downloadGrievanceFile, failGrievance, uploadForm } from './grievance-http.support';

const MY_FILTER_KEYS = ['status'] as const;

/**
 * HTTP implementation of the submitter's contract (`/api/grievances/options`, `/my...`). Nothing is
 * cached: every write sends the rowVersion the caller passes in. `deleteDraft` sends it in the body
 * (`DELETE` with `{ rowVersion }`, as the API reads it), `markSeen` has no body (204), uploads are
 * multipart (`rowVersion`, `file`), downloads are Blobs.
 */
@Injectable({ providedIn: 'root' })
export class MyGrievanceHttpService implements MyGrievanceDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/grievances`;
  }

  options(): Observable<GrievanceOptions> {
    return this.http
      .get<GrievanceOptions>(`${this.url}/options`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  query(request: PageRequest<MyGrievanceFilter>): Observable<PagedResult<MyGrievance>> {
    return this.http
      .get<PagedResult<MyGrievance>>(`${this.url}/my`, {
        params: buildPageParams(request, GRIEVANCE_MY_SORT_KEYS, MY_FILTER_KEYS),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  getById(id: number): Observable<MyGrievanceDetail> {
    return this.http
      .get<MyGrievanceDetail>(`${this.url}/my/${id}`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  dashboard(): Observable<MyGrievanceDashboard> {
    return this.http
      .get<MyGrievanceDashboard>(`${this.url}/my/dashboard`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload> {
    return downloadGrievanceFile(this.http, `${this.url}/my/attachments/${attachmentId}`);
  }

  createDraft(input: GrievanceDraftInput): Observable<MyGrievanceDetail> {
    return this.http
      .post<MyGrievanceDetail>(`${this.url}/my`, input)
      .pipe(catchError((e) => failGrievance(e)));
  }

  updateDraft(id: number, changes: GrievanceDraftUpdate): Observable<MyGrievanceDetail> {
    return this.http
      .put<MyGrievanceDetail>(`${this.url}/my/${id}`, changes)
      .pipe(catchError((e) => failGrievance(e)));
  }

  deleteDraft(id: number, rowVersion: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/my/${id}`, { body: { rowVersion } }).pipe(
      map(() => undefined),
      catchError((e) => failGrievance(e)),
    );
  }

  submit(id: number, request: SubmitGrievanceRequest): Observable<MyGrievanceDetail> {
    return this.http
      .post<MyGrievanceDetail>(`${this.url}/my/${id}/submit`, request)
      .pipe(catchError((e) => failGrievance(e)));
  }

  sendMessage(id: number, request: MyGrievanceMessageRequest): Observable<MyGrievanceDetail> {
    return this.http
      .post<MyGrievanceDetail>(`${this.url}/my/${id}/messages`, request)
      .pipe(catchError((e) => failGrievance(e)));
  }

  uploadAttachment(
    id: number,
    rowVersion: string,
    file: File,
  ): Observable<MyGrievanceAttachmentUploadResult> {
    return this.http
      .post<MyGrievanceAttachmentUploadResult>(
        `${this.url}/my/${id}/attachments`,
        uploadForm(rowVersion, file),
      )
      .pipe(catchError((e) => failGrievance(e)));
  }

  markSeen(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/my/${id}/seen`, null).pipe(
      map(() => undefined),
      catchError((e) => failGrievance(e)),
    );
  }
}
