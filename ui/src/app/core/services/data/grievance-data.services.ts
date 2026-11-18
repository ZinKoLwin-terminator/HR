import { Observable } from 'rxjs';
import { PagedResult, PageRequest } from '../../models/common.models';
import {
  AcknowledgeGrievanceRequest,
  ApproveResolutionRequest,
  AssignInvestigatorRequest,
  EscalateGrievanceRequest,
  GrievanceAttachmentDownload,
  GrievanceAttachmentUploadResult,
  GrievanceAttachmentVisibility,
  GrievanceAuditEntry,
  GrievanceAuditFilter,
  GrievanceCase,
  GrievanceCaseDetail,
  GrievanceCaseFilter,
  GrievanceCaseMessageRequest,
  GrievanceCategory,
  GrievanceCategoryFilter,
  GrievanceCommand,
  GrievanceCorrectiveAction,
  GrievanceCorrectiveActionInput,
  GrievanceCorrectiveActionUpdate,
  GrievanceDraftInput,
  GrievanceDraftUpdate,
  GrievanceHistoryEntry,
  GrievanceEmployeeOption,
  GrievanceInvestigatorOption,
  GrievanceNoteRequest,
  GrievanceOptions,
  GrievanceReasonCommand,
  GrievanceTriageRequest,
  MyGrievance,
  MyGrievanceAttachmentUploadResult,
  MyGrievanceDetail,
  MyGrievanceFilter,
  MyGrievanceMessageRequest,
  ProposeResolutionRequest,
  RequestGrievanceInfoRequest,
  SubmitGrievanceRequest,
  UnassignInvestigatorRequest,
} from '../../models/grievance.models';
import {
  GrievanceHrDashboard,
  GrievanceReportFilter,
  GrievanceReportRow,
  GrievanceReviewDashboard,
  GrievanceSummary,
  GrievanceSummaryFilter,
  MyGrievanceDashboard,
} from '../../models/grievance-insight.models';
import { CrudDataService } from './crud-data.service';

// Contracts of the employee grievance module (`/api/grievances`). Screens inject these abstract
// classes only. Errors are emitted as `AppError` (see `grievance.models.ts` for the status codes).
// One method per API route. In the demo build the mocks are a browser simulation that enforces
// nothing; the API is the authority on scope, redaction and the state machine.

/**
 * Categories (`grievance-management`, `MapCrud` on `/api/grievances/categories`). Activating or
 * deactivating is `update(id, { status })`, as for every other CRUD contract.
 */
export abstract class GrievanceCategoryDataService extends CrudDataService<
  GrievanceCategory,
  GrievanceCategory,
  GrievanceCategoryFilter
> {}

/**
 * The submitter's own cases (`/api/grievances/my...`): reads need `grievance:View`, writes
 * `grievance:Create`. A case of someone else is NotFound. Anonymous cases are found through the
 * caller's pseudonym, so they appear here like any other.
 */
export abstract class MyGrievanceDataService {
  /** `GET /options`: active categories (sort order) and departments for the form. */
  abstract options(): Observable<GrievanceOptions>;
  /** `GET /my`: the caller's cases, drafts included, newest update first by default. */
  abstract query(request: PageRequest<MyGrievanceFilter>): Observable<PagedResult<MyGrievance>>;
  /** `GET /my/{id}`: not audited (the submitter reading their own case leaves no access-log row). */
  abstract getById(id: number): Observable<MyGrievanceDetail>;
  /** `GET /my/dashboard`. */
  abstract dashboard(): Observable<MyGrievanceDashboard>;
  /** `GET /my/attachments/{attachmentId}`: Shared attachments of the caller's own cases only. */
  abstract downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload>;

  /** `POST /my` (201): a new Draft. */
  abstract createDraft(input: GrievanceDraftInput): Observable<MyGrievanceDetail>;
  /** `PUT /my/{id}`: Draft only. */
  abstract updateDraft(id: number, changes: GrievanceDraftUpdate): Observable<MyGrievanceDetail>;
  /** `DELETE /my/{id}` (body `{ rowVersion }`): Draft only, removed for good with its files. */
  abstract deleteDraft(id: number, rowVersion: string): Observable<void>;
  /** `POST /my/{id}/submit`: Draft to Submitted. */
  abstract submit(id: number, request: SubmitGrievanceRequest): Observable<MyGrievanceDetail>;
  /**
   * `POST /my/{id}/messages`: not on a Draft, Closed or archived case (allowed on Rejected and Resolved). While
   * AwaitingEmployeeResponse this is the response and the case returns to its previous status.
   */
  abstract sendMessage(
    id: number,
    request: MyGrievanceMessageRequest,
  ): Observable<MyGrievanceDetail>;
  /**
   * `POST /my/{id}/attachments` (multipart `rowVersion` + `file`, rate limited): always Shared.
   * Draft or open cases only.
   */
  abstract uploadAttachment(
    id: number,
    rowVersion: string,
    file: File,
  ): Observable<MyGrievanceAttachmentUploadResult>;
  /**
   * `POST /my/{id}/seen` (no body, 204): clears the unread badge. Idempotent. The seen time is kept
   * outside the case row, so the case `rowVersion` does not change.
   */
  abstract markSeen(id: number): Observable<void>;
}

/**
 * The case team (`/api/grievances/cases...`, `/investigations`, `/reviews/pending`, `/attachments`,
 * `/case-actions`, `/investigators`). The scope of a case is the union of the caller's tiers
 * (management: every non-Draft case; investigator: active assignment; reviewer: escalated or pending
 * proposal, and cases the caller decided), never a case the caller submitted (named or anonymous)
 * or is the subject employee of: such a case is silently left out of lists and details (404).
 * Report rows, export rows and the dashboard's recent activity leave out only the cases the caller
 * is the subject of. Aggregate figures leave nothing out, so every viewer sees the same numbers.
 * Every command answers with the case detail and its new `rowVersion`.
 */
export abstract class GrievanceCaseDataService {
  // ---- Reads ----
  /** `GET /cases/query` (`grievance-management:View`). */
  abstract query(request: PageRequest<GrievanceCaseFilter>): Observable<PagedResult<GrievanceCase>>;
  /** `GET /investigations` (`grievance-investigation:View`): cases with the caller's active assignment. */
  abstract investigations(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>>;
  /** `GET /reviews/pending` (`grievance-review:View`): escalated cases and pending proposals, oldest first. */
  abstract pendingReviews(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>>;
  /** `GET /cases/{id}` (audited): sections trimmed by the caller's tiers. */
  abstract getById(id: number): Observable<GrievanceCaseDetail>;
  /** `GET /cases/{id}/history`: oldest first. */
  abstract history(id: number): Observable<GrievanceHistoryEntry[]>;
  /** `GET /cases/{id}/audit` (`grievance-management:View` and administrator): newest first. */
  abstract audit(
    id: number,
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>>;
  /**
   * `GET /audit` (`grievance-management:View` and administrator): the module-wide access log, newest
   * first, including the Excel exports (`caseId` null); filters `action`, `from`, `to`.
   */
  abstract auditLog(
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>>;
  /**
   * `GET /investigators` (`grievance-management:Edit`): by display name; `search` matches code or
   * name. Never filtered per case (a missing name would reveal an anonymous submitter); `assign`
   * refuses a conflicted choice instead.
   */
  abstract investigators(search?: string | null): Observable<GrievanceInvestigatorOption[]>;
  /**
   * `GET /employees?search=` (`grievance-management:Edit`): current employees matching a code or
   * name, by name, at most `GRIEVANCE_EMPLOYEE_OPTIONS_MAX` (the triage "subject employee" picker).
   */
  abstract employeeOptions(search: string): Observable<GrievanceEmployeeOption[]>;

  // ---- State machine (management Edit unless stated) ----
  /** Submitted to Acknowledged. */
  abstract acknowledge(
    id: number,
    request: AcknowledgeGrievanceRequest,
  ): Observable<GrievanceCaseDetail>;
  /** Acknowledged or Reopened to UnderReview. */
  abstract startReview(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail>;
  /** See {@link AssignInvestigatorRequest}. */
  abstract assign(id: number, request: AssignInvestigatorRequest): Observable<GrievanceCaseDetail>;
  abstract unassign(
    id: number,
    request: UnassignInvestigatorRequest,
  ): Observable<GrievanceCaseDetail>;
  /** Assigned to UnderInvestigation (assigned investigator Edit or management Edit). */
  abstract startInvestigation(
    id: number,
    request: GrievanceCommand,
  ): Observable<GrievanceCaseDetail>;
  /** To AwaitingEmployeeResponse (management Edit or assigned investigator Edit). */
  abstract requestInfo(
    id: number,
    request: RequestGrievanceInfoRequest,
  ): Observable<GrievanceCaseDetail>;
  /** To ResolutionProposed (Lead investigator Edit from UnderInvestigation, management from UnderReview). */
  abstract proposeResolution(
    id: number,
    request: ProposeResolutionRequest,
  ): Observable<GrievanceCaseDetail>;
  /** ResolutionProposed to Resolved (`grievance-review:Approve`, not the proposer). */
  abstract approveResolution(
    id: number,
    request: ApproveResolutionRequest,
  ): Observable<GrievanceCaseDetail>;
  /** ResolutionProposed back to UnderInvestigation, or UnderReview without a Lead (`grievance-review:Approve`). */
  abstract rejectResolution(
    id: number,
    request: GrievanceReasonCommand,
  ): Observable<GrievanceCaseDetail>;
  /** Submitted, Acknowledged or UnderReview to Rejected. */
  abstract reject(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail>;
  /** Resolved to Closed. */
  abstract close(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail>;
  /** Resolved or Rejected to Reopened; from Closed also needs `grievance-review:Approve`. */
  abstract reopen(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail>;
  /** Flags an open case (management Edit or assigned investigator). */
  abstract escalate(id: number, request: EscalateGrievanceRequest): Observable<GrievanceCaseDetail>;
  /** Closed or Rejected without legal hold: sets the archive date (`grievance-management:Delete`). */
  abstract archive(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail>;

  // ---- Case work ----
  /** `PUT /cases/{id}/triage`. */
  abstract triage(id: number, request: GrievanceTriageRequest): Observable<GrievanceCaseDetail>;
  /** `POST /cases/{id}/messages`: to the submitter (management or assigned investigator). */
  abstract sendMessage(
    id: number,
    request: GrievanceCaseMessageRequest,
  ): Observable<GrievanceCaseDetail>;
  /** `POST /cases/{id}/notes`: append-only (management or assigned investigator). */
  abstract addNote(id: number, request: GrievanceNoteRequest): Observable<GrievanceCaseDetail>;
  /**
   * `POST /cases/{id}/attachments` (multipart `rowVersion`, `visibility`, `file`; rate limited), open
   * cases only. A Shared file changes the case row (409 on a stale `rowVersion`); a CaseTeam or
   * Restricted file accepts an old well-formed `rowVersion` and leaves the case's unchanged.
   */
  abstract uploadAttachment(
    id: number,
    rowVersion: string,
    visibility: GrievanceAttachmentVisibility,
    file: File,
  ): Observable<GrievanceAttachmentUploadResult>;
  /** `GET /attachments/{attachmentId}` (audited): only attachments the caller's tiers may open. */
  abstract downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload>;
  /** `POST /cases/{id}/actions` (201, management Edit). */
  abstract addCorrectiveAction(
    id: number,
    input: GrievanceCorrectiveActionInput,
  ): Observable<GrievanceCorrectiveAction>;
  /** `PUT /case-actions/{actionId}` (management Edit). */
  abstract updateCorrectiveAction(
    actionId: number,
    changes: GrievanceCorrectiveActionUpdate,
  ): Observable<GrievanceCorrectiveAction>;
}

/**
 * Grievance dashboards and reports (read only, aggregates). Every figure is the server's; screens
 * display it and never compute an official figure. Metric definitions: `grievance-insight.models.ts`.
 * Paging is `pageIndex` and `pageSize` (1 to 200); `sort` takes only `GRIEVANCE_REPORT_SORT_KEYS`.
 */
export abstract class GrievanceInsightDataService {
  /** `GET /dashboard/hr` (`grievance-management:View`). */
  abstract hrDashboard(): Observable<GrievanceHrDashboard>;
  /** `GET /dashboard/review` (`grievance-review:View`). */
  abstract reviewDashboard(): Observable<GrievanceReviewDashboard>;
  /** `GET /reports/cases` (`grievance-management:View`). */
  abstract casesReport(
    request: PageRequest<GrievanceReportFilter>,
  ): Observable<PagedResult<GrievanceReportRow>>;
  /** `GET /reports/summary` (`grievance-management:View` or `grievance-review:View`). */
  abstract summary(filters?: Partial<GrievanceSummaryFilter>): Observable<GrievanceSummary>;
}

// ---- Method lists for `lazyDataService` ------------------------------------------------------
// `satisfies` makes the compiler refuse a list when a method of the contract is missing (or a name
// is not one), so a lazy binding can never forget a new method.

const GRIEVANCE_CATEGORY_METHOD_SET = {
  getAll: true,
  getById: true,
  query: true,
  create: true,
  update: true,
  delete: true,
} as const satisfies Record<keyof GrievanceCategoryDataService, true>;

/** The method names of {@link GrievanceCategoryDataService} (for the lazy binding). */
export const GRIEVANCE_CATEGORY_METHODS = Object.keys(
  GRIEVANCE_CATEGORY_METHOD_SET,
) as readonly (keyof GrievanceCategoryDataService)[];

const MY_GRIEVANCE_METHOD_SET = {
  options: true,
  query: true,
  getById: true,
  dashboard: true,
  downloadAttachment: true,
  createDraft: true,
  updateDraft: true,
  deleteDraft: true,
  submit: true,
  sendMessage: true,
  uploadAttachment: true,
  markSeen: true,
} as const satisfies Record<keyof MyGrievanceDataService, true>;

/** The method names of {@link MyGrievanceDataService} (for the lazy binding). */
export const GRIEVANCE_MY_METHODS = Object.keys(
  MY_GRIEVANCE_METHOD_SET,
) as readonly (keyof MyGrievanceDataService)[];

const GRIEVANCE_CASE_METHOD_SET = {
  query: true,
  investigations: true,
  pendingReviews: true,
  getById: true,
  history: true,
  audit: true,
  auditLog: true,
  investigators: true,
  employeeOptions: true,
  acknowledge: true,
  startReview: true,
  assign: true,
  unassign: true,
  startInvestigation: true,
  requestInfo: true,
  proposeResolution: true,
  approveResolution: true,
  rejectResolution: true,
  reject: true,
  close: true,
  reopen: true,
  escalate: true,
  archive: true,
  triage: true,
  sendMessage: true,
  addNote: true,
  uploadAttachment: true,
  downloadAttachment: true,
  addCorrectiveAction: true,
  updateCorrectiveAction: true,
} as const satisfies Record<keyof GrievanceCaseDataService, true>;

/** The method names of {@link GrievanceCaseDataService} (for the lazy binding). */
export const GRIEVANCE_CASE_METHODS = Object.keys(
  GRIEVANCE_CASE_METHOD_SET,
) as readonly (keyof GrievanceCaseDataService)[];

const GRIEVANCE_INSIGHT_METHOD_SET = {
  hrDashboard: true,
  reviewDashboard: true,
  casesReport: true,
  summary: true,
} as const satisfies Record<keyof GrievanceInsightDataService, true>;

/** The method names of {@link GrievanceInsightDataService} (for the lazy binding). */
export const GRIEVANCE_INSIGHT_METHODS = Object.keys(
  GRIEVANCE_INSIGHT_METHOD_SET,
) as readonly (keyof GrievanceInsightDataService)[];
