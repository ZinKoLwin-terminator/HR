import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap, timer } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AppError, PagedResult, PageRequest } from '../../core/models/common.models';
import {
  AcknowledgeGrievanceRequest,
  ApproveResolutionRequest,
  AssignInvestigatorRequest,
  EscalateGrievanceRequest,
  GRIEVANCE_ATTACHMENT_VISIBILITIES,
  GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE,
  GRIEVANCE_CASE_SORT_KEYS,
  GRIEVANCE_CORRECTIVE_ACTION_STATUSES,
  GRIEVANCE_EMPLOYEE_OPTIONS_MAX,
  GRIEVANCE_LIMITS,
  GRIEVANCE_NOTE_KINDS,
  GRIEVANCE_PRIORITIES,
  GrievanceActorRole,
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
  GrievanceViewerRole,
  ProposeResolutionRequest,
  RequestGrievanceInfoRequest,
  UnassignInvestigatorRequest,
} from '../../core/models/grievance.models';
import { GrievanceCaseDataService } from '../../core/services/data/grievance-data.services';
import { inspectEvidence, readFileBytes } from '../../core/utilities/kpi-evidence';
import {
  GrievanceCaseRecord,
  GrievanceCorrectiveActionRecord,
  nextGrievanceRowVersion,
} from '../../mock-data/grievance/grievance-records';
import { todayIso } from '../../shared/utilities/date.util';
import {
  isEmptyFilterValue,
  matchesSearch,
  paginate,
  sortItems,
} from '../../shared/utilities/query.util';
import { mockError, mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import { UserMockService } from '../settings/settings-mock.services';
import { GrievanceCategoryMockService } from './grievance-category-mock.service';
import { GrievanceModule, GrievanceMockAccess, GrievanceMockActor } from './grievance-mock-access';
import {
  GrievanceMockRedaction,
  grievanceAttachmentVisibleTo,
  grievanceIsOverdue,
} from './grievance-mock-redaction';
import {
  ensureCurrent,
  isoDate,
  requireRowVersion,
  requiredText,
  text,
  validation,
} from './grievance-mock-rules';
import { GrievanceMockStore } from './grievance-mock-store';
import {
  GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE,
  GRIEVANCE_CRITICAL_TRIAGE_REASON,
} from './grievance-case-domain';
import {
  GrievanceDomainCommand,
  applyGrievanceCommand,
  grievanceWorkflowActionOf,
} from './grievance-case-domain';
import {
  GrievanceActorRights,
  grievanceActorRefusal,
  isGrievanceOpen,
} from '../../core/utilities/grievance-workflow';

type Permission = [GrievanceModule, 'View' | 'Create' | 'Edit' | 'Delete' | 'Approve'];

const MANAGEMENT_EDIT: Permission = ['grievance-management', 'Edit'];
const INVESTIGATION_EDIT: Permission = ['grievance-investigation', 'Edit'];
const REVIEW_APPROVE: Permission = ['grievance-review', 'Approve'];
const ANY_VIEW: Permission[] = [
  ['grievance-management', 'View'],
  ['grievance-investigation', 'View'],
  ['grievance-review', 'View'],
];

/**
 * Where the case team may add files: an open case that is not archived (the API's
 * `EnsureFilesOpen`). Resolved, Rejected and Closed cases take no new files (409).
 */
const FILES_OPEN = (record: GrievanceCaseRecord): boolean =>
  record.archivedAtUtc === null && isGrievanceOpen(record.status);

/**
 * The case team's grievance work (`/api/grievances/cases...`): lists per tier, the detail trimmed by
 * tier, every state-machine command, triage, messages, notes, files, corrective actions and the
 * access log, with the API's order of checks: permission (403), body (400), row version (400),
 * scope (404), actor (403), stale (409), status (409).
 *
 * BROWSER SIMULATION, NO REAL SECURITY: the tiers come from the simulated session's role and its
 * grants in Settings; nothing is protected. It shows how the API behaves, nothing more.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceCaseMockService implements GrievanceCaseDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly access = inject(GrievanceMockAccess);
  private readonly store = inject(GrievanceMockStore);
  private readonly redaction = inject(GrievanceMockRedaction);
  private readonly categories = inject(GrievanceCategoryMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly users = inject(UserMockService);

  private respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  /** The endpoint's authorization (403 before anything else). */
  private endpoint(...anyOf: Permission[]): GrievanceMockActor {
    const actor = this.access.actor();
    this.access.require(actor, ...anyOf);
    return actor;
  }

  private now(): string {
    return new Date().toISOString();
  }

  private auditRole(tiers: readonly GrievanceViewerRole[]): GrievanceActorRole {
    return tiers.includes('Management')
      ? 'Management'
      : tiers.includes('Investigator')
        ? 'Investigator'
        : 'Reviewer';
  }

  private refuse(record: GrievanceCaseRecord, actor: GrievanceMockActor, message: string): never {
    this.store.audit(
      record.id,
      'AccessRefused',
      actor.userId,
      this.auditRole(this.access.tiers(record, actor)),
      'forbidden',
    );
    throw mockError('Forbidden', message);
  }

  private detail(record: GrievanceCaseRecord, actor: GrievanceMockActor): GrievanceCaseDetail {
    return this.redaction.teamDetail(record, this.access.tiers(record, actor), actor.employeeId);
  }

  private actorName(actor: GrievanceMockActor): string {
    return (
      (actor.employeeId !== null ? this.redaction.employeeName(actor.employeeId) : null) ??
      actor.name
    );
  }

  /**
   * One case-team step through the shared domain (`grievance-case-domain.ts`): scope (404), actor
   * (403), stale (409), then the step itself (400 / 403 / 409). The row version moves on; the access
   * log and the notifications follow.
   */
  private step(
    id: number,
    actor: GrievanceMockActor,
    givenRowVersion: string,
    command: GrievanceDomainCommand,
    prefer: 'team' | 'review' = 'team',
  ): { after: GrievanceCaseRecord; detail: GrievanceCaseDetail } {
    const before = this.access.visibleCase(id, actor);
    const rights = this.access.rights(before, actor);
    const refusal = grievanceActorRefusal(grievanceWorkflowActionOf(command), rights);
    if (refusal !== null) {
      this.refuse(before, actor, refusal);
    }
    ensureCurrent(before.rowVersion, givenRowVersion);
    const role = this.access.actingRole(rights, prefer);
    const counters = new Map<string, number>();
    let after: GrievanceCaseRecord;
    try {
      after = applyGrievanceCommand(before, command, {
        now: this.now(),
        today: todayIso(),
        actor: {
          userId: actor.userId,
          employeeId: actor.employeeId,
          name: this.actorName(actor),
          role,
        },
        rights,
        nextId: (kind) => {
          const next = counters.get(kind) ?? this.store.nextChildId(kind);
          counters.set(kind, next + 1);
          return next;
        },
      });
    } catch (error) {
      const failure = error as AppError;
      if (failure.code === 'Forbidden') {
        this.refuse(before, actor, failure.message);
      }
      if (failure.message === GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE) {
        this.store.audit(id, 'AccessRefused', actor.userId, role, 'conflict-of-interest');
      }
      throw error;
    }
    after.rowVersion = nextGrievanceRowVersion(before.rowVersion);
    this.store.save(after);
    const added = after.history.slice(before.history.length);
    const main = added[0];
    const auditAction =
      command.action === 'assign'
        ? 'Assign'
        : command.action === 'unassign'
          ? 'Unassign'
          : main
            ? 'StatusChange'
            : 'SendMessage';
    const detail = main
      ? main.fromStatus === main.toStatus
        ? main.action
        : `${main.fromStatus}>${main.toStatus}`
      : null;
    this.store.audit(id, auditAction, actor.userId, role, detail);
    // The API's step pipeline: the named submitter hears of any step they can see; the reviewers
    // of an immediate escalation (a proposal tells them itself, in proposeResolution).
    if (added.some((h) => h.visibleToSubmitter)) {
      this.access.notifySubmitter(after);
    }
    const escalated = after.escalations.slice(before.escalations.length);
    if (command.action !== 'proposeResolution' && escalated.some((e) => e.isImmediate)) {
      this.access.notifyReviewers(after, 'it was escalated as urgent');
    }
    return { after, detail: this.detail(after, actor) };
  }

  /** Case work outside the state machine: scope (404), rights (403), stale (409), status (409). */
  private caseWork(
    id: number,
    actor: GrievanceMockActor,
    givenRowVersion: string,
    allowed: (rights: GrievanceActorRights) => boolean,
    statusOk: (record: GrievanceCaseRecord) => boolean,
    statusMessage: string,
    /** False where the step does not change the case row (an old well-formed version is fine). */
    checkStale = true,
  ): { record: GrievanceCaseRecord; rights: GrievanceActorRights; role: GrievanceActorRole } {
    const record = this.access.visibleCase(id, actor);
    const rights = this.access.rights(record, actor);
    if (!allowed(rights)) {
      this.refuse(record, actor, 'You are not allowed to do this on this case.');
    }
    if (checkStale) {
      ensureCurrent(record.rowVersion, givenRowVersion);
    }
    if (!statusOk(record)) {
      throw mockError('Conflict', statusMessage);
    }
    return { record, rights, role: this.access.actingRole(rights) };
  }

  // ---- Lists ----------------------------------------------------------------------------------

  private list(
    request: PageRequest<GrievanceCaseFilter>,
    actor: GrievanceMockActor,
    inScope: (record: GrievanceCaseRecord, tiers: GrievanceViewerRole[]) => boolean,
    defaultOrder: 'newest' | 'oldest',
    /** Only Case management honours the Archived filter; the other lists never show archived cases. */
    honourArchived = false,
  ): PagedResult<GrievanceCase> {
    if (request.sort?.active && !GRIEVANCE_CASE_SORT_KEYS.includes(request.sort.active)) {
      validation(`Sorting by "${request.sort.active}" is not supported.`, 'sort');
    }
    const filters: Partial<GrievanceCaseFilter> = request.filters ?? {};
    const today = todayIso();
    const set = <K extends keyof GrievanceCaseFilter>(key: K): boolean =>
      !isEmptyFilterValue(filters[key]);
    const rows = this.store
      .records()
      .filter((record) => {
        const tiers = this.access.tiers(record, actor);
        if (tiers.length === 0 || !inScope(record, tiers)) {
          return false;
        }
        const archived = record.archivedAtUtc !== null;
        if (honourArchived && filters.archived === true ? !archived : archived) {
          return false;
        }
        const submitted = (record.submittedAtUtc ?? '').slice(0, 10);
        return (
          (!set('status') || record.status === filters.status) &&
          (!set('priority') || record.priority === filters.priority) &&
          (!set('categoryId') || record.categoryId === filters.categoryId) &&
          (!set('departmentId') ||
            (!record.isAnonymous && record.departmentId === filters.departmentId)) &&
          (!set('isAnonymous') || record.isAnonymous === filters.isAnonymous) &&
          (!set('isEscalated') || record.isEscalated === filters.isEscalated) &&
          (!set('overdue') || grievanceIsOverdue(record, today) === filters.overdue) &&
          (!set('investigatorEmployeeId') ||
            record.assignments.some(
              (a) =>
                a.unassignedAtUtc === null &&
                a.investigatorEmployeeId === filters.investigatorEmployeeId,
            )) &&
          (!set('submittedFrom') || submitted >= filters.submittedFrom!) &&
          (!set('submittedTo') || submitted <= filters.submittedTo!)
        );
      })
      .map((record) => this.redaction.teamCase(record, today))
      .filter((row) => matchesSearch(row, request.search, ['caseNo', 'subject', 'categoryName']));
    const sorted = request.sort?.active
      ? sortItems(rows, request.sort)
      : rows.sort((a, b) =>
          defaultOrder === 'newest'
            ? b.submittedAtUtc.localeCompare(a.submittedAtUtc)
            : a.submittedAtUtc.localeCompare(b.submittedAtUtc),
        );
    return paginate(sorted, request.pageIndex, request.pageSize);
  }

  query(request: PageRequest<GrievanceCaseFilter>): Observable<PagedResult<GrievanceCase>> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-management', 'View']);
      return this.list(request, actor, (_, tiers) => tiers.includes('Management'), 'newest', true);
    });
  }

  investigations(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-investigation', 'View']);
      return this.list(request, actor, (_, tiers) => tiers.includes('Investigator'), 'newest');
    });
  }

  pendingReviews(
    request: PageRequest<GrievanceCaseFilter>,
  ): Observable<PagedResult<GrievanceCase>> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-review', 'View']);
      return this.list(
        request,
        actor,
        // Open cases only: an escalated open case, or a pending proposal (always open).
        (record, tiers) =>
          tiers.includes('Reviewer') &&
          isGrievanceOpen(record.status) &&
          this.access.inReviewQueue(record),
        'oldest',
      );
    });
  }

  // ---- Reads ----------------------------------------------------------------------------------

  getById(id: number): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(...ANY_VIEW);
      const record = this.access.visibleCase(id, actor);
      const tiers = this.access.tiers(record, actor);
      this.store.audit(id, 'ViewCase', actor.userId, this.auditRole(tiers));
      return this.redaction.teamDetail(record, tiers, actor.employeeId);
    });
  }

  history(id: number): Observable<GrievanceHistoryEntry[]> {
    return this.respond(() => {
      const actor = this.endpoint(...ANY_VIEW);
      const record = this.access.visibleCase(id, actor);
      this.store.audit(
        id,
        'ViewHistory',
        actor.userId,
        this.auditRole(this.access.tiers(record, actor)),
      );
      return this.redaction.history(record, this.access.tiers(record, actor));
    });
  }

  audit(
    id: number,
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-management', 'View']);
      if (!actor.isAdmin) {
        throw mockError('Forbidden', 'Only an administrator can read the access log of a case.');
      }
      const record = this.access.visibleCase(id, actor);
      if (!this.access.tiers(record, actor).includes('Management')) {
        throw mockError('NotFound', `Grievance case #${id} was not found.`);
      }
      return this.auditPage(request, (a) => a.caseId === id);
    });
  }

  auditLog(
    request: PageRequest<GrievanceAuditFilter>,
  ): Observable<PagedResult<GrievanceAuditEntry>> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-management', 'View']);
      if (!actor.isAdmin) {
        throw mockError('Forbidden', 'Only an administrator can read the grievance access log.');
      }
      // Module-wide, the Excel exports (no case) included.
      return this.auditPage(request, () => true);
    });
  }

  /** One page of the access log, newest first, with the action and the date range. */
  private auditPage(
    request: PageRequest<GrievanceAuditFilter>,
    keep: (row: { caseId: number | null }) => boolean,
  ): PagedResult<GrievanceAuditEntry> {
    const f: Partial<GrievanceAuditFilter> = request.filters ?? {};
    const rows: GrievanceAuditEntry[] = this.store
      .audits()
      .filter((a) => {
        const day = a.occurredAtUtc.slice(0, 10);
        return (
          keep(a) &&
          (isEmptyFilterValue(f.action) || a.action === f.action) &&
          (isEmptyFilterValue(f.from) || day >= f.from!) &&
          (isEmptyFilterValue(f.to) || day <= f.to!)
        );
      })
      .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc) || b.id - a.id)
      .map((a) => ({
        ...a,
        caseNo: a.caseId === null ? null : (this.store.find(a.caseId)?.caseNo ?? null),
        actorName:
          a.actorUserId === null
            ? null
            : (this.users.findById(a.actorUserId)?.displayName ?? `User #${a.actorUserId}`),
      }));
    return paginate(rows, request.pageIndex, request.pageSize);
  }

  investigators(search?: string | null): Observable<GrievanceInvestigatorOption[]> {
    return this.respond(() => {
      this.endpoint(MANAGEMENT_EDIT);
      // Never filtered per case: a missing name would reveal the submitter of an anonymous case.
      const options = this.access
        .usersWith('grievance-investigation', 'Edit')
        .map((user) => {
          const name = this.redaction.employeeName(user.employeeId) ?? user.displayName;
          return {
            employeeId: user.employeeId,
            employeeCode: this.redaction.employeeCode(user.employeeId),
            displayName: name,
            departmentName: this.redaction.departmentName(
              this.redaction.employeeDepartmentId(user.employeeId),
            ),
            positionName: this.redaction.employeePositionTitle(user.employeeId),
            activeCaseCount: this.store
              .records()
              .filter(
                (c) =>
                  isGrievanceOpen(c.status) &&
                  c.assignments.some(
                    (a) =>
                      a.unassignedAtUtc === null && a.investigatorEmployeeId === user.employeeId,
                  ),
              ).length,
          };
        })
        .filter((o) => matchesSearch(o, search ?? undefined, ['employeeCode', 'displayName']));
      return options.sort((a, b) => a.displayName.localeCompare(b.displayName));
    });
  }

  employeeOptions(search: string): Observable<GrievanceEmployeeOption[]> {
    return this.respond(() => {
      this.endpoint(MANAGEMENT_EDIT);
      return this.redaction.employeeOptions(search ?? '', GRIEVANCE_EMPLOYEE_OPTIONS_MAX);
    });
  }

  // ---- State machine --------------------------------------------------------------------------

  acknowledge(id: number, request: AcknowledgeGrievanceRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      const target = isoDate(
        request.targetResolutionDate,
        'targetResolutionDate',
        'Target date',
        false,
      );
      const record = this.store.find(id);
      const days = this.categories.findById(record?.categoryId ?? 0)?.defaultTargetDays ?? 30;
      return this.step(id, actor, rowVersion, {
        action: 'acknowledge',
        priority: request.priority,
        targetResolutionDate: target,
        defaultTargetDays: days,
        comment: request.comment,
      }).detail;
    });
  }

  startReview(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'startReview', comment: request.comment })
        .detail;
    });
  }

  assign(id: number, request: AssignInvestigatorRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      const investigatorId = request.investigatorEmployeeId;
      if (
        !Number.isInteger(investigatorId) ||
        !this.access
          .usersWith('grievance-investigation', 'Edit')
          .some((u) => u.employeeId === investigatorId)
      ) {
        validation('Choose an employee who can investigate.', 'investigatorEmployeeId');
      }
      const record = this.store.find(id);
      const { after, detail } = this.step(id, actor, rowVersion, {
        action: 'assign',
        investigatorEmployeeId: investigatorId,
        role: request.role,
        reason: request.reason ?? null,
        // Only a NAMED submitter is checked: refusing on an anonymous case would reveal them.
        namedSubmitterEmployeeId:
          record && !record.isAnonymous ? this.store.namedSubmitterId(record.id) : null,
      });
      this.access.notifyInvestigator(after, investigatorId);
      return detail;
    });
  }

  unassign(id: number, request: UnassignInvestigatorRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, {
        action: 'unassign',
        assignmentId: request.assignmentId,
        reason: request.reason ?? null,
      }).detail;
    });
  }

  startInvestigation(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, {
        action: 'startInvestigation',
        comment: request.comment,
      }).detail;
    });
  }

  requestInfo(id: number, request: RequestGrievanceInfoRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'requestInfo', message: request.message })
        .detail;
    });
  }

  proposeResolution(
    id: number,
    request: ProposeResolutionRequest,
  ): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      const { after, detail } = this.step(id, actor, rowVersion, {
        action: 'proposeResolution',
        summary: request.summary,
      });
      this.access.notifyReviewers(after, 'a resolution was proposed');
      return detail;
    });
  }

  approveResolution(
    id: number,
    request: ApproveResolutionRequest,
  ): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(REVIEW_APPROVE);
      const rowVersion = requireRowVersion(request?.rowVersion);
      const { detail } = this.step(
        id,
        actor,
        rowVersion,
        {
          action: 'approveResolution',
          visibleToSubmitter: request.visibleToSubmitter,
          comment: request.comment ?? null,
        },
        'review',
      );
      return detail;
    });
  }

  rejectResolution(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(REVIEW_APPROVE);
      const rowVersion = requireRowVersion(request?.rowVersion);
      const { detail } = this.step(
        id,
        actor,
        rowVersion,
        { action: 'rejectResolution', reason: request.reason },
        'review',
      );
      return detail;
    });
  }

  reject(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'reject', reason: request.reason }).detail;
    });
  }

  close(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'close', comment: request.comment }).detail;
    });
  }

  reopen(id: number, request: GrievanceReasonCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'reopen', reason: request.reason }).detail;
    });
  }

  escalate(id: number, request: EscalateGrievanceRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      if (typeof request.isImmediate !== 'boolean') {
        validation('Say whether the escalation is immediate.', 'isImmediate');
      }
      return this.step(id, actor, rowVersion, {
        action: 'escalate',
        reason: request.reason,
        isImmediate: request.isImmediate,
      }).detail;
    });
  }

  archive(id: number, request: GrievanceCommand): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(['grievance-management', 'Delete']);
      const rowVersion = requireRowVersion(request?.rowVersion);
      return this.step(id, actor, rowVersion, { action: 'archive', comment: request.comment })
        .detail;
    });
  }

  // ---- Case work ------------------------------------------------------------------------------

  triage(id: number, request: GrievanceTriageRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      if (request.priority !== null && !GRIEVANCE_PRIORITIES.includes(request.priority)) {
        validation('Choose a priority.', 'priority');
      }
      const target = isoDate(
        request.targetResolutionDate,
        'targetResolutionDate',
        'Target date',
        false,
      );
      if (typeof request.legalHold !== 'boolean') {
        validation('Say whether the case is under legal hold.', 'legalHold');
      }
      if (
        request.subjectEmployeeId !== null &&
        !this.redaction.employeeExists(request.subjectEmployeeId)
      ) {
        validation('Choose an existing employee.', 'subjectEmployeeId');
      }
      if (
        request.involvedDepartmentId !== null &&
        !this.departments.findById(request.involvedDepartmentId)
      ) {
        validation('Choose an existing department.', 'involvedDepartmentId');
      }
      const { record } = this.caseWork(
        id,
        actor,
        rowVersion,
        (rights) => rights.managementEdit,
        (r) => r.archivedAtUtc === null,
        'An archived case is read-only.',
      );
      if (!isGrievanceOpen(record.status)) {
        // A case that is no longer open keeps its triage; only the legal hold may change.
        const changed =
          request.categoryId !== record.categoryId ||
          request.priority !== record.priority ||
          target !== record.targetResolutionDate ||
          request.subjectEmployeeId !== record.subjectEmployeeId ||
          request.involvedDepartmentId !== record.involvedDepartmentId;
        if (changed) {
          throw mockError(
            'Conflict',
            'Only the legal hold can change on a case that is no longer open.',
          );
        }
        const held: GrievanceCaseRecord = {
          ...record,
          legalHold: request.legalHold,
          rowVersion: nextGrievanceRowVersion(record.rowVersion),
        };
        this.store.save(held);
        this.store.audit(id, 'Triage', actor.userId, 'Management', 'legalHold');
        return this.detail(held, actor);
      }
      if (request.categoryId !== record.categoryId) {
        const category = this.categories.findById(request.categoryId);
        if (!category || category.status !== 'Active') {
          validation('Choose an active category.', 'categoryId');
        }
        if (record.isAnonymous && !category.allowsAnonymous) {
          validation(`"${category.name}" does not allow anonymous cases.`, 'categoryId');
        }
      }
      if (request.priority === null && record.status !== 'Submitted') {
        validation('An acknowledged case keeps a priority.', 'priority');
      }
      if (target === null && record.status !== 'Submitted') {
        validation('An acknowledged case keeps a target date.', 'targetResolutionDate');
      }
      if (target !== null && target !== record.targetResolutionDate && target < todayIso()) {
        validation('The target date cannot be in the past.', 'targetResolutionDate');
      }
      if (
        request.subjectEmployeeId !== null &&
        record.assignments.some(
          (a) =>
            a.unassignedAtUtc === null && a.investigatorEmployeeId === request.subjectEmployeeId,
        )
      ) {
        // The API's generic conflict-of-interest refusal on the field (it never says why).
        validation(GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE, 'subjectEmployeeId');
      }
      const saved: GrievanceCaseRecord = {
        ...record,
        categoryId: request.categoryId,
        priority: request.priority,
        targetResolutionDate: target,
        subjectEmployeeId: request.subjectEmployeeId,
        involvedDepartmentId: request.involvedDepartmentId,
        legalHold: request.legalHold,
        rowVersion: nextGrievanceRowVersion(record.rowVersion),
      };
      // Raising the priority to Critical escalates at once (a case is escalated only once).
      const escalate =
        request.priority === 'Critical' && record.priority !== 'Critical' && !record.isEscalated;
      if (escalate) {
        const now = this.now();
        const name = this.actorName(actor);
        saved.isEscalated = true;
        saved.escalations = [
          ...record.escalations,
          {
            id: this.store.nextChildId('escalations'),
            reason: GRIEVANCE_CRITICAL_TRIAGE_REASON,
            isImmediate: true,
            escalatedByUserId: actor.userId,
            escalatedByName: name,
            escalatedAtUtc: now,
            clearedAtUtc: null,
            outcome: null,
          },
        ];
        saved.history = [
          ...record.history,
          {
            id: this.store.nextChildId('history'),
            fromStatus: record.status,
            toStatus: record.status,
            action: 'Escalate',
            actorRole: 'Management',
            actorUserId: actor.userId,
            actorEmployeeId: actor.employeeId,
            actorName: name,
            reason: GRIEVANCE_CRITICAL_TRIAGE_REASON,
            visibleToSubmitter: false,
            occurredAtUtc: now,
          },
        ];
      }
      this.store.save(saved);
      this.store.audit(id, 'Triage', actor.userId, 'Management');
      if (escalate) {
        this.access.notifyReviewers(saved, 'it was escalated as urgent');
      }
      return this.detail(saved, actor);
    });
  }

  sendMessage(id: number, request: GrievanceCaseMessageRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      requiredText(request.body, 'body', 'The message', GRIEVANCE_LIMITS.messageMax);
      const { after, detail } = this.step(id, actor, rowVersion, {
        action: 'postTeamMessage',
        message: request.body,
      });
      this.access.notifySubmitter(after);
      return detail;
    });
  }

  addNote(id: number, request: GrievanceNoteRequest): Observable<GrievanceCaseDetail> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
      const rowVersion = requireRowVersion(request?.rowVersion);
      if (!GRIEVANCE_NOTE_KINDS.includes(request.kind)) {
        validation('Choose a note kind.', 'kind');
      }
      const body = requiredText(request.body, 'body', 'The note', GRIEVANCE_LIMITS.noteMax);
      const interview = request.kind === 'Interview';
      const interviewDate = isoDate(
        request.interviewDate,
        'interviewDate',
        'Interview date',
        false,
      );
      const interviewee = text(
        request.intervieweeText,
        'intervieweeText',
        'Interviewee',
        GRIEVANCE_LIMITS.shortTextMax,
        false,
      );
      if (!interview && (interviewDate !== null || interviewee !== null)) {
        validation('Interview details belong to an Interview note only.', 'interviewDate');
      }
      if (interviewDate !== null && interviewDate > todayIso()) {
        validation('The interview date cannot be in the future.', 'interviewDate');
      }
      const { record, role } = this.caseWork(
        id,
        actor,
        rowVersion,
        (rights) => rights.managementEdit || rights.investigatorEdit,
        (r) => r.archivedAtUtc === null && isGrievanceOpen(r.status),
        'Notes can be added to an open case only.',
      );
      // Notes are append-only rows: the case and its rowVersion do not change.
      const saved: GrievanceCaseRecord = {
        ...record,
        notes: [
          ...record.notes,
          {
            id: this.store.nextChildId('notes'),
            kind: request.kind,
            body,
            interviewDate,
            intervieweeText: interviewee,
            authorUserId: actor.userId,
            authorEmployeeId: actor.employeeId,
            authorName: this.actorName(actor),
            createdAtUtc: this.now(),
          },
        ],
      };
      this.store.save(saved);
      this.store.audit(id, 'AddNote', actor.userId, role);
      return this.detail(saved, actor);
    });
  }

  uploadAttachment(
    id: number,
    rowVersion: string,
    visibility: GrievanceAttachmentVisibility,
    file: File,
  ): Observable<GrievanceAttachmentUploadResult> {
    return from(readFileBytes(file)).pipe(
      switchMap((bytes) =>
        this.respond(() => {
          const actor = this.endpoint(MANAGEMENT_EDIT, INVESTIGATION_EDIT);
          const given = requireRowVersion(rowVersion);
          if (!GRIEVANCE_ATTACHMENT_VISIBILITIES.includes(visibility)) {
            validation('Choose who may see the file.', 'visibility');
          }
          const { record, rights, role } = this.caseWork(
            id,
            actor,
            given,
            (r) => r.managementEdit || r.investigatorEdit,
            FILES_OPEN,
            'Files can be added to an open case only.',
            // Only a Shared file changes the case row (the submitter's unread badge), so only it
            // is refused on a stale version; CaseTeam and Restricted files are append-only rows.
            visibility === 'Shared',
          );
          if (visibility === 'Restricted' && !rights.managementEdit) {
            this.refuse(record, actor, 'Only HR management can add a restricted file.');
          }
          // The case team's own quota (the submitter's files count separately).
          const kept = record.attachments.filter(
            (a) => a.removedAtUtc === null && a.uploadedByRole !== 'Submitter',
          ).length;
          if (kept >= GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE) {
            throw mockError(
              'Conflict',
              `The case team can add at most ${GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE} files to a case.`,
            );
          }
          const inspected = inspectEvidence(file.name, bytes);
          if (!inspected.ok) {
            validation(inspected.message.replace('evidence files', 'files'), 'file');
          }
          const now = this.now();
          const attachment = {
            id: this.store.nextChildId('attachments'),
            communicationId: null,
            noteId: null,
            visibility,
            uploadedByRole: role,
            uploadedByUserId: actor.userId,
            fileName: inspected.fileName,
            contentType: inspected.contentType,
            sizeBytes: bytes.length,
            uploadedAtUtc: now,
            removedAtUtc: null,
          };
          this.store.putBlob(
            attachment.id,
            new Blob([new Uint8Array(bytes)], { type: inspected.contentType }),
          );
          const saved: GrievanceCaseRecord = {
            ...record,
            attachments: [...record.attachments, attachment],
            lastSubmitterUpdateAtUtc:
              visibility === 'Shared' ? now : record.lastSubmitterUpdateAtUtc,
            rowVersion:
              visibility === 'Shared'
                ? nextGrievanceRowVersion(record.rowVersion)
                : record.rowVersion,
          };
          this.store.save(saved);
          this.store.audit(
            id,
            'UploadAttachment',
            actor.userId,
            role,
            `attachment:${attachment.id}`,
          );
          if (visibility === 'Shared') {
            this.access.notifySubmitter(saved);
          }
          return {
            attachment: this.redaction.teamAttachment(attachment),
            caseRowVersion: saved.rowVersion,
          };
        }),
      ),
    );
  }

  downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload> {
    // Not through `respond`: that deep-clones the result, and a Blob is handed on as it is.
    return timer(this.latency).pipe(
      map(() => {
        const actor = this.endpoint(...ANY_VIEW);
        const owner = this.store
          .records()
          .find((c) => c.attachments.some((a) => a.id === attachmentId));
        const notFound = mockError('NotFound', `Attachment #${attachmentId} was not found.`);
        if (!owner) {
          throw notFound;
        }
        const tiers = this.access.tiers(owner, actor);
        const attachment = owner.attachments.find((a) => a.id === attachmentId)!;
        if (
          tiers.length === 0 ||
          attachment.removedAtUtc !== null ||
          !grievanceAttachmentVisibleTo(tiers, attachment.visibility)
        ) {
          throw notFound;
        }
        const blob = this.store.blobOf(attachmentId, attachment.contentType);
        if (!blob) {
          throw mockError('NotFound', 'The file of this attachment is not available in the demo.');
        }
        this.store.audit(
          owner.id,
          'DownloadAttachment',
          actor.userId,
          this.auditRole(tiers),
          `attachment:${attachmentId}`,
        );
        return { blob, fileName: attachment.fileName };
      }),
    );
  }

  // ---- Corrective actions ---------------------------------------------------------------------

  private cleanAction(
    input: GrievanceCorrectiveActionInput | GrievanceCorrectiveActionUpdate,
  ): Pick<
    GrievanceCorrectiveActionRecord,
    'actionType' | 'description' | 'responsibleText' | 'dueDate'
  > {
    return {
      actionType: requiredText(input.actionType, 'actionType', 'Action type', 100),
      description: requiredText(
        input.description,
        'description',
        'Description',
        GRIEVANCE_LIMITS.reasonMax,
      ),
      responsibleText: text(
        input.responsibleText,
        'responsibleText',
        'Responsible',
        GRIEVANCE_LIMITS.shortTextMax,
        false,
      ),
      dueDate: isoDate(input.dueDate, 'dueDate', 'Due date', false),
    };
  }

  addCorrectiveAction(
    id: number,
    input: GrievanceCorrectiveActionInput,
  ): Observable<GrievanceCorrectiveAction> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(input?.rowVersion);
      const fields = this.cleanAction(input);
      const { record } = this.caseWork(
        id,
        actor,
        rowVersion,
        (rights) => rights.managementEdit,
        (r) => r.archivedAtUtc === null,
        'An archived case cannot change.',
      );
      const action: GrievanceCorrectiveActionRecord = {
        id: this.store.nextChildId('correctiveActions'),
        ...fields,
        completedOn: null,
        status: 'Proposed',
        createdByUserId: actor.userId,
        rowVersion: nextGrievanceRowVersion(undefined),
      };
      // An own row with its own rowVersion: the case rowVersion does not change.
      this.store.save({ ...record, correctiveActions: [...record.correctiveActions, action] });
      this.store.audit(id, 'CorrectiveAction', actor.userId, 'Management', `action:${action.id}`);
      return this.toAction(action, id);
    });
  }

  updateCorrectiveAction(
    actionId: number,
    changes: GrievanceCorrectiveActionUpdate,
  ): Observable<GrievanceCorrectiveAction> {
    return this.respond(() => {
      const actor = this.endpoint(MANAGEMENT_EDIT);
      const rowVersion = requireRowVersion(changes?.rowVersion);
      const fields = this.cleanAction(changes);
      if (!GRIEVANCE_CORRECTIVE_ACTION_STATUSES.includes(changes.status)) {
        validation('Choose a status.', 'status');
      }
      const completedOn = isoDate(changes.completedOn, 'completedOn', 'Completed on', false);
      if (changes.status === 'Implemented' && completedOn === null) {
        validation('An implemented action needs the date it was completed.', 'completedOn');
      }
      if (completedOn !== null && completedOn > todayIso()) {
        validation('The completion date cannot be in the future.', 'completedOn');
      }
      const owner = this.store
        .records()
        .find((c) => c.correctiveActions.some((a) => a.id === actionId));
      if (!owner || !this.access.tiers(owner, actor).includes('Management')) {
        throw mockError('NotFound', `Corrective action #${actionId} was not found.`);
      }
      if (!this.access.rights(owner, actor).managementEdit) {
        this.refuse(owner, actor, 'You are not allowed to do this on this case.');
      }
      const current = owner.correctiveActions.find((a) => a.id === actionId)!;
      ensureCurrent(current.rowVersion, rowVersion);
      if (owner.archivedAtUtc !== null) {
        throw mockError('Conflict', 'An archived case cannot change.');
      }
      const updated: GrievanceCorrectiveActionRecord = {
        ...current,
        ...fields,
        completedOn,
        status: changes.status,
        rowVersion: nextGrievanceRowVersion(current.rowVersion),
      };
      this.store.save({
        ...owner,
        correctiveActions: owner.correctiveActions.map((a) => (a.id === actionId ? updated : a)),
      });
      this.store.audit(
        owner.id,
        'CorrectiveAction',
        actor.userId,
        'Management',
        `action:${actionId}`,
      );
      return this.toAction(updated, owner.id);
    });
  }

  private toAction(
    row: GrievanceCorrectiveActionRecord,
    caseId: number,
  ): GrievanceCorrectiveAction {
    return {
      id: row.id,
      caseId,
      actionType: row.actionType,
      description: row.description,
      responsibleText: row.responsibleText,
      dueDate: row.dueDate,
      completedOn: row.completedOn,
      status: row.status,
      rowVersion: row.rowVersion,
    };
  }
}
