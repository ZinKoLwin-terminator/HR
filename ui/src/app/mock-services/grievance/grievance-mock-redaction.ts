import { Injectable, inject } from '@angular/core';
import { CURRENT_EMPLOYMENT_STATUSES, fullName } from '../../core/models/employee.models';
import {
  GRIEVANCE_ANONYMOUS_ACTOR_NAME,
  GRIEVANCE_ANONYMOUS_LABEL,
  GRIEVANCE_SUBMITTER_ACTOR_NAME,
  GrievanceActorRole,
  GrievanceAttachment,
  GrievanceAttachmentVisibility,
  GrievanceCase,
  GrievanceCaseDetail,
  GrievanceEmployeeOption,
  GrievanceHistoryEntry,
  GrievanceHistoryAction,
  GrievanceSubmitter,
  GrievanceViewerRole,
  MyGrievance,
  MyGrievanceActor,
  MyGrievanceAttachment,
  MyGrievanceDetail,
} from '../../core/models/grievance.models';
import {
  GrievanceAttachmentRecord,
  GrievanceCaseRecord,
} from '../../mock-data/grievance/grievance-records';
import { todayIso } from '../../shared/utilities/date.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import {
  BranchMockService,
  DepartmentMockService,
  PositionMockService,
} from '../organization/organization-mock.services';
import { GrievanceCategoryMockService } from './grievance-category-mock.service';
import { GrievanceMockStore } from './grievance-mock-store';
import { isGrievanceOpen } from '../../core/utilities/grievance-workflow';

/** Reasons the submitter may read on their timeline. */
const SUBMITTER_REASONS: readonly GrievanceHistoryAction[] = ['Reject', 'Reopen', 'RequestInfo'];

/** Reasons a reviewer-only caller may read in the history (the proposal and its decisions). */
const REVIEWER_REASONS: readonly GrievanceHistoryAction[] = [
  'ProposeResolution',
  'ApproveResolution',
  'RejectResolution',
];

/** Which attachment visibilities a set of tiers may open. */
export function grievanceAttachmentVisibleTo(
  tiers: readonly GrievanceViewerRole[],
  visibility: GrievanceAttachmentVisibility,
): boolean {
  if (tiers.includes('Management')) {
    return true;
  }
  if (tiers.includes('Investigator')) {
    return visibility !== 'Restricted';
  }
  return tiers.includes('Reviewer') && visibility === 'Shared';
}

const asMyActor = (role: GrievanceActorRole): MyGrievanceActor =>
  role === 'Submitter' ? 'You' : role === 'System' ? 'System' : 'HR team';

/** Open and the target date is before today. */
export function grievanceIsOverdue(record: GrievanceCaseRecord, today = todayIso()): boolean {
  return (
    isGrievanceOpen(record.status) &&
    record.targetResolutionDate !== null &&
    record.targetResolutionDate < today
  );
}

/**
 * Every mapping from a stored case to what a caller receives, in one place (the API's
 * `GrievanceRedaction`). The submitter's view names nobody on the HR side; the case team never gets
 * the submitter of an anonymous case; sections are trimmed by tier. Simulation for the demo.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceMockRedaction {
  private readonly categories = inject(GrievanceCategoryMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly branches = inject(BranchMockService);
  private readonly positions = inject(PositionMockService);
  private readonly store = inject(GrievanceMockStore);

  categoryName(id: number): string {
    return this.categories.findById(id)?.name ?? `Category #${id}`;
  }

  departmentName(id: number | null): string | null {
    return id === null ? null : (this.departments.findById(id)?.name ?? `Department #${id}`);
  }

  employeeName(id: number | null): string | null {
    if (id === null) {
      return null;
    }
    const employee = this.employees.findById(id);
    return employee ? fullName(employee) : `Employee #${id}`;
  }

  employeeCode(id: number): string {
    return this.employees.findById(id)?.employeeCode ?? `#${id}`;
  }

  /** Current employees matching a code or name, by name (the triage subject picker). */
  employeeOptions(search: string, max: number): GrievanceEmployeeOption[] {
    const needle = search.trim().toLowerCase();
    return this.employees
      .records()
      .filter((e) => CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus))
      .map((e) => ({
        employeeId: e.id,
        employeeCode: e.employeeCode,
        displayName: fullName(e),
        departmentName: this.departmentName(e.departmentId),
        positionName: this.positions.findById(e.positionId)?.title ?? null,
      }))
      .filter(
        (o) =>
          needle === '' ||
          o.displayName.toLowerCase().includes(needle) ||
          o.employeeCode.toLowerCase().includes(needle),
      )
      .sort((a, b) => a.displayName.localeCompare(b.displayName))
      .slice(0, max);
  }

  employeeExists(id: number): boolean {
    return this.employees.findById(id) !== undefined;
  }

  employeeDepartmentId(id: number): number | null {
    return this.employees.findById(id)?.departmentId ?? null;
  }

  employeePositionTitle(id: number): string | null {
    const positionId = this.employees.findById(id)?.positionId;
    return positionId === undefined ? null : (this.positions.findById(positionId)?.title ?? null);
  }

  // ---- Submitter's view -----------------------------------------------------------------------

  myCase(record: GrievanceCaseRecord): MyGrievance {
    const seen = this.store.seenAt(record.id);
    const last = record.lastSubmitterUpdateAtUtc;
    return {
      id: record.id,
      caseNo: record.caseNo,
      categoryId: record.categoryId,
      categoryName: this.categoryName(record.categoryId),
      subject: record.subject,
      status: record.status,
      isAnonymous: record.isAnonymous,
      createdAtUtc: record.createdAtUtc,
      submittedAtUtc: record.submittedAtUtc,
      targetResolutionDate: record.targetResolutionDate,
      lastUpdateAtUtc: last,
      hasUnreadUpdate: last !== null && (seen === null || last > seen),
      rowVersion: record.rowVersion,
    };
  }

  private myAttachment(a: GrievanceAttachmentRecord): MyGrievanceAttachment {
    return {
      id: a.id,
      fileName: a.fileName,
      contentType: a.contentType,
      sizeBytes: a.sizeBytes,
      uploadedBy: asMyActor(a.uploadedByRole),
      uploadedAtUtc: a.uploadedAtUtc,
    };
  }

  myDetail(record: GrievanceCaseRecord): MyGrievanceDetail {
    const resolutionShown =
      (record.status === 'Resolved' || record.status === 'Closed') &&
      record.resolutionVisibleToSubmitter;
    return {
      ...this.myCase(record),
      description: record.description,
      incidentDate: record.incidentDate,
      location: record.location,
      involvedPersonText: record.involvedPersonText,
      involvedDepartmentId: record.involvedDepartmentId,
      involvedDepartmentName: this.departmentName(record.involvedDepartmentId),
      declarationAcceptedAtUtc: record.declarationAcceptedAtUtc,
      acknowledgedAtUtc: record.acknowledgedAtUtc,
      resolvedAtUtc: record.resolvedAtUtc,
      closedAtUtc: record.closedAtUtc,
      rejectionReason: record.status === 'Rejected' ? record.rejectionReason : null,
      resolutionSummary: resolutionShown ? record.resolutionSummary : null,
      timeline: record.history
        .filter((h) => h.visibleToSubmitter)
        .map((h) => ({
          fromStatus: h.fromStatus,
          toStatus: h.toStatus,
          action: h.action,
          actor: asMyActor(h.actorRole),
          reason: SUBMITTER_REASONS.includes(h.action) ? h.reason : null,
          occurredAtUtc: h.occurredAtUtc,
        })),
      messages: record.communications.map((m) => ({
        id: m.id,
        direction: m.direction,
        kind: m.kind,
        body: m.body,
        actor: m.direction === 'FromSubmitter' ? 'You' : 'HR team',
        inReplyToId: m.inReplyToId,
        createdAtUtc: m.createdAtUtc,
      })),
      attachments: record.attachments
        .filter((a) => a.removedAtUtc === null && a.visibility === 'Shared')
        .map((a) => this.myAttachment(a)),
    };
  }

  myAttachmentOf(a: GrievanceAttachmentRecord): MyGrievanceAttachment {
    return this.myAttachment(a);
  }

  // ---- Case team ------------------------------------------------------------------------------

  teamCase(record: GrievanceCaseRecord, today = todayIso()): GrievanceCase {
    const lead = record.assignments.find((a) => a.role === 'Lead' && a.unassignedAtUtc === null);
    const named = record.isAnonymous ? null : this.store.namedSubmitterId(record.id);
    return {
      id: record.id,
      caseNo: record.caseNo ?? '',
      categoryId: record.categoryId,
      categoryName: this.categoryName(record.categoryId),
      subject: record.subject,
      status: record.status,
      priority: record.priority,
      isAnonymous: record.isAnonymous,
      submitterLabel: record.isAnonymous
        ? GRIEVANCE_ANONYMOUS_LABEL
        : (this.employeeName(named) ?? GRIEVANCE_ANONYMOUS_LABEL),
      departmentId: record.isAnonymous ? null : record.departmentId,
      departmentName: record.isAnonymous ? null : this.departmentName(record.departmentId),
      submittedAtUtc: record.submittedAtUtc ?? record.createdAtUtc,
      targetResolutionDate: record.targetResolutionDate,
      isOverdue: grievanceIsOverdue(record, today),
      isEscalated: record.isEscalated,
      leadInvestigatorEmployeeId: lead?.investigatorEmployeeId ?? null,
      leadInvestigatorName: lead ? this.employeeName(lead.investigatorEmployeeId) : null,
      archivedAtUtc: record.archivedAtUtc,
      rowVersion: record.rowVersion,
    };
  }

  private submitter(record: GrievanceCaseRecord): GrievanceSubmitter | null {
    if (record.isAnonymous) {
      return null;
    }
    const employeeId = this.store.namedSubmitterId(record.id);
    if (employeeId === null) {
      return null;
    }
    return {
      employeeId,
      employeeCode: this.employeeCode(employeeId),
      displayName: this.employeeName(employeeId) ?? '',
      companyId: record.companyId,
      branchId: record.branchId,
      branchName:
        record.branchId === null
          ? null
          : (this.branches.findById(record.branchId)?.name ?? `Branch #${record.branchId}`),
      departmentId: record.departmentId,
      departmentName: this.departmentName(record.departmentId),
      positionId: record.positionId,
      positionName:
        record.positionId === null
          ? null
          : (this.positions.findById(record.positionId)?.title ?? `Position #${record.positionId}`),
    };
  }

  teamAttachment(a: GrievanceAttachmentRecord): GrievanceAttachment {
    return {
      id: a.id,
      fileName: a.fileName,
      contentType: a.contentType,
      sizeBytes: a.sizeBytes,
      visibility: a.visibility,
      uploadedByRole: a.uploadedByRole,
      uploadedAtUtc: a.uploadedAtUtc,
      communicationId: a.communicationId,
      noteId: a.noteId,
    };
  }

  /** The detail trimmed to the caller's tiers (see `GrievanceCaseDetail`). */
  teamDetail(
    record: GrievanceCaseRecord,
    tiers: GrievanceViewerRole[],
    actorEmployeeId: number | null,
  ): GrievanceCaseDetail {
    const management = tiers.includes('Management');
    // A reviewer-only caller gets no assignment or escalation reasons (case-team material).
    const reviewerOnly = !management && !tiers.includes('Investigator');
    const investigator = tiers.includes('Investigator');
    const lead = record.assignments.find((a) => a.role === 'Lead' && a.unassignedAtUtc === null);
    const pending = record.proposals.find((p) => p.decision === 'Pending');
    const latest = pending ?? [...record.proposals].sort((a, b) => b.id - a.id)[0];
    return {
      ...this.teamCase(record),
      submitter: this.submitter(record),
      viewerRoles: [...tiers],
      isLeadInvestigator:
        investigator && lead !== undefined && lead.investigatorEmployeeId === actorEmployeeId,
      description: record.description,
      incidentDate: record.incidentDate,
      location: record.location,
      involvedPersonText: record.involvedPersonText,
      involvedDepartmentId: record.involvedDepartmentId,
      involvedDepartmentName: this.departmentName(record.involvedDepartmentId),
      subjectEmployeeId: record.subjectEmployeeId,
      subjectEmployeeName: this.employeeName(record.subjectEmployeeId),
      returnStatus: record.returnStatus,
      declarationAcceptedAtUtc: record.declarationAcceptedAtUtc ?? record.createdAtUtc,
      acknowledgedAtUtc: record.acknowledgedAtUtc,
      resolvedAtUtc: record.resolvedAtUtc,
      closedAtUtc: record.closedAtUtc,
      resolutionSummary: record.resolutionSummary,
      resolutionVisibleToSubmitter: record.resolutionVisibleToSubmitter,
      rejectionReason: record.rejectionReason,
      retentionUntil: record.retentionUntil,
      legalHold: record.legalHold,
      assignments: [...record.assignments]
        .sort(
          (a, b) =>
            Number(a.unassignedAtUtc !== null) - Number(b.unassignedAtUtc !== null) || b.id - a.id,
        )
        .map((a) => ({
          id: a.id,
          investigatorEmployeeId: a.investigatorEmployeeId,
          investigatorCode: this.employeeCode(a.investigatorEmployeeId),
          investigatorName: this.employeeName(a.investigatorEmployeeId) ?? '',
          role: a.role,
          assignedAtUtc: a.assignedAtUtc,
          assignedByName: a.assignedByName,
          unassignedAtUtc: a.unassignedAtUtc,
          unassignedByName: a.unassignedByName,
          reason: reviewerOnly ? null : a.reason,
        })),
      notes: record.notes
        .filter(
          (n) => management || investigator || n.kind === 'Finding' || n.kind === 'Recommendation',
        )
        .map((n) => ({
          id: n.id,
          kind: n.kind,
          body: n.body,
          interviewDate: n.interviewDate,
          intervieweeText: n.intervieweeText,
          authorEmployeeId: n.authorEmployeeId,
          authorName: n.authorName,
          createdAtUtc: n.createdAtUtc,
        })),
      messages:
        management || investigator
          ? record.communications.map((m) => ({
              id: m.id,
              direction: m.direction,
              kind: m.kind,
              body: m.body,
              authorRole: m.authorRole,
              authorName: m.authorName,
              inReplyToId: m.inReplyToId,
              createdAtUtc: m.createdAtUtc,
            }))
          : [],
      attachments: record.attachments
        .filter((a) => a.removedAtUtc === null && grievanceAttachmentVisibleTo(tiers, a.visibility))
        .map((a) => this.teamAttachment(a)),
      correctiveActions: management
        ? record.correctiveActions.map((c) => ({
            id: c.id,
            caseId: record.id,
            actionType: c.actionType,
            description: c.description,
            responsibleText: c.responsibleText,
            dueDate: c.dueDate,
            completedOn: c.completedOn,
            status: c.status,
            rowVersion: c.rowVersion,
          }))
        : [],
      proposal: latest
        ? {
            id: latest.id,
            summary: latest.summary,
            proposedByEmployeeId: latest.proposedByEmployeeId,
            proposedByName: latest.proposedByName,
            proposedAtUtc: latest.proposedAtUtc,
            decision: latest.decision,
            decidedByName: latest.decidedByName,
            decidedAtUtc: latest.decidedAtUtc,
            decisionComment: latest.decisionComment,
          }
        : null,
      escalations: [...record.escalations]
        .sort((a, b) => b.escalatedAtUtc.localeCompare(a.escalatedAtUtc) || b.id - a.id)
        .map((e) => ({
          id: e.id,
          reason: reviewerOnly ? null : e.reason,
          isImmediate: e.isImmediate,
          escalatedByName: e.escalatedByName,
          escalatedAtUtc: e.escalatedAtUtc,
          clearedAtUtc: e.clearedAtUtc,
          outcome: e.outcome,
        })),
    };
  }

  /**
   * The history, oldest first. Submitter steps never carry ids; on a named case the case team sees
   * the submitter's name (from the submitter record), on an anonymous one "Anonymous submitter". A
   * reviewer-only caller does not see the RequestInfo question (investigation material).
   */
  history(
    record: GrievanceCaseRecord,
    tiers: readonly GrievanceViewerRole[],
  ): GrievanceHistoryEntry[] {
    const reviewerOnly = !tiers.includes('Management') && !tiers.includes('Investigator');
    const named = record.isAnonymous
      ? null
      : this.employeeName(this.store.namedSubmitterId(record.id));
    return record.history.map((h) => {
      const entry = { ...h };
      if (h.actorRole === 'Submitter') {
        entry.actorUserId = null;
        entry.actorEmployeeId = null;
        entry.actorName = record.isAnonymous
          ? GRIEVANCE_ANONYMOUS_ACTOR_NAME
          : (named ?? GRIEVANCE_SUBMITTER_ACTOR_NAME);
      }
      // A reviewer-only caller keeps only the reasons of the proposal and its decisions.
      if (reviewerOnly && !REVIEWER_REASONS.includes(h.action)) {
        entry.reason = null;
      }
      return entry;
    });
  }
}
