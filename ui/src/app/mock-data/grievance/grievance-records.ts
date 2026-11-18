import { Entity } from '../../core/models/common.models';
import {
  GrievanceActorRole,
  GrievanceAssignmentRole,
  GrievanceAttachmentVisibility,
  GrievanceAuditAction,
  GrievanceCommunicationDirection,
  GrievanceCommunicationKind,
  GrievanceCorrectiveActionStatus,
  GrievanceHistoryEntry,
  GrievanceNoteKind,
  GrievancePriority,
  GrievanceProposalDecision,
  GrievanceStatus,
} from '../../core/models/grievance.models';

/**
 * What the grievance mock store keeps, shaped like the planned `grv` tables. BROWSER SIMULATION:
 * the demo enforces nothing, and all data is fictional.
 *
 * The submitter is NOT on the case record: the link (employee id for a named case, pseudonym for an
 * anonymous one) lives in a separate store, as `grv.CaseSubmitter` does, and the mock services read it
 * only to decide "is this the caller's case" and to address notifications. It never reaches a returned
 * object.
 */

export interface GrievanceAssignmentRecord {
  id: number;
  investigatorEmployeeId: number;
  role: GrievanceAssignmentRole;
  assignedAtUtc: string;
  assignedByUserId: number | null;
  assignedByName: string;
  unassignedAtUtc: string | null;
  unassignedByUserId: number | null;
  unassignedByName: string | null;
  reason: string | null;
}

export interface GrievanceNoteRecord {
  id: number;
  kind: GrievanceNoteKind;
  body: string;
  interviewDate: string | null;
  intervieweeText: string | null;
  authorUserId: number | null;
  authorEmployeeId: number | null;
  authorName: string;
  createdAtUtc: string;
}

/** Author ids are null for a submitter message on an anonymous case. */
export interface GrievanceCommunicationRecord {
  id: number;
  direction: GrievanceCommunicationDirection;
  kind: GrievanceCommunicationKind;
  body: string;
  authorRole: GrievanceActorRole;
  authorUserId: number | null;
  authorEmployeeId: number | null;
  authorName: string;
  inReplyToId: number | null;
  createdAtUtc: string;
}

/** `uploadedByUserId` is null for a submitter upload on an anonymous case. */
export interface GrievanceAttachmentRecord {
  id: number;
  communicationId: number | null;
  noteId: number | null;
  visibility: GrievanceAttachmentVisibility;
  uploadedByRole: GrievanceActorRole;
  uploadedByUserId: number | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  removedAtUtc: string | null;
}

export interface GrievanceCorrectiveActionRecord {
  id: number;
  actionType: string;
  description: string;
  responsibleText: string | null;
  dueDate: string | null;
  completedOn: string | null;
  status: GrievanceCorrectiveActionStatus;
  createdByUserId: number | null;
  rowVersion: string;
}

export interface GrievanceProposalRecord {
  id: number;
  summary: string;
  proposedByUserId: number | null;
  proposedByEmployeeId: number | null;
  proposedByName: string;
  proposedAtUtc: string;
  decision: GrievanceProposalDecision;
  decidedByUserId: number | null;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionComment: string | null;
}

export interface GrievanceEscalationRecord {
  id: number;
  reason: string;
  isImmediate: boolean;
  escalatedByUserId: number | null;
  escalatedByName: string;
  escalatedAtUtc: string;
  clearedAtUtc: string | null;
  outcome: string | null;
}

/** A `grv.StatusHistory` row (append-only). Same shape as the case-team DTO. */
export type GrievanceHistoryRecord = GrievanceHistoryEntry;

/** One case with its child rows. Placement columns are null for an anonymous case. */
export interface GrievanceCaseRecord extends Entity {
  /** Null for a draft. */
  caseNo: string | null;
  categoryId: number;
  subject: string;
  description: string;
  incidentDate: string | null;
  location: string | null;
  involvedPersonText: string | null;
  involvedDepartmentId: number | null;
  subjectEmployeeId: number | null;
  isAnonymous: boolean;
  status: GrievanceStatus;
  priority: GrievancePriority | null;
  targetResolutionDate: string | null;
  returnStatus: GrievanceStatus | null;
  isEscalated: boolean;
  companyId: number | null;
  branchId: number | null;
  departmentId: number | null;
  positionId: number | null;
  createdAtUtc: string;
  declarationAcceptedAtUtc: string | null;
  submittedAtUtc: string | null;
  acknowledgedAtUtc: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  resolutionSummary: string | null;
  resolutionVisibleToSubmitter: boolean;
  rejectionReason: string | null;
  archivedAtUtc: string | null;
  retentionUntil: string | null;
  legalHold: boolean;
  lastSubmitterUpdateAtUtc: string | null;
  rowVersion: string;
  assignments: GrievanceAssignmentRecord[];
  notes: GrievanceNoteRecord[];
  communications: GrievanceCommunicationRecord[];
  attachments: GrievanceAttachmentRecord[];
  correctiveActions: GrievanceCorrectiveActionRecord[];
  proposals: GrievanceProposalRecord[];
  escalations: GrievanceEscalationRecord[];
  history: GrievanceHistoryRecord[];
}

/**
 * The `grv.CaseSubmitter` row of a case: exactly one of `employeeId` (named) and `pseudonym`
 * (anonymous) is set. `seenAtUtc` is the submitter's last "seen" (kept here, not on the case, so
 * marking a case seen never changes the case `rowVersion`).
 */
export interface GrievanceSubmitterLink {
  caseId: number;
  employeeId: number | null;
  pseudonym: string | null;
  seenAtUtc: string | null;
}

/** A `grv.AuditLog` row (append-only). `detail` holds codes only. */
export interface GrievanceAuditRecord {
  id: number;
  caseId: number | null;
  action: GrievanceAuditAction;
  actorUserId: number | null;
  actorRole: GrievanceActorRole;
  occurredAtUtc: string;
  detail: string | null;
}

/** The next concurrency token of the mock: a counter in text form. */
export function nextGrievanceRowVersion(current: string | undefined): string {
  return String((Number(current) || 0) + 1);
}

/**
 * SIMULATION of the API's keyed pseudonym (`HMACSHA256(secret, employeeId)`): a fixed-key FNV-1a
 * hash in the browser. It only lets the demo find "my anonymous cases" without storing the employee
 * id on them; it is NOT secret and NOT secure (anyone can recompute it from the bundle).
 */
export function grievanceDemoPseudonym(employeeId: number): string {
  const text = `apex-hrms-demo-pseudonym-key:${employeeId}`;
  let a = 0x811c9dc5;
  let b = 0x01000193;
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    a = Math.imul(a ^ code, 0x01000193) >>> 0;
    b = Math.imul(b ^ (code + i), 0x811c9dc5) >>> 0;
  }
  return `${a.toString(16).padStart(8, '0')}${b.toString(16).padStart(8, '0')}`;
}

/** `GRV-yyyy-nnnnn` from the year and the sequence number. */
export function grievanceCaseNo(year: number, sequence: number): string {
  return `GRV-${year}-${String(sequence).padStart(5, '0')}`;
}

/** Shared with the screens and the mock (see core/utilities/grievance-workflow.ts). */
export { grievanceStepVisibleToSubmitter } from '../../core/utilities/grievance-workflow';
