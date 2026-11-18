import { Entity, RecordStatus } from './common.models';
import { EvidenceDownload } from './kpi.models';

/**
 * Employee grievance models, typed to the API contract (`/api/grievances`, see the approved plan and,
 * once written, `api/README.md` "HTTP contract (Grievances)").
 *
 * Conventions (the same as the KPI module):
 *  - dates are `yyyy-MM-dd` strings; `...AtUtc` fields are ISO date-times in UTC;
 *  - every enum is the exact text of the database CHECK constraint (`UnderReview`, `CaseTeam`, ...);
 *    the `..._LABELS` maps give the display text;
 *  - `rowVersion` is the opaque concurrency token of the case: send back the value you last received.
 *    Every write answers with the new one; use it for the next write;
 *  - errors are `AppError`: Validation (400, also a malformed `rowVersion`), NotFound (404, also a case
 *    outside the caller's scope), Forbidden (403, wrong actor), Conflict (409, wrong status or stale
 *    `rowVersion`).
 *
 * Confidentiality: the submitter of an anonymous case is never on any object of this file. The
 * case-team types carry `submitter: null` and `submitterLabel: 'Anonymous'`; history and messages name
 * the actor "Anonymous submitter". The submitter's own views (`MyGrievance...`) name nobody on the HR
 * side: every case-team actor is "HR team".
 *
 * In the demo build (mock services) all of this is a browser simulation: nothing is enforced and all
 * data is fictional.
 */

// ---- Enumerations (exact API texts) --------------------------------------------------------

export type GrievanceStatus =
  | 'Draft'
  | 'Submitted'
  | 'Acknowledged'
  | 'UnderReview'
  | 'Assigned'
  | 'UnderInvestigation'
  | 'AwaitingEmployeeResponse'
  | 'ResolutionProposed'
  | 'Resolved'
  | 'Closed'
  | 'Rejected'
  | 'Reopened';

/** Workflow order. */
export const GRIEVANCE_STATUSES: readonly GrievanceStatus[] = [
  'Draft',
  'Submitted',
  'Acknowledged',
  'UnderReview',
  'Assigned',
  'UnderInvestigation',
  'AwaitingEmployeeResponse',
  'ResolutionProposed',
  'Resolved',
  'Closed',
  'Rejected',
  'Reopened',
];

export const GRIEVANCE_STATUS_LABELS: Readonly<Record<GrievanceStatus, string>> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Acknowledged: 'Acknowledged',
  UnderReview: 'Under Review',
  Assigned: 'Assigned',
  UnderInvestigation: 'Under Investigation',
  AwaitingEmployeeResponse: 'Awaiting Employee Response',
  ResolutionProposed: 'Resolution Proposed',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Rejected: 'Rejected',
  Reopened: 'Reopened',
};

/**
 * The statuses of an open case: not Draft, Resolved, Closed or Rejected. Only an open case can be
 * overdue (target date before today, business time zone) or escalated.
 */
export const GRIEVANCE_OPEN_STATUSES: readonly GrievanceStatus[] = [
  'Submitted',
  'Acknowledged',
  'UnderReview',
  'Assigned',
  'UnderInvestigation',
  'AwaitingEmployeeResponse',
  'ResolutionProposed',
  'Reopened',
];

export type GrievancePriority = 'Low' | 'Medium' | 'High' | 'Critical';
export const GRIEVANCE_PRIORITIES: readonly GrievancePriority[] = [
  'Low',
  'Medium',
  'High',
  'Critical',
];

/**
 * Who may open an attachment:
 *  - Shared: the submitter and the whole case team (management, assigned investigators, reviewers);
 *  - CaseTeam: management and the assigned investigators;
 *  - Restricted: management only.
 * Every submitter upload is Shared.
 */
export type GrievanceAttachmentVisibility = 'Shared' | 'CaseTeam' | 'Restricted';
export const GRIEVANCE_ATTACHMENT_VISIBILITIES: readonly GrievanceAttachmentVisibility[] = [
  'Shared',
  'CaseTeam',
  'Restricted',
];
export const GRIEVANCE_ATTACHMENT_VISIBILITY_LABELS: Readonly<
  Record<GrievanceAttachmentVisibility, string>
> = {
  Shared: 'Shared with the employee',
  CaseTeam: 'Case team only',
  Restricted: 'Restricted (HR management)',
};

export type GrievanceNoteKind = 'Note' | 'Interview' | 'FollowUp' | 'Finding' | 'Recommendation';
export const GRIEVANCE_NOTE_KINDS: readonly GrievanceNoteKind[] = [
  'Note',
  'Interview',
  'FollowUp',
  'Finding',
  'Recommendation',
];
export const GRIEVANCE_NOTE_KIND_LABELS: Readonly<Record<GrievanceNoteKind, string>> = {
  Note: 'Note',
  Interview: 'Interview',
  FollowUp: 'Follow-up',
  Finding: 'Finding',
  Recommendation: 'Recommendation',
};

export type GrievanceCommunicationDirection = 'ToSubmitter' | 'FromSubmitter';

export type GrievanceCommunicationKind = 'Message' | 'InfoRequest' | 'InfoResponse';
export const GRIEVANCE_COMMUNICATION_KIND_LABELS: Readonly<
  Record<GrievanceCommunicationKind, string>
> = {
  Message: 'Message',
  InfoRequest: 'Information request',
  InfoResponse: 'Information response',
};

/** The role an actor had on the case when acting (history, messages, attachments). */
export type GrievanceActorRole =
  'Submitter' | 'Management' | 'Investigator' | 'Reviewer' | 'System';

/** The case-team tiers the caller holds on one case (a caller may hold several). */
export type GrievanceViewerRole = 'Management' | 'Investigator' | 'Reviewer';

export type GrievanceAssignmentRole = 'Lead' | 'Member';
export const GRIEVANCE_ASSIGNMENT_ROLES: readonly GrievanceAssignmentRole[] = ['Lead', 'Member'];

export type GrievanceCorrectiveActionStatus = 'Proposed' | 'Approved' | 'Rejected' | 'Implemented';
export const GRIEVANCE_CORRECTIVE_ACTION_STATUSES: readonly GrievanceCorrectiveActionStatus[] = [
  'Proposed',
  'Approved',
  'Rejected',
  'Implemented',
];

export type GrievanceProposalDecision = 'Pending' | 'Approved' | 'Rejected';

/**
 * The action of a history entry. Assign, Unassign and Escalate keep the status (`fromStatus` equals
 * `toStatus`); every other action is a transition of the state machine.
 */
export type GrievanceHistoryAction =
  | 'Submit'
  | 'Acknowledge'
  | 'StartReview'
  | 'Assign'
  | 'Unassign'
  | 'StartInvestigation'
  | 'RequestInfo'
  | 'Respond'
  | 'ProposeResolution'
  | 'ApproveResolution'
  | 'RejectResolution'
  | 'Reject'
  | 'Close'
  | 'Reopen'
  | 'Escalate'
  | 'Archive';
export const GRIEVANCE_HISTORY_ACTION_LABELS: Readonly<Record<GrievanceHistoryAction, string>> = {
  Submit: 'Submitted',
  Acknowledge: 'Acknowledged',
  StartReview: 'Review started',
  Assign: 'Investigator assigned',
  Unassign: 'Investigator removed',
  StartInvestigation: 'Investigation started',
  RequestInfo: 'Information requested',
  Respond: 'Employee responded',
  ProposeResolution: 'Resolution proposed',
  ApproveResolution: 'Resolution approved',
  RejectResolution: 'Resolution sent back',
  Reject: 'Rejected',
  Close: 'Closed',
  Reopen: 'Reopened',
  Escalate: 'Escalated',
  Archive: 'Archived',
};

/** Actions of the confidential access log (`grv.AuditLog`). `detail` holds codes only, never text. */
export type GrievanceAuditAction =
  | 'ViewCase'
  | 'ViewHistory'
  | 'DownloadAttachment'
  | 'UploadAttachment'
  | 'Export'
  | 'Assign'
  | 'Unassign'
  | 'StatusChange'
  | 'Triage'
  | 'AddNote'
  | 'SendMessage'
  | 'CorrectiveAction'
  | 'AccessRefused';

// ---- Display texts and limits (the API uses the same) -----------------------------------------

/** `submitterLabel` of an anonymous case. */
export const GRIEVANCE_ANONYMOUS_LABEL = 'Anonymous';
/** Actor name of the submitter of an anonymous case (history, messages). */
export const GRIEVANCE_ANONYMOUS_ACTOR_NAME = 'Anonymous submitter';
/**
 * Actor name STORED for the submitter of a named case (history rows never carry the submitter's ids).
 * The case-team history shows the submitter's real name instead, resolved from the submitter record.
 */
export const GRIEVANCE_SUBMITTER_ACTOR_NAME = 'Submitter';
/** Department text of an anonymous case in lists, reports and charts. */
export const GRIEVANCE_NOT_DISCLOSED_LABEL = 'Not disclosed';

/** Text lengths checked by the API (inclusive). */
export const GRIEVANCE_LIMITS = {
  subjectMin: 5,
  subjectMax: 200,
  descriptionMin: 20,
  descriptionMax: 4000,
  /** Location, involved person, interviewee, corrective-action texts. */
  shortTextMax: 200,
  reasonMax: 1000,
  messageMax: 4000,
  /** An information request: also stored as the history reason, so the reason limit applies. */
  infoRequestMax: 1000,
  noteMax: 4000,
  summaryMax: 4000,
} as const;

/**
 * Attachment limits: content checked by type (PDF, PNG, JPEG, DOCX, XLSX), 10 MB each. The per-case
 * quotas are separate: the submitter's own files and the case team's files.
 */
export const GRIEVANCE_ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;
/** Files the submitter may add to one case. */
export const GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE = 10;
/** Files the case team may add to one case. */
export const GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE = 20;
/** What one employee may upload to their grievances in a day (the API's quota). */
export const GRIEVANCE_SUBMITTER_DAILY_UPLOAD_BYTES = 100 * 1024 * 1024;
/** Drafts one employee may have at a time. */
export const GRIEVANCE_OPEN_DRAFTS_MAX = 5;
export const GRIEVANCE_ATTACHMENT_ALLOWED_EXTENSIONS: readonly string[] = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.docx',
  '.xlsx',
];

// ---- Categories (HR master data, `grievance-management`) ---------------------------------------

export interface GrievanceCategory extends Entity {
  /** At most 20 characters: upper-case letters, digits and dashes. Unique. */
  code: string;
  /** At most 100 characters, unique. */
  name: string;
  description: string;
  /** Whether a case of this category may be submitted anonymously. */
  allowsAnonymous: boolean;
  /** Days from acknowledgement to the default target resolution date (1 to 365). */
  defaultTargetDays: number;
  sortOrder: number;
  status: RecordStatus;
  rowVersion?: string;
}

export interface GrievanceCategoryFilter {
  status: RecordStatus;
  allowsAnonymous: boolean;
}

// ---- Submitter view (`grievance`) --------------------------------------------------------------

/** One active category of the submission form, by sort order. */
export interface GrievanceCategoryOption {
  id: number;
  code: string;
  name: string;
  description: string;
  allowsAnonymous: boolean;
  defaultTargetDays: number;
}

/** An active department, for the optional "involved department" of the form. */
export interface GrievanceDepartmentOption {
  id: number;
  name: string;
}

/** `GET /api/grievances/options` (`grievance:View`). */
export interface GrievanceOptions {
  categories: GrievanceCategoryOption[];
  departments: GrievanceDepartmentOption[];
}

/** Who acted, as the submitter sees it: themselves, or anyone on the HR side. */
export type MyGrievanceActor = 'You' | 'HR team' | 'System';

/** One of the caller's own cases in a list. */
export interface MyGrievance extends Entity {
  /** `GRV-yyyy-nnnnn`, assigned at submission; null for a draft. */
  caseNo: string | null;
  categoryId: number;
  categoryName: string;
  subject: string;
  status: GrievanceStatus;
  isAnonymous: boolean;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  targetResolutionDate: string | null;
  /** The last change the submitter should notice (status, message, shared file, resolution). */
  lastUpdateAtUtc: string | null;
  /** `lastUpdateAtUtc` is later than the last `markSeen` (also for anonymous cases). */
  hasUnreadUpdate: boolean;
  rowVersion: string;
}

/** A status change the submitter may see (`VisibleToSubmitter`), oldest first. */
export interface MyGrievanceTimelineEntry {
  fromStatus: GrievanceStatus;
  toStatus: GrievanceStatus;
  action: GrievanceHistoryAction;
  actor: MyGrievanceActor;
  /** The reason recorded with the step (reject, reopen, information request), else null. */
  reason: string | null;
  occurredAtUtc: string;
}

export interface MyGrievanceMessage {
  id: number;
  direction: GrievanceCommunicationDirection;
  kind: GrievanceCommunicationKind;
  body: string;
  actor: MyGrievanceActor;
  inReplyToId: number | null;
  createdAtUtc: string;
}

/** A Shared attachment of the case. The storage key is never exposed. */
export interface MyGrievanceAttachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedBy: MyGrievanceActor;
  uploadedAtUtc: string;
}

/**
 * `GET /api/grievances/my/{id}` (not audited: the submitter reading their own case). Never carries
 * investigation notes, corrective actions, priority, assignments or any case-team member's name.
 */
export interface MyGrievanceDetail extends MyGrievance {
  description: string;
  incidentDate: string | null;
  location: string | null;
  involvedPersonText: string | null;
  involvedDepartmentId: number | null;
  involvedDepartmentName: string | null;
  declarationAcceptedAtUtc: string | null;
  acknowledgedAtUtc: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  /** Only when Rejected. */
  rejectionReason: string | null;
  /** Only when Resolved or Closed and HR made the resolution visible to the submitter. */
  resolutionSummary: string | null;
  timeline: MyGrievanceTimelineEntry[];
  /** Oldest first. */
  messages: MyGrievanceMessage[];
  /** Shared attachments only, oldest first. */
  attachments: MyGrievanceAttachment[];
}

export interface MyGrievanceFilter {
  status: GrievanceStatus;
}

/** The `sort` fields of `GET /my` (anything else is a 400 on `sort`). */
export const GRIEVANCE_MY_SORT_KEYS: readonly string[] = [
  'caseNo',
  'subject',
  'status',
  'createdAtUtc',
  'submittedAtUtc',
  'lastUpdateAtUtc',
];

/**
 * Body of `POST /api/grievances/my` (a new draft) and, with `rowVersion`, of `PUT /my/{id}` (Draft
 * only). Saving a draft needs only an active category and a non-empty subject (maximum lengths
 * apply); the subject and description minimums of `GRIEVANCE_LIMITS` apply at submission.
 * `isAnonymous` is refused when the category does not allow it, and is fixed when the draft is
 * created: an update that changes it is a Validation error on `isAnonymous` (start a new draft
 * instead). The placement copied at submission never changes either. An employee may hold at most
 * `GRIEVANCE_OPEN_DRAFTS_MAX` drafts (Validation).
 */
export interface GrievanceDraftInput {
  categoryId: number;
  subject: string;
  description: string;
  /** Not after today. */
  incidentDate: string | null;
  location: string | null;
  involvedPersonText: string | null;
  involvedDepartmentId: number | null;
  isAnonymous: boolean;
}

export interface GrievanceDraftUpdate extends GrievanceDraftInput {
  rowVersion: string;
}

/** Draft to Submitted. `declarationAccepted` must be true. The case number is assigned now. */
export interface SubmitGrievanceRequest {
  rowVersion: string;
  declarationAccepted: boolean;
}

/**
 * A message from the submitter. While the case is AwaitingEmployeeResponse the message is the
 * response (kind InfoResponse): `rowVersion` is checked and the case returns to the status it came
 * from (new `rowVersion`). Otherwise it is a plain Message: `rowVersion` is not checked and the case
 * `rowVersion` does not change. Refused (Conflict) on a Draft, a Closed or an archived case; allowed
 * on a Rejected or Resolved one.
 */
export interface MyGrievanceMessageRequest {
  rowVersion: string;
  /** 1 to 4000 characters. */
  body: string;
  inReplyToId?: number | null;
}

export interface MyGrievanceAttachmentUploadResult {
  attachment: MyGrievanceAttachment;
  caseRowVersion: string;
}

// ---- Case-team view (`grievance-management`, `grievance-investigation`, `grievance-review`) ------

/** The named submitter of a case: the organisation placement copied at submission. */
export interface GrievanceSubmitter {
  employeeId: number;
  employeeCode: string;
  displayName: string;
  companyId: number | null;
  branchId: number | null;
  branchName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  positionId: number | null;
  positionName: string | null;
}

/** One case in a case-team list (never a Draft). */
export interface GrievanceCase extends Entity {
  caseNo: string;
  categoryId: number;
  categoryName: string;
  subject: string;
  status: GrievanceStatus;
  /** Null until acknowledged. */
  priority: GrievancePriority | null;
  isAnonymous: boolean;
  /** The submitter's display name, or `GRIEVANCE_ANONYMOUS_LABEL`. */
  submitterLabel: string;
  /** The submitter's department at submission; null for an anonymous case. */
  departmentId: number | null;
  departmentName: string | null;
  submittedAtUtc: string;
  targetResolutionDate: string | null;
  /** Open and the target date is before today (business time zone); computed when read. */
  isOverdue: boolean;
  isEscalated: boolean;
  leadInvestigatorEmployeeId: number | null;
  leadInvestigatorName: string | null;
  archivedAtUtc: string | null;
  rowVersion: string;
}

export interface GrievanceAssignment {
  id: number;
  investigatorEmployeeId: number;
  investigatorCode: string;
  investigatorName: string;
  role: GrievanceAssignmentRole;
  assignedAtUtc: string;
  assignedByName: string;
  /** Null while active. */
  unassignedAtUtc: string | null;
  unassignedByName: string | null;
  /** Null for a reviewer-only caller. */
  reason: string | null;
}

/** Append-only. `interviewDate` and `intervieweeText` belong to Interview notes. */
export interface GrievanceNote {
  id: number;
  kind: GrievanceNoteKind;
  body: string;
  interviewDate: string | null;
  intervieweeText: string | null;
  authorEmployeeId: number | null;
  authorName: string;
  createdAtUtc: string;
}

/** Append-only. The author of a submitter message on an anonymous case is "Anonymous submitter". */
export interface GrievanceMessage {
  id: number;
  direction: GrievanceCommunicationDirection;
  kind: GrievanceCommunicationKind;
  body: string;
  authorRole: GrievanceActorRole;
  authorName: string;
  inReplyToId: number | null;
  createdAtUtc: string;
}

/** Metadata of one file. The storage key and hash are never exposed. */
export interface GrievanceAttachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  visibility: GrievanceAttachmentVisibility;
  uploadedByRole: GrievanceActorRole;
  uploadedAtUtc: string;
  communicationId: number | null;
  noteId: number | null;
}

/** HR's follow-up measure. Has its own `rowVersion`. */
export interface GrievanceCorrectiveAction {
  id: number;
  caseId: number;
  /** Free text, at most 100 characters (for example "Training", "Policy change"). */
  actionType: string;
  description: string;
  responsibleText: string | null;
  dueDate: string | null;
  completedOn: string | null;
  status: GrievanceCorrectiveActionStatus;
  rowVersion: string;
}

export interface GrievanceResolutionProposal {
  id: number;
  summary: string;
  proposedByEmployeeId: number | null;
  proposedByName: string;
  proposedAtUtc: string;
  decision: GrievanceProposalDecision;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionComment: string | null;
}

export interface GrievanceEscalation {
  id: number;
  /** Null for a reviewer-only caller (the reason is case-team material). */
  reason: string | null;
  isImmediate: boolean;
  escalatedByName: string;
  escalatedAtUtc: string;
  /** Always null in v1: escalations are never cleared. */
  clearedAtUtc: string | null;
  outcome: string | null;
}

/**
 * `GET /api/grievances/cases/{id}`. The scope is the union of the caller's tiers; the sections are
 * trimmed by tier (a section the caller may not see is an empty list or null, never an error):
 *  - Management: everything;
 *  - Investigator (active assignment): no corrective actions, no Restricted attachments;
 *  - Reviewer: notes of kind Finding and Recommendation only, no messages, Shared attachments only,
 *    no corrective actions.
 * The status history is a separate call (`history`).
 */
export interface GrievanceCaseDetail extends GrievanceCase {
  /** Null when the case is anonymous. */
  submitter: GrievanceSubmitter | null;
  /** The caller's tiers on this case. */
  viewerRoles: GrievanceViewerRole[];
  /** The caller is the active Lead investigator. */
  isLeadInvestigator: boolean;
  description: string;
  incidentDate: string | null;
  location: string | null;
  involvedPersonText: string | null;
  involvedDepartmentId: number | null;
  involvedDepartmentName: string | null;
  /** Set by HR at triage; that employee is excluded from every case-team tier of the case. */
  subjectEmployeeId: number | null;
  subjectEmployeeName: string | null;
  /** While AwaitingEmployeeResponse: the status the case returns to on the response. */
  returnStatus: GrievanceStatus | null;
  declarationAcceptedAtUtc: string;
  acknowledgedAtUtc: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  resolutionSummary: string | null;
  resolutionVisibleToSubmitter: boolean;
  rejectionReason: string | null;
  /** The close or reject date plus 7 years; null before. */
  retentionUntil: string | null;
  /** While true the case cannot be archived. An archived case is read-only. */
  legalHold: boolean;
  /** Active first, then ended, newest first. */
  assignments: GrievanceAssignment[];
  /** Oldest first. */
  notes: GrievanceNote[];
  /** Oldest first. */
  messages: GrievanceMessage[];
  /** Oldest first; removed files are left out. */
  attachments: GrievanceAttachment[];
  correctiveActions: GrievanceCorrectiveAction[];
  /** The pending proposal, else the latest decided one, else null. */
  proposal: GrievanceResolutionProposal | null;
  /** Newest first. */
  escalations: GrievanceEscalation[];
}

/** `GET /cases/{id}/history`: every StatusHistory row, oldest first (append-only). */
export interface GrievanceHistoryEntry {
  id: number;
  fromStatus: GrievanceStatus;
  toStatus: GrievanceStatus;
  action: GrievanceHistoryAction;
  actorRole: GrievanceActorRole;
  /** Always null for a Submitter step (named or anonymous), and for System. */
  actorUserId: number | null;
  actorEmployeeId: number | null;
  /**
   * The account's name; for a Submitter step the submitter's name on a named case (resolved from the
   * submitter record) and "Anonymous submitter" on an anonymous one.
   */
  actorName: string;
  /**
   * The reason recorded with the step. A caller who sees the case only as a reviewer gets it only on
   * the proposal and decision steps (ProposeResolution, ApproveResolution, RejectResolution); every
   * other reason is null for them.
   */
  reason: string | null;
  visibleToSubmitter: boolean;
  occurredAtUtc: string;
}

/** `GET /cases/{id}/audit` (management View and administrator): the access log, newest first. */
export interface GrievanceAuditEntry {
  id: number;
  caseId: number | null;
  caseNo: string | null;
  action: GrievanceAuditAction;
  /** Null for the submitter of an anonymous case. */
  actorUserId: number | null;
  actorName: string | null;
  actorRole: GrievanceActorRole;
  occurredAtUtc: string;
  /** Codes only (for example a status pair or an attachment id), never free text. */
  detail: string | null;
}

export interface GrievanceAuditFilter {
  action: GrievanceAuditAction;
  /** Inclusive `yyyy-MM-dd` range of `occurredAtUtc` (business dates). */
  from: string;
  to: string;
}

/**
 * Filters of the case-team lists (`/cases/query`, `/investigations`, `/reviews/pending`), combined
 * with AND. A department filter never matches an anonymous case. Archived cases are left out unless
 * `archived` is true (then only archived cases are listed; management only).
 */
export interface GrievanceCaseFilter {
  status: GrievanceStatus;
  priority: GrievancePriority;
  categoryId: number;
  departmentId: number;
  isAnonymous: boolean;
  isEscalated: boolean;
  overdue: boolean;
  archived: boolean;
  investigatorEmployeeId: number;
  /** Submission date range (inclusive, business time zone). */
  submittedFrom: string;
  submittedTo: string;
}

/** The query-string names of {@link GrievanceCaseFilter}. */
export const GRIEVANCE_CASE_FILTER_KEYS: readonly (keyof GrievanceCaseFilter)[] = [
  'status',
  'priority',
  'categoryId',
  'departmentId',
  'isAnonymous',
  'isEscalated',
  'overdue',
  'archived',
  'investigatorEmployeeId',
  'submittedFrom',
  'submittedTo',
];

/** The `sort` fields the case-team lists accept (anything else is a 400 on `sort`). */
export const GRIEVANCE_CASE_SORT_KEYS: readonly string[] = [
  'caseNo',
  'submittedAtUtc',
  'targetResolutionDate',
  'priority',
  'status',
  'categoryName',
];

/** One employee who may be assigned (holds `grievance-investigation:Edit`, active). */
export interface GrievanceInvestigatorOption {
  employeeId: number;
  employeeCode: string;
  displayName: string;
  departmentName: string | null;
  positionName: string | null;
  /** Open cases on which the employee has an active assignment. */
  activeCaseCount: number;
}

/**
 * One current employee for the triage "subject employee" picker
 * (`GET /api/grievances/employees?search=`, `grievance-management:Edit`). At most 20, by name.
 */
export interface GrievanceEmployeeOption {
  employeeId: number;
  employeeCode: string;
  displayName: string;
  departmentName: string | null;
  positionName: string | null;
}

/** The most rows `employeeOptions` answers with. */
export const GRIEVANCE_EMPLOYEE_OPTIONS_MAX = 20;

// ---- Case-team commands (`POST /api/grievances/cases/{id}/{action}`) ---------------------------

/** start-review, start-investigation, close, archive: the comment is optional (history reason). */
export interface GrievanceCommand {
  rowVersion: string;
  comment?: string | null;
}

/** reject, reopen, reject-resolution: the reason is required (1 to 1000 characters). */
export interface GrievanceReasonCommand {
  rowVersion: string;
  reason: string;
}

/**
 * Submitted to Acknowledged. `targetResolutionDate` (not before today) defaults to today plus the
 * category's `defaultTargetDays`. Critical escalates the case immediately and alerts the reviewers.
 */
export interface AcknowledgeGrievanceRequest {
  rowVersion: string;
  priority: GrievancePriority;
  targetResolutionDate?: string | null;
  comment?: string | null;
}

/**
 * Lead: from Acknowledged, UnderReview or Reopened the case moves to Assigned; on a case that already
 * has a Lead the new one replaces it (the reason is then required) and the status is kept.
 * Member: added without a status change. Not before acknowledgement, not on a Closed case.
 * Conflict of interest: the subject employee, or the submitter of a NAMED case, is refused with a
 * Validation error on `investigatorEmployeeId` and one generic message ("conflict of interest");
 * the refusal is audited. An anonymous case is never checked against its submitter (a refusal would
 * reveal them); the submitter is still kept out of every case-team tier of their own case.
 */
export interface AssignInvestigatorRequest {
  rowVersion: string;
  investigatorEmployeeId: number;
  role: GrievanceAssignmentRole;
  reason?: string | null;
}

/** Ends one active Member assignment. The active Lead cannot be removed, only replaced by `assign`. */
export interface UnassignInvestigatorRequest {
  rowVersion: string;
  assignmentId: number;
  /** Optional, at most 1000 characters. */
  reason?: string | null;
}

/**
 * UnderReview, Assigned or UnderInvestigation to AwaitingEmployeeResponse. `message` (1 to
 * `GRIEVANCE_LIMITS.infoRequestMax` = 1000) is sent to the submitter as an InfoRequest and recorded
 * as the history reason.
 */
export interface RequestGrievanceInfoRequest {
  rowVersion: string;
  message: string;
}

/** To ResolutionProposed (Lead from UnderInvestigation, management from UnderReview). */
export interface ProposeResolutionRequest {
  rowVersion: string;
  /** 1 to 4000 characters. */
  summary: string;
}

/** ResolutionProposed to Resolved; the reviewer cannot be the proposer. */
export interface ApproveResolutionRequest {
  rowVersion: string;
  /** Whether the submitter sees the resolution summary. */
  visibleToSubmitter: boolean;
  comment?: string | null;
}

/**
 * Flags an open case as escalated (no status change). A case is escalated once (a second escalation
 * is a Conflict) and an escalation is never cleared.
 */
export interface EscalateGrievanceRequest {
  rowVersion: string;
  reason: string;
  isImmediate: boolean;
}

/**
 * `PUT /cases/{id}/triage` (management Edit, open cases): replaces these fields. `priority` and
 * `targetResolutionDate` may be null only while Submitted (an acknowledged case always has both).
 * Raising the priority to Critical escalates the case at once (unless it already is). The category
 * may change, but never to one that refuses anonymity for an anonymous case. `legalHold` is required
 * (always sent; Validation on `legalHold` when missing). A subject employee who is an active
 * assignee of the case is refused with the generic conflict-of-interest Validation on
 * `subjectEmployeeId`.
 */
export interface GrievanceTriageRequest {
  rowVersion: string;
  categoryId: number;
  priority: GrievancePriority | null;
  targetResolutionDate: string | null;
  subjectEmployeeId: number | null;
  involvedDepartmentId: number | null;
  legalHold: boolean;
}

/**
 * `POST /cases/{id}/messages`: a Message to the submitter. Refused (Conflict) on a Draft, a Closed or
 * an archived case; allowed on a Rejected or Resolved one.
 */
export interface GrievanceCaseMessageRequest {
  rowVersion: string;
  body: string;
}

/** `POST /cases/{id}/notes` (management or assigned investigator, open cases). */
export interface GrievanceNoteRequest {
  rowVersion: string;
  kind: GrievanceNoteKind;
  /** 1 to 4000 characters. */
  body: string;
  /** Interview only; not after today. */
  interviewDate?: string | null;
  intervieweeText?: string | null;
}

/** `POST /cases/{id}/actions`. `rowVersion` is the case's. */
export interface GrievanceCorrectiveActionInput {
  rowVersion: string;
  actionType: string;
  description: string;
  responsibleText?: string | null;
  dueDate?: string | null;
}

/** `PUT /case-actions/{id}`. `rowVersion` is the action's own. */
export interface GrievanceCorrectiveActionUpdate {
  rowVersion: string;
  actionType: string;
  description: string;
  responsibleText: string | null;
  dueDate: string | null;
  /** Required when the status is Implemented. */
  completedOn: string | null;
  status: GrievanceCorrectiveActionStatus;
}

/** Answer to a case-team upload: the new attachment and the case's new row version. */
export interface GrievanceAttachmentUploadResult {
  attachment: GrievanceAttachment;
  caseRowVersion: string;
}

/** A downloaded file (the same shape as a KPI evidence download). */
export type GrievanceAttachmentDownload = EvidenceDownload;
