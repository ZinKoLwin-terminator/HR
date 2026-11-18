import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap, timer } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { fullName } from '../../core/models/employee.models';
import {
  GRIEVANCE_ANONYMOUS_ACTOR_NAME,
  GRIEVANCE_OPEN_DRAFTS_MAX,
  GRIEVANCE_SUBMITTER_DAILY_UPLOAD_BYTES,
  GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE,
  GRIEVANCE_LIMITS,
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
import { inspectEvidence, readFileBytes } from '../../core/utilities/kpi-evidence';
import {
  GrievanceCaseRecord,
  grievanceCaseNo,
  nextGrievanceRowVersion,
} from '../../mock-data/grievance/grievance-records';
import { todayIso } from '../../shared/utilities/date.util';
import { matchesSearch, paginate, sortItems } from '../../shared/utilities/query.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { mockError, mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import { GrievanceCategoryMockService } from './grievance-category-mock.service';
import { GrievanceMockAccess, GrievanceMockActor } from './grievance-mock-access';
import { GrievanceMockRedaction } from './grievance-mock-redaction';
import {
  ensureCurrent,
  isoDate,
  requireRowVersion,
  requiredText,
  text,
  validation,
} from './grievance-mock-rules';
import { GrievanceMockStore } from './grievance-mock-store';
import { GrievanceDomainCommand, applyGrievanceCommand } from './grievance-case-domain';
import { GrievanceActorRights, isGrievanceOpen } from '../../core/utilities/grievance-workflow';

/**
 * The submitter's own grievances (`/api/grievances/my...`), with the API's rules: drafts, the
 * declaration and submission, messages and the response to an information request, Shared files,
 * and the unread badge.
 *
 * BROWSER SIMULATION, NO REAL SECURITY OR ANONYMITY: everything lives in this browser; "own" comes
 * from the simulated session's employee; the anonymous link is a demo pseudonym anyone could
 * recompute. It only shows how the real API behaves.
 */
@Injectable({ providedIn: 'root' })
export class MyGrievanceMockService implements MyGrievanceDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly access = inject(GrievanceMockAccess);
  private readonly store = inject(GrievanceMockStore);
  private readonly redaction = inject(GrievanceMockRedaction);
  private readonly categories = inject(GrievanceCategoryMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly employees = inject(EmployeeMockService);

  private respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  /** 403 without the permission. */
  private actorWith(action: 'View' | 'Create'): GrievanceMockActor {
    const actor = this.access.actor();
    this.access.require(actor, ['grievance', action]);
    return actor;
  }

  /** The caller's case (drafts included), else 404. */
  private own(id: number, actor: GrievanceMockActor): GrievanceCaseRecord {
    const record = this.store.find(id);
    if (!record || !this.store.isSubmitter(id, actor.employeeId)) {
      throw mockError('NotFound', `Grievance #${id} was not found.`);
    }
    return record;
  }

  private ownCases(actor: GrievanceMockActor): GrievanceCaseRecord[] {
    const ids = this.store.ownCaseIds(actor.employeeId);
    return this.store.records().filter((c) => ids.has(c.id));
  }

  private requireEmployee(actor: GrievanceMockActor): number {
    if (actor.employeeId === null) {
      throw mockError(
        'Forbidden',
        'Your account is not linked to an employee, so it cannot report a grievance.',
      );
    }
    return actor.employeeId;
  }

  /** The submitter as an author: no ids and "Anonymous submitter" on an anonymous case. */
  private author(record: GrievanceCaseRecord, actor: GrievanceMockActor) {
    if (record.isAnonymous) {
      return { userId: null, employeeId: null, name: GRIEVANCE_ANONYMOUS_ACTOR_NAME };
    }
    const employee =
      actor.employeeId === null ? undefined : this.employees.findById(actor.employeeId);
    return {
      userId: actor.userId,
      employeeId: actor.employeeId,
      name: employee ? fullName(employee) : actor.name,
    };
  }

  private now(): string {
    return new Date().toISOString();
  }

  // ---- Draft rules ----------------------------------------------------------------------------

  /**
   * What a draft needs to be saved (400): a category and a non-empty subject, and no text over its
   * maximum. The minimum lengths of subject and description apply at submission.
   */
  private cleanDraft(input: GrievanceDraftInput): GrievanceDraftInput {
    if (!input) {
      validation('The request body is required.', 'body');
    }
    const subject = requiredText(input.subject, 'subject', 'Subject', GRIEVANCE_LIMITS.subjectMax);
    const description =
      text(
        input.description,
        'description',
        'Description',
        GRIEVANCE_LIMITS.descriptionMax,
        false,
      ) ?? '';
    const incidentDate = isoDate(input.incidentDate, 'incidentDate', 'Incident date', false);
    if (incidentDate !== null && incidentDate > todayIso()) {
      validation('The incident date cannot be in the future.', 'incidentDate');
    }
    const location = text(
      input.location,
      'location',
      'Location',
      GRIEVANCE_LIMITS.shortTextMax,
      false,
    );
    const involvedPersonText = text(
      input.involvedPersonText,
      'involvedPersonText',
      'Person involved',
      GRIEVANCE_LIMITS.shortTextMax,
      false,
    );
    if (
      input.involvedDepartmentId !== null &&
      input.involvedDepartmentId !== undefined &&
      !this.departments.findById(input.involvedDepartmentId)
    ) {
      validation('Choose an existing department.', 'involvedDepartmentId');
    }
    if (typeof input.isAnonymous !== 'boolean') {
      validation('Say whether the grievance is anonymous.', 'isAnonymous');
    }
    this.checkCategory(input.categoryId, input.isAnonymous);
    return {
      categoryId: input.categoryId,
      subject,
      description,
      incidentDate,
      location,
      involvedPersonText,
      involvedDepartmentId: input.involvedDepartmentId ?? null,
      isAnonymous: input.isAnonymous,
    };
  }

  /** The full rules of a submission (400): subject and description minimums too. */
  private checkComplete(record: GrievanceCaseRecord): void {
    requiredText(
      record.subject,
      'subject',
      'Subject',
      GRIEVANCE_LIMITS.subjectMax,
      GRIEVANCE_LIMITS.subjectMin,
    );
    requiredText(
      record.description,
      'description',
      'Description',
      GRIEVANCE_LIMITS.descriptionMax,
      GRIEVANCE_LIMITS.descriptionMin,
    );
  }

  private checkCategory(categoryId: number, anonymous: boolean): void {
    const category = this.categories.findById(categoryId);
    if (!category || category.status !== 'Active') {
      validation('Choose an active category.', 'categoryId');
    }
    if (anonymous && !category.allowsAnonymous) {
      validation(`"${category.name}" cannot be reported anonymously.`, 'isAnonymous');
    }
  }

  // ---- Reads ----------------------------------------------------------------------------------

  options(): Observable<GrievanceOptions> {
    return this.respond(() => {
      this.actorWith('View');
      return {
        categories: this.categories
          .records()
          .filter((c) => c.status === 'Active')
          .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
          .map((c) => ({
            id: c.id,
            code: c.code,
            name: c.name,
            description: c.description,
            allowsAnonymous: c.allowsAnonymous,
            defaultTargetDays: c.defaultTargetDays,
          })),
        departments: this.departments
          .records()
          .filter((d) => d.status === 'Active')
          .sort((a, b) => a.name.localeCompare(b.name))
          .map((d) => ({ id: d.id, name: d.name })),
      };
    });
  }

  query(request: PageRequest<MyGrievanceFilter>): Observable<PagedResult<MyGrievance>> {
    return this.respond(() => {
      const actor = this.actorWith('View');
      if (request.sort?.active && !GRIEVANCE_MY_SORT_KEYS.includes(request.sort.active)) {
        validation(`Sorting by "${request.sort.active}" is not supported.`, 'sort');
      }
      const status = request.filters?.status;
      const rows = this.ownCases(actor)
        .map((c) => this.redaction.myCase(c))
        .filter(
          (row) =>
            (!status || row.status === status) &&
            matchesSearch(row, request.search, ['caseNo', 'subject', 'categoryName']),
        );
      const sorted = request.sort?.active
        ? sortItems(rows, request.sort)
        : rows.sort((a, b) =>
            (b.lastUpdateAtUtc ?? b.createdAtUtc).localeCompare(
              a.lastUpdateAtUtc ?? a.createdAtUtc,
            ),
          );
      return paginate(sorted, request.pageIndex, request.pageSize);
    });
  }

  getById(id: number): Observable<MyGrievanceDetail> {
    return this.respond(() => {
      const actor = this.actorWith('View');
      // The submitter's own views are not audited (only the case team's are).
      return this.redaction.myDetail(this.own(id, actor));
    });
  }

  dashboard(): Observable<MyGrievanceDashboard> {
    return this.respond(() => {
      const actor = this.actorWith('View');
      const mine = this.ownCases(actor).map((c) => this.redaction.myCase(c));
      const count = (predicate: (row: MyGrievance) => boolean): number =>
        mine.filter(predicate).length;
      return {
        drafts: count((r) => r.status === 'Draft'),
        open: count((r) => isGrievanceOpen(r.status)),
        awaitingMyResponse: count((r) => r.status === 'AwaitingEmployeeResponse'),
        resolved: count((r) => r.status === 'Resolved'),
        closed: count((r) => r.status === 'Closed'),
        rejected: count((r) => r.status === 'Rejected'),
        unreadUpdates: count((r) => r.hasUnreadUpdate),
        recent: [...mine]
          .sort((a, b) =>
            (b.lastUpdateAtUtc ?? b.createdAtUtc).localeCompare(
              a.lastUpdateAtUtc ?? a.createdAtUtc,
            ),
          )
          .slice(0, 5),
      };
    });
  }

  downloadAttachment(attachmentId: number): Observable<GrievanceAttachmentDownload> {
    // Not through `respond`: that deep-clones the result, and a Blob is handed on as it is.
    return timer(this.latency).pipe(
      map(() => {
        const actor = this.actorWith('View');
        const record = this.ownCases(actor).find((c) =>
          c.attachments.some((a) => a.id === attachmentId),
        );
        const attachment = record?.attachments.find((a) => a.id === attachmentId);
        if (
          !record ||
          !attachment ||
          attachment.removedAtUtc !== null ||
          attachment.visibility !== 'Shared'
        ) {
          throw mockError('NotFound', `Attachment #${attachmentId} was not found.`);
        }
        const blob = this.store.blobOf(attachmentId, attachment.contentType);
        if (!blob) {
          throw mockError('NotFound', 'The file of this attachment is not available in the demo.');
        }
        // The submitter's own downloads are not audited (only the case team's are).
        return { blob, fileName: attachment.fileName };
      }),
    );
  }

  // ---- Drafts ---------------------------------------------------------------------------------

  createDraft(input: GrievanceDraftInput): Observable<MyGrievanceDetail> {
    return this.respond(() => {
      const actor = this.actorWith('Create');
      const draft = this.cleanDraft(input);
      const employeeId = this.requireEmployee(actor);
      const drafts = this.ownCases(actor).filter((c) => c.status === 'Draft').length;
      if (drafts >= GRIEVANCE_OPEN_DRAFTS_MAX) {
        throw mockError(
          'Validation',
          'You already have ' +
            GRIEVANCE_OPEN_DRAFTS_MAX +
            ' drafts. Submit or delete one before starting another.',
        );
      }
      const id = this.store.nextCaseId();
      const now = this.now();
      const record: GrievanceCaseRecord = {
        id,
        caseNo: null,
        ...draft,
        subjectEmployeeId: null,
        status: 'Draft',
        priority: null,
        targetResolutionDate: null,
        returnStatus: null,
        isEscalated: false,
        companyId: null,
        branchId: null,
        departmentId: null,
        positionId: null,
        createdAtUtc: now,
        declarationAcceptedAtUtc: null,
        submittedAtUtc: null,
        acknowledgedAtUtc: null,
        resolvedAtUtc: null,
        closedAtUtc: null,
        resolutionSummary: null,
        resolutionVisibleToSubmitter: false,
        rejectionReason: null,
        archivedAtUtc: null,
        retentionUntil: null,
        legalHold: false,
        lastSubmitterUpdateAtUtc: null,
        rowVersion: nextGrievanceRowVersion(undefined),
        assignments: [],
        notes: [],
        communications: [],
        attachments: [],
        correctiveActions: [],
        proposals: [],
        escalations: [],
        history: [],
      };
      this.store.save(record);
      this.store.linkSubmitter(id, employeeId, draft.isAnonymous);
      return this.redaction.myDetail(record);
    });
  }

  updateDraft(id: number, changes: GrievanceDraftUpdate): Observable<MyGrievanceDetail> {
    return this.respond(() => {
      const actor = this.actorWith('Create');
      const rowVersion = requireRowVersion(changes?.rowVersion);
      const draft = this.cleanDraft(changes);
      this.requireEmployee(actor);
      const record = this.own(id, actor);
      ensureCurrent(record.rowVersion, rowVersion);
      if (record.status !== 'Draft') {
        throw mockError('Conflict', 'Only a draft can be edited.');
      }
      // How the employee reports is fixed when the draft is created.
      if (draft.isAnonymous !== record.isAnonymous) {
        validation('Start a new draft to change how you report.', 'isAnonymous');
      }
      const saved: GrievanceCaseRecord = {
        ...record,
        ...draft,
        rowVersion: nextGrievanceRowVersion(record.rowVersion),
      };
      this.store.save(saved);
      return this.redaction.myDetail(saved);
    });
  }

  deleteDraft(id: number, rowVersion: string): Observable<void> {
    return this.respond(() => {
      const actor = this.actorWith('Create');
      const given = requireRowVersion(rowVersion);
      const record = this.own(id, actor);
      ensureCurrent(record.rowVersion, given);
      if (record.status !== 'Draft') {
        throw mockError(
          'Conflict',
          'Only a draft can be deleted; a submitted grievance stays on record.',
        );
      }
      this.store.remove(id);
    });
  }

  submit(id: number, request: SubmitGrievanceRequest): Observable<MyGrievanceDetail> {
    return this.respond(() => {
      const actor = this.actorWith('Create');
      const rowVersion = requireRowVersion(request?.rowVersion);
      const employeeId = this.requireEmployee(actor);
      const record = this.own(id, actor);
      const rights = this.access.rights(record, actor);
      ensureCurrent(record.rowVersion, rowVersion);
      if (record.status === 'Draft') {
        // The minimums a draft may still miss.
        this.checkComplete(record);
      }
      const category = this.categories.findById(record.categoryId);
      const employee = this.employees.findById(employeeId);
      const saved = this.step(record, actor, rights, {
        action: 'submit',
        declarationAccepted: request.declarationAccepted,
        categoryActive: category?.status === 'Active',
        categoryAllowsAnonymous: category?.allowsAnonymous ?? false,
        caseNo: grievanceCaseNo(Number(todayIso().slice(0, 4)), this.store.nextCaseSequence()),
        // Copied for a named case only; never changes after submission.
        placement: employee
          ? {
              companyId: employee.companyId,
              branchId: employee.branchId,
              departmentId: employee.departmentId,
              positionId: employee.positionId,
            }
          : null,
      });
      this.store.markSeen(id, saved.lastSubmitterUpdateAtUtc ?? this.now());
      // Audited without a user id; the submitter is not left out of the notification.
      this.store.audit(id, 'StatusChange', null, 'Submitter', 'Draft>Submitted');
      this.access.notifyManagement(saved, 'a new grievance was submitted');
      return this.redaction.myDetail(saved);
    });
  }

  /**
   * One submitter step through the shared domain (`grievance-case-domain.ts`); the anonymous
   * submitter is recorded without ids. Saves and returns the changed record.
   */
  private step(
    record: GrievanceCaseRecord,
    actor: GrievanceMockActor,
    rights: GrievanceActorRights,
    command: GrievanceDomainCommand,
    keepRowVersion = false,
  ): GrievanceCaseRecord {
    const counters = new Map<string, number>();
    const saved = applyGrievanceCommand(record, command, {
      now: this.now(),
      today: todayIso(),
      actor: { ...this.author(record, actor), role: 'Submitter' },
      rights,
      nextId: (kind) => {
        const next = counters.get(kind) ?? this.store.nextChildId(kind);
        counters.set(kind, next + 1);
        return next;
      },
    });
    if (!keepRowVersion) {
      saved.rowVersion = nextGrievanceRowVersion(record.rowVersion);
    }
    this.store.save(saved);
    return saved;
  }

  // ---- Conversation and files -----------------------------------------------------------------

  sendMessage(id: number, request: MyGrievanceMessageRequest): Observable<MyGrievanceDetail> {
    return this.respond(() => {
      const actor = this.actorWith('Create');
      requiredText(request?.body, 'body', 'The message', GRIEVANCE_LIMITS.messageMax);
      const record = this.own(id, actor);
      const rights = this.access.rights(record, actor);
      if (record.status !== 'AwaitingEmployeeResponse') {
        // A plain message is an append-only row: no rowVersion check, and it does not move on.
        const saved = this.step(
          record,
          actor,
          rights,
          {
            action: 'postSubmitterMessage',
            message: request.body,
            inReplyToId: request.inReplyToId ?? null,
          },
          true,
        );
        // A plain message notifies nobody (the API's rule): the case team sees it on the case.
        this.store.audit(id, 'SendMessage', null, 'Submitter');
        return this.redaction.myDetail(saved);
      }
      // While the case awaits the submitter, the message is the response.
      const rowVersion = requireRowVersion(request.rowVersion);
      ensureCurrent(record.rowVersion, rowVersion);
      const saved = this.step(record, actor, rights, {
        action: 'respond',
        message: request.body,
        inReplyToId: request.inReplyToId ?? null,
      });
      this.store.markSeen(id, saved.lastSubmitterUpdateAtUtc ?? this.now());
      this.store.audit(
        id,
        'StatusChange',
        null,
        'Submitter',
        `AwaitingEmployeeResponse>${saved.status}`,
      );
      this.access.notifyManagement(saved, 'the employee responded');
      this.access.notifyActiveInvestigators(saved, 'the employee responded');
      return this.redaction.myDetail(saved);
    });
  }

  uploadAttachment(
    id: number,
    rowVersion: string,
    file: File,
  ): Observable<MyGrievanceAttachmentUploadResult> {
    return from(readFileBytes(file)).pipe(
      switchMap((bytes) =>
        this.respond(() => {
          const actor = this.actorWith('Create');
          // Required and well formed, but not compared: a submitter upload is an append-only row
          // and never changes the case rowVersion (so it never collides with HR work).
          requireRowVersion(rowVersion);
          const record = this.own(id, actor);
          if (
            record.archivedAtUtc !== null ||
            (record.status !== 'Draft' && !isGrievanceOpen(record.status))
          ) {
            throw mockError('Conflict', 'Files can be added to a draft or an open grievance only.');
          }
          // The submitter's own quota per case (the case team's files count separately).
          const kept = record.attachments.filter(
            (a) => a.removedAtUtc === null && a.uploadedByRole === 'Submitter',
          ).length;
          if (kept >= GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE) {
            throw mockError(
              'Conflict',
              `You can add at most ${GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE} files to a grievance.`,
            );
          }
          // The daily quota of every upload by this employee (all their grievances).
          const employeeId = this.requireEmployee(actor);
          const today = todayIso();
          const used = this.store.uploadedBytesOn(employeeId, today);
          if (used + bytes.length > GRIEVANCE_SUBMITTER_DAILY_UPLOAD_BYTES) {
            validation(
              'You have reached today’s upload limit of 100 MB. Try again tomorrow.',
              'file',
            );
          }
          const inspected = inspectEvidence(file.name, bytes);
          if (!inspected.ok) {
            validation(inspected.message.replace('evidence files', 'files'), 'file');
          }
          const author = this.author(record, actor);
          const attachment = {
            id: this.store.nextChildId('attachments'),
            communicationId: null,
            noteId: null,
            visibility: 'Shared' as const,
            uploadedByRole: 'Submitter' as const,
            uploadedByUserId: author.userId,
            fileName: inspected.fileName,
            contentType: inspected.contentType,
            sizeBytes: bytes.length,
            uploadedAtUtc: this.now(),
            removedAtUtc: null,
          };
          this.store.putBlob(
            attachment.id,
            new Blob([new Uint8Array(bytes)], { type: inspected.contentType }),
          );
          this.store.recordUpload(employeeId, today, bytes.length);
          const saved: GrievanceCaseRecord = {
            ...record,
            attachments: [...record.attachments, attachment],
          };
          this.store.save(saved);
          // Drafts are neither audited nor notified.
          if (record.status !== 'Draft') {
            this.store.audit(
              id,
              'UploadAttachment',
              null,
              'Submitter',
              `attachment:${attachment.id}`,
            );
          }
          return {
            attachment: this.redaction.myAttachmentOf(attachment),
            caseRowVersion: saved.rowVersion,
          };
        }),
      ),
    );
  }

  markSeen(id: number): Observable<void> {
    return this.respond(() => {
      const actor = this.actorWith('View');
      this.own(id, actor);
      // Kept on the submitter link, not on the case: the case rowVersion does not change.
      this.store.markSeen(id, this.now());
    });
  }
}
