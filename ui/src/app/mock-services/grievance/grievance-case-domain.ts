import {
  GRIEVANCE_ANONYMOUS_ACTOR_NAME,
  GRIEVANCE_LIMITS,
  GRIEVANCE_SUBMITTER_ACTOR_NAME,
  GRIEVANCE_PRIORITIES,
  GrievanceActorRole,
  GrievanceAssignmentRole,
  GrievanceHistoryAction,
  GrievancePriority,
  GrievanceStatus,
} from '../../core/models/grievance.models';
import {
  GrievanceCaseRecord,
  GrievanceCommunicationRecord,
} from '../../mock-data/grievance/grievance-records';
import { addDays, addMonths } from '../../shared/utilities/date.util';
import { mockError } from '../mock-response';
import {
  GrievanceActorRights,
  GrievanceWorkflowAction,
  GrievanceWorkflowState,
  grievanceStepVisibleToSubmitter,
  grievanceTransition,
} from '../../core/utilities/grievance-workflow';

/**
 * One step of a grievance case, as a pure function over the stored record: input rules (400), the
 * state machine (403/409, see `grievance-workflow.ts`), the step's own rules, the changes and the
 * history rows. DEMO MIRROR of the API's domain (`Domain/Grievances`); both run the shared golden
 * file `docs/test-data/grievance-transition-cases.json` (`grievance-case-domain.spec.ts`).
 *
 * Not here (the services do it): scope (404), the `rowVersion`, case numbers, notifications, the
 * access log, and whether the row version moves on.
 */

/** Who acts. The services pass the anonymous submitter as { null, null, 'Anonymous submitter' }. */
export interface GrievanceDomainActor {
  userId: number | null;
  employeeId: number | null;
  name: string;
  role: GrievanceActorRole;
}

type ChildKind = 'assignments' | 'communications' | 'proposals' | 'escalations' | 'history';

export interface GrievanceDomainContext {
  /** ISO date-time of the step. */
  now: string;
  /** Today in the business time zone (`yyyy-MM-dd`). */
  today: string;
  actor: GrievanceDomainActor;
  rights: GrievanceActorRights;
  /** The id of the next new row of a kind (called once per new row, in order). */
  nextId: (kind: ChildKind) => number;
}

export type GrievanceDomainCommand =
  | {
      action: 'submit';
      declarationAccepted: boolean;
      categoryActive: boolean;
      categoryAllowsAnonymous: boolean;
      caseNo: string;
      /** The named submitter's placement (ignored for an anonymous case). */
      placement: {
        companyId: number;
        branchId: number;
        departmentId: number;
        positionId: number;
      } | null;
    }
  | {
      action: 'acknowledge';
      priority: GrievancePriority;
      targetResolutionDate: string | null;
      defaultTargetDays: number;
      comment?: string | null;
    }
  | { action: 'reject'; reason: string | null }
  | { action: 'startReview'; comment?: string | null }
  | {
      action: 'assign';
      investigatorEmployeeId: number;
      role: GrievanceAssignmentRole;
      reason: string | null;
      /** The submitter of a NAMED case; null for an anonymous one (never checked: it would reveal them). */
      namedSubmitterEmployeeId: number | null;
    }
  | {
      action: 'unassign';
      /** One of the two identifies the active assignment. */
      assignmentId?: number;
      investigatorEmployeeId?: number;
      reason?: string | null;
    }
  | { action: 'startInvestigation'; comment?: string | null }
  | { action: 'requestInfo'; message: string | null }
  | { action: 'respond'; message: string | null; inReplyToId?: number | null }
  | { action: 'postSubmitterMessage'; message: string | null; inReplyToId?: number | null }
  | { action: 'postTeamMessage'; message: string | null }
  | { action: 'proposeResolution'; summary: string | null }
  | { action: 'approveResolution'; visibleToSubmitter: boolean; comment: string | null }
  | { action: 'rejectResolution'; reason: string | null }
  | { action: 'close'; comment?: string | null }
  | { action: 'reopen'; reason: string | null }
  | { action: 'escalate'; reason: string | null; isImmediate?: boolean }
  | { action: 'archive'; comment?: string | null };

/** The generic refusal of a conflicted assignment (never says whether submitter or subject). */
export const GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE =
  'This person cannot be assigned to this case (conflict of interest).';
export const GRIEVANCE_CRITICAL_ACK_REASON = 'Critical priority: escalated when acknowledged.';
export const GRIEVANCE_CRITICAL_TRIAGE_REASON = 'Critical priority: escalated at triage.';
/** The reason of the Unassign steps a reopening writes (the API's text). */
export const GRIEVANCE_REOPEN_UNASSIGN_REASON = 'Case reopened';
/** Years a closed or rejected case is kept before it may be purged. */
export const GRIEVANCE_RETENTION_YEARS = 7;

function invalid(field: string, message: string): never {
  throw mockError('Validation', message, { [field]: message });
}

/** Trimmed text of min..max characters, or null when blank and optional. */
function clean(
  value: string | null | undefined,
  field: string,
  label: string,
  max: number,
  required: boolean,
): string | null {
  const text = (value ?? '').trim();
  if (text === '') {
    if (required) {
      invalid(field, `${label} is required.`);
    }
    return null;
  }
  if (text.length > max) {
    invalid(field, `${label} must be at most ${max} characters.`);
  }
  return text;
}

const reason = (value: string | null | undefined, required = true): string | null =>
  clean(value, 'reason', 'A reason', GRIEVANCE_LIMITS.reasonMax, required);
const comment = (value: string | null | undefined): string | null =>
  clean(value, 'comment', 'The comment', GRIEVANCE_LIMITS.reasonMax, false);
const message = (value: string | null | undefined, max: number): string =>
  clean(value, 'message', 'The message', max, true)!;

const workflowAction = (command: GrievanceDomainCommand): GrievanceWorkflowAction =>
  command.action === 'assign'
    ? command.role === 'Lead'
      ? 'assignLead'
      : 'assignMember'
    : command.action;

export function grievanceWorkflowState(record: GrievanceCaseRecord): GrievanceWorkflowState {
  return {
    status: record.status,
    returnStatus: record.returnStatus,
    hasActiveLead: record.assignments.some((a) => a.role === 'Lead' && a.unassignedAtUtc === null),
    legalHold: record.legalHold,
    archived: record.archivedAtUtc !== null,
    isEscalated: record.isEscalated,
  };
}

/** The workflow action a command is checked against (assign splits by role). */
export function grievanceWorkflowActionOf(
  command: GrievanceDomainCommand,
): GrievanceWorkflowAction {
  return workflowAction(command);
}

/** Retention date of a case closed or rejected today. */
export const grievanceRetentionUntil = (today: string): string =>
  addMonths(today, GRIEVANCE_RETENTION_YEARS * 12);

/**
 * Runs one step and returns the changed copy of the record (the input is not touched). Throws an
 * `AppError`: Validation (input or a step rule), Forbidden (actor), Conflict (status or a guard).
 */
export function applyGrievanceCommand(
  record: GrievanceCaseRecord,
  command: GrievanceDomainCommand,
  ctx: GrievanceDomainContext,
): GrievanceCaseRecord {
  // ---- 1. Input (400) ----
  let text: string | null = null;
  switch (command.action) {
    case 'submit':
      if (command.declarationAccepted !== true) {
        invalid('declarationAccepted', 'Please confirm the declaration before submitting.');
      }
      break;
    case 'acknowledge':
      if (!GRIEVANCE_PRIORITIES.includes(command.priority)) {
        invalid('priority', 'Choose a priority.');
      }
      if (command.targetResolutionDate !== null && command.targetResolutionDate < ctx.today) {
        invalid('targetResolutionDate', 'The target date cannot be in the past.');
      }
      text = comment(command.comment);
      break;
    case 'reject':
    case 'rejectResolution':
    case 'reopen':
    case 'escalate':
      text = reason(command.reason);
      break;
    case 'assign':
      if (command.role !== 'Lead' && command.role !== 'Member') {
        invalid('role', 'Choose Lead or Member.');
      }
      text = reason(command.reason, false);
      break;
    case 'unassign':
      text = reason(command.reason, false);
      break;
    case 'requestInfo':
      text = clean(
        command.message,
        'message',
        'The information request',
        GRIEVANCE_LIMITS.infoRequestMax,
        true,
      );
      break;
    case 'respond':
    case 'postSubmitterMessage':
    case 'postTeamMessage':
      text = message(command.message, GRIEVANCE_LIMITS.messageMax);
      break;
    case 'proposeResolution':
      text = clean(command.summary, 'summary', 'The summary', GRIEVANCE_LIMITS.summaryMax, true);
      break;
    case 'approveResolution':
      if (typeof command.visibleToSubmitter !== 'boolean') {
        invalid('visibleToSubmitter', 'Say whether the employee sees the resolution.');
      }
      text = comment(command.comment);
      break;
    default:
      text = comment((command as { comment?: string | null }).comment);
  }

  // ---- 2. State machine (403 / 409) ----
  const result = grievanceTransition(
    workflowAction(command),
    grievanceWorkflowState(record),
    ctx.rights,
  );
  if (!result.ok) {
    throw mockError(result.code, result.message);
  }

  // ---- 3. The step ----
  const r: GrievanceCaseRecord = structuredClone(record);
  const from = r.status;
  const to = result.to;
  r.status = to;
  const steps: {
    action: GrievanceHistoryAction;
    from: GrievanceStatus;
    to: GrievanceStatus;
    reason: string | null;
  }[] = [];
  const step = (action: GrievanceHistoryAction, why: string | null, a = from, b = to): void => {
    steps.push({ action, from: a, to: b, reason: why });
  };
  const communicate = (
    direction: GrievanceCommunicationRecord['direction'],
    kind: GrievanceCommunicationRecord['kind'],
    body: string,
    inReplyToId: number | null,
  ): void => {
    if (inReplyToId !== null && !r.communications.some((m) => m.id === inReplyToId)) {
      invalid('inReplyToId', 'The message replied to is not part of this case.');
    }
    r.communications.push({
      id: ctx.nextId('communications'),
      direction,
      kind,
      body,
      authorRole: ctx.actor.role,
      authorUserId: ctx.actor.userId,
      authorEmployeeId: ctx.actor.employeeId,
      authorName: ctx.actor.name,
      inReplyToId,
      createdAtUtc: ctx.now,
    });
    if (direction === 'ToSubmitter') {
      r.lastSubmitterUpdateAtUtc = ctx.now;
    }
  };
  const escalate = (why: string, isImmediate: boolean): void => {
    r.isEscalated = true;
    r.escalations.push({
      id: ctx.nextId('escalations'),
      reason: why,
      isImmediate,
      escalatedByUserId: ctx.actor.userId,
      escalatedByName: ctx.actor.name,
      escalatedAtUtc: ctx.now,
      clearedAtUtc: null,
      outcome: null,
    });
    step('Escalate', why, to, to);
  };
  const endAssignment = (id: number, why: string | null): void => {
    r.assignments = r.assignments.map((a) =>
      a.id === id
        ? {
            ...a,
            unassignedAtUtc: ctx.now,
            unassignedByUserId: ctx.actor.userId,
            unassignedByName: ctx.actor.name,
            reason: why ?? a.reason,
          }
        : a,
    );
  };
  const decide = (decision: 'Approved' | 'Rejected', why: string | null): string => {
    const pending = r.proposals.find((p) => p.decision === 'Pending');
    if (!pending) {
      throw mockError('Conflict', 'There is no pending resolution proposal.');
    }
    Object.assign(pending, {
      decision,
      decidedByUserId: ctx.actor.userId,
      decidedByName: ctx.actor.name,
      decidedAtUtc: ctx.now,
      decisionComment: why,
    });
    return pending.summary;
  };

  switch (command.action) {
    case 'submit': {
      if (!command.categoryActive) {
        invalid('categoryId', 'The category of this draft is no longer active.');
      }
      if (r.isAnonymous && !command.categoryAllowsAnonymous) {
        invalid('isAnonymous', 'This category cannot be reported anonymously.');
      }
      const placement = r.isAnonymous ? null : command.placement;
      r.companyId = placement?.companyId ?? null;
      r.branchId = placement?.branchId ?? null;
      r.departmentId = placement?.departmentId ?? null;
      r.positionId = placement?.positionId ?? null;
      r.caseNo = command.caseNo;
      r.submittedAtUtc = ctx.now;
      r.declarationAcceptedAtUtc = ctx.now;
      step('Submit', null);
      break;
    }
    case 'acknowledge':
      r.priority = command.priority;
      r.targetResolutionDate =
        command.targetResolutionDate ?? addDays(ctx.today, command.defaultTargetDays);
      r.acknowledgedAtUtc = ctx.now;
      step('Acknowledge', text);
      if (command.priority === 'Critical' && !r.isEscalated) {
        escalate(GRIEVANCE_CRITICAL_ACK_REASON, true);
      }
      break;
    case 'reject':
      r.rejectionReason = text;
      r.retentionUntil = grievanceRetentionUntil(ctx.today);
      step('Reject', text);
      break;
    case 'startReview':
      step('StartReview', text);
      break;
    case 'assign': {
      const investigator = command.investigatorEmployeeId;
      if (
        investigator === r.subjectEmployeeId ||
        (command.namedSubmitterEmployeeId !== null &&
          investigator === command.namedSubmitterEmployeeId)
      ) {
        invalid('investigatorEmployeeId', GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE);
      }
      const active = r.assignments.filter((a) => a.unassignedAtUtc === null);
      const existing = active.find((a) => a.investigatorEmployeeId === investigator);
      if (existing && !(existing.role === 'Member' && command.role === 'Lead')) {
        throw mockError('Conflict', 'This investigator is already assigned to the case.');
      }
      if (command.role === 'Lead') {
        const lead = active.find((a) => a.role === 'Lead');
        // Replacing the Lead of a case under way keeps the status and needs a reason.
        if (lead && from === to && text === null) {
          invalid('reason', 'Say why the lead investigator is replaced.');
        }
        if (lead) {
          endAssignment(lead.id, text);
        }
      }
      if (existing) {
        endAssignment(existing.id, text);
      }
      r.assignments.push({
        id: ctx.nextId('assignments'),
        investigatorEmployeeId: investigator,
        role: command.role,
        assignedAtUtc: ctx.now,
        assignedByUserId: ctx.actor.userId,
        assignedByName: ctx.actor.name,
        unassignedAtUtc: null,
        unassignedByUserId: null,
        unassignedByName: null,
        reason: text,
      });
      step('Assign', text);
      break;
    }
    case 'unassign': {
      const assignment = r.assignments.find(
        (a) =>
          a.unassignedAtUtc === null &&
          (command.assignmentId !== undefined
            ? a.id === command.assignmentId
            : a.investigatorEmployeeId === command.investigatorEmployeeId),
      );
      if (!assignment) {
        invalid(
          command.assignmentId !== undefined ? 'assignmentId' : 'investigatorEmployeeId',
          'This is not an active assignment of the case.',
        );
      }
      if (assignment.role === 'Lead') {
        throw mockError(
          'Conflict',
          'The lead investigator cannot be removed; assign a new lead to replace them.',
        );
      }
      endAssignment(assignment.id, text);
      step('Unassign', text);
      break;
    }
    case 'startInvestigation':
      step('StartInvestigation', text);
      break;
    case 'requestInfo':
      r.returnStatus = from;
      communicate('ToSubmitter', 'InfoRequest', text!, null);
      step('RequestInfo', text);
      break;
    case 'respond': {
      const lastRequest = [...r.communications].reverse().find((m) => m.kind === 'InfoRequest');
      communicate(
        'FromSubmitter',
        'InfoResponse',
        text!,
        command.inReplyToId ?? lastRequest?.id ?? null,
      );
      r.returnStatus = null;
      step('Respond', null);
      break;
    }
    case 'postSubmitterMessage':
      communicate('FromSubmitter', 'Message', text!, command.inReplyToId ?? null);
      break;
    case 'postTeamMessage':
      communicate('ToSubmitter', 'Message', text!, null);
      break;
    case 'proposeResolution':
      r.proposals.push({
        id: ctx.nextId('proposals'),
        summary: text!,
        proposedByUserId: ctx.actor.userId,
        proposedByEmployeeId: ctx.actor.employeeId,
        proposedByName: ctx.actor.name,
        proposedAtUtc: ctx.now,
        decision: 'Pending',
        decidedByUserId: null,
        decidedByName: null,
        decidedAtUtc: null,
        decisionComment: null,
      });
      step('ProposeResolution', null);
      break;
    case 'approveResolution':
      r.resolutionSummary = decide('Approved', text);
      r.resolutionVisibleToSubmitter = command.visibleToSubmitter;
      r.resolvedAtUtc = ctx.now;
      step('ApproveResolution', text);
      break;
    case 'rejectResolution':
      decide('Rejected', text);
      step('RejectResolution', text);
      break;
    case 'close':
      r.closedAtUtc = ctx.now;
      r.retentionUntil = grievanceRetentionUntil(ctx.today);
      step('Close', text);
      break;
    case 'reopen':
      step('Reopen', text);
      // Reopening ends every active assignment (the lead included), each with a hidden Unassign
      // step: the case leaves the investigators' lists until HR assigns a lead again.
      for (const a of r.assignments.filter((x) => x.unassignedAtUtc === null)) {
        endAssignment(a.id, GRIEVANCE_REOPEN_UNASSIGN_REASON);
        step('Unassign', GRIEVANCE_REOPEN_UNASSIGN_REASON, to, to);
      }
      break;
    case 'escalate':
      escalate(text!, command.isImmediate ?? false);
      break;
    case 'archive':
      r.archivedAtUtc = ctx.now;
      step('Archive', text);
      break;
  }

  // The submitter is never identified in what a case records: no ids, and a fixed name (the
  // case-team screens show a named submitter's name from the submitter record).
  const submitter = ctx.actor.role === 'Submitter';
  const recorded = submitter
    ? {
        actorUserId: null,
        actorEmployeeId: null,
        actorName: r.isAnonymous ? GRIEVANCE_ANONYMOUS_ACTOR_NAME : GRIEVANCE_SUBMITTER_ACTOR_NAME,
      }
    : {
        actorUserId: ctx.actor.userId,
        actorEmployeeId: ctx.actor.employeeId,
        actorName: ctx.actor.name,
      };
  for (const s of steps) {
    const visible = grievanceStepVisibleToSubmitter(s.action, s.from, s.to);
    r.history.push({
      id: ctx.nextId('history'),
      fromStatus: s.from,
      toStatus: s.to,
      action: s.action,
      actorRole: ctx.actor.role,
      ...recorded,
      reason: s.reason,
      visibleToSubmitter: visible,
      occurredAtUtc: ctx.now,
    });
    if (visible) {
      r.lastSubmitterUpdateAtUtc = ctx.now;
    }
  }
  return r;
}
