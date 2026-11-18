import { TestBed } from '@angular/core/testing';
import { PermissionAction } from '../../core/models/settings.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import {
  GRIEVANCE_CASE_METHODS,
  GrievanceCaseDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { GrievanceCaseMockService } from './grievance-case-mock.service';
import { GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE } from './grievance-case-domain';
import {
  DANIEL,
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
  PAGE,
  configureGrievanceTest,
  failure,
  pdfFile,
  wait,
} from './grievance-test-support';

/** Role ids of the seed: 2 HR Officer, 3 Department Manager. */
const HR_OFFICER_ROLE = 2;
const MANAGER_ROLE = 3;

describe('grievance mock: case team', () => {
  let cases: GrievanceCaseDataService;
  let session: ReturnType<typeof configureGrievanceTest>;

  beforeEach(() => {
    session = configureGrievanceTest(HR_ADMIN);
    cases = TestBed.inject(GrievanceCaseDataService);
  });

  /** Adds grants to a role through Settings, as HR Administrator would (D4). */
  async function grant(roleId: number, extra: Record<string, PermissionAction[]>): Promise<void> {
    const permissions = TestBed.inject(RolePermissionDataService);
    const current = await wait(permissions.getForRole(roleId));
    await wait(permissions.save(roleId, { ...current.grants, ...extra }));
  }

  const version = async (id: number): Promise<string> => (await wait(cases.getById(id))).rowVersion;

  /** Submits a grievance as the signed-in administrator (EMP002) and returns its id. */
  async function submitAsAdmin(my: MyGrievanceDataService, isAnonymous: boolean): Promise<number> {
    const draft = await wait(
      my.createDraft({
        categoryId: 1,
        subject: 'Sample case of the administrator',
        description: 'Sample text long enough to pass the description rule.',
        incidentDate: null,
        location: null,
        involvedPersonText: null,
        involvedDepartmentId: null,
        isAnonymous,
      }),
    );
    await wait(my.submit(draft.id, { rowVersion: draft.rowVersion, declarationAccepted: true }));
    return draft.id;
  }

  it('binds every contract method to the mock', () => {
    expect(new Set(GRIEVANCE_CASE_METHODS).size).toBe(GRIEVANCE_CASE_METHODS.length);
    for (const method of GRIEVANCE_CASE_METHODS) {
      expect(typeof GrievanceCaseMockService.prototype[method], method).toBe('function');
    }
  });

  describe('scoping', () => {
    it('refuses an employee at the endpoint (403 before scope)', async () => {
      session.as(DANIEL);
      expect((await failure(cases.query(PAGE))).code).toBe('Forbidden');
      expect((await failure(cases.getById(4))).code).toBe('Forbidden');
    });

    it('lists every non-draft, non-archived case for management', async () => {
      const page = await wait(cases.query(PAGE));
      const ids = page.items.map((c) => c.id);
      expect(ids).not.toContain(1);
      expect(ids).not.toContain(2);
      expect(ids).not.toContain(14);
      expect(page.total).toBe(15);
      const archived = await wait(cases.query({ ...PAGE, filters: { archived: true } }));
      expect(archived.items.map((c) => c.id)).toEqual([14]);
    });

    it('gives the investigator the assigned cases only, never as the subject', async () => {
      session.as(HR_OFFICER);
      const mine = await wait(cases.investigations(PAGE));
      // Every case EMP009 leads, closed ones included, except the archived #14 and the reopened
      // #16 (reopening ended its assignments).
      expect(mine.items.map((c) => c.id).sort((a, b) => a - b)).toEqual([
        7, 8, 9, 10, 11, 12, 13, 17, 18,
      ]);
      expect((await failure(cases.query(PAGE))).code).toBe('Forbidden');
      expect((await failure(cases.getById(3))).code).toBe('NotFound');
      // #5 names this officer as the subject employee.
      expect((await failure(cases.getById(5))).code).toBe('NotFound');
    });

    it('rejects a sort field the API does not accept', async () => {
      const error = await failure(
        cases.query({ ...PAGE, sort: { active: 'subjectEmployeeId', direction: 'asc' } }),
      );
      expect(error.fieldErrors?.['sort']).toBeDefined();
    });

    it('never filters an anonymous case by department', async () => {
      const page = await wait(cases.query({ ...PAGE, filters: { departmentId: 4 } }));
      expect(page.items.every((c) => !c.isAnonymous)).toBe(true);
      expect(page.items.map((c) => c.id).sort((a, b) => a - b)).toEqual([3, 5, 8, 10, 16]);
    });
  });

  describe('redaction by tier', () => {
    it('hides the anonymous submitter from management', async () => {
      const detail = await wait(cases.getById(9));
      expect(detail.submitter).toBeNull();
      expect(detail.submitterLabel).toBe('Anonymous');
      expect(detail.departmentName).toBeNull();
      expect(JSON.stringify(detail)).not.toContain('Thant Zin');
      const named = await wait(cases.getById(7));
      expect(named.submitter?.employeeId).toBe(28);
    });

    it('trims an investigator’s view (no Restricted file, no corrective actions)', async () => {
      session.as(HR_OFFICER);
      const detail = await wait(cases.getById(9));
      expect(detail.viewerRoles).toEqual(['Investigator']);
      expect(detail.isLeadInvestigator).toBe(true);
      expect(detail.attachments.map((a) => a.visibility)).toEqual(['CaseTeam']);
      const withActions = await wait(cases.getById(11));
      expect(withActions.correctiveActions).toEqual([]);
    });

    it('shows a reviewer findings and recommendations only, no messages', async () => {
      await grant(MANAGER_ROLE, { 'grievance-review': ['View', 'Approve'] });
      session.as(MANAGER);
      const detail = await wait(cases.getById(9));
      expect(detail.viewerRoles).toEqual(['Reviewer']);
      expect(detail.notes.map((n) => n.kind)).toEqual(['Finding']);
      expect(detail.messages).toEqual([]);
      expect(detail.attachments).toEqual([]);
      const queue = await wait(cases.pendingReviews(PAGE));
      // Escalated open cases and the pending proposal, oldest submission first.
      expect(queue.items.map((c) => c.id)).toEqual([8, 11, 9]);
    });

    it('redacts the anonymous submitter in the history', async () => {
      const history = await wait(cases.history(9));
      expect(history[0]).toMatchObject({ actorName: 'Anonymous submitter', actorUserId: null });
    });
  });

  describe('state machine', () => {
    it('checks the rowVersion: missing 400, stale 409', async () => {
      expect((await failure(cases.acknowledge(3, { rowVersion: '', priority: 'Low' }))).code).toBe(
        'Validation',
      );
      expect(
        (await failure(cases.acknowledge(3, { rowVersion: '999', priority: 'Low' }))).code,
      ).toBe('Conflict');
    });

    it('acknowledges a Critical case with an immediate escalation and alerts reviewers', async () => {
      const detail = await wait(
        cases.acknowledge(3, { rowVersion: await version(3), priority: 'Critical' }),
      );
      expect(detail.status).toBe('Acknowledged');
      expect(detail.isEscalated).toBe(true);
      expect(detail.escalations[0]).toMatchObject({ isImmediate: true });
      expect(detail.targetResolutionDate).not.toBeNull();
      const history = await wait(cases.history(3));
      expect(history.slice(-2).map((h) => h.action)).toEqual(['Acknowledge', 'Escalate']);
      expect(new Set(history.map((h) => h.id)).size).toBe(history.length);
      // Acknowledging again is a status conflict.
      const again = await failure(
        cases.acknowledge(3, { rowVersion: detail.rowVersion, priority: 'Low' }),
      );
      expect(again.code).toBe('Conflict');
    });

    it('refuses a target date in the past', async () => {
      const error = await failure(
        cases.acknowledge(3, {
          rowVersion: await version(3),
          priority: 'Low',
          targetResolutionDate: '2000-01-01',
        }),
      );
      expect(error.fieldErrors?.['targetResolutionDate']).toBeDefined();
    });

    it('keeps a case out of every tier of its submitter', async () => {
      const my = TestBed.inject(MyGrievanceDataService);
      const id = await submitAsAdmin(my, false);
      expect((await failure(cases.getById(id))).code).toBe('NotFound');
      expect((await wait(cases.query(PAGE))).items.some((c) => c.id === id)).toBe(false);
    });

    it('refuses the subject and a named submitter with one generic message, never an anonymous one', async () => {
      // The administrator submits a named and an anonymous case; HR Officer handles them.
      const my = TestBed.inject(MyGrievanceDataService);
      const named = await submitAsAdmin(my, false);
      const anonymous = await submitAsAdmin(my, true);
      await grant(HR_OFFICER_ROLE, { 'grievance-management': ['View', 'Edit'] });
      session.as(HR_OFFICER);
      for (const id of [named, anonymous]) {
        await wait(cases.acknowledge(id, { rowVersion: await version(id), priority: 'Low' }));
      }
      const asSubmitter = await failure(
        cases.assign(named, {
          rowVersion: await version(named),
          investigatorEmployeeId: 2,
          role: 'Lead',
        }),
      );
      expect(asSubmitter).toMatchObject({
        code: 'Validation',
        message: GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE,
      });
      expect(Object.keys(asSubmitter.fieldErrors ?? {})).toEqual(['investigatorEmployeeId']);
      // A refusal on an anonymous case would reveal its submitter: it is not checked.
      const assigned = await wait(
        cases.assign(anonymous, {
          rowVersion: await version(anonymous),
          investigatorEmployeeId: 2,
          role: 'Lead',
        }),
      );
      expect(assigned.status).toBe('Assigned');
      session.as(HR_ADMIN);
      // #5 names EMP009 as its subject.
      const asSubject = await failure(
        cases.assign(5, { rowVersion: await version(5), investigatorEmployeeId: 9, role: 'Lead' }),
      );
      expect(asSubject).toMatchObject({
        code: 'Validation',
        message: GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE,
      });
      const audit = await wait(cases.audit(5, { ...PAGE, filters: { action: 'AccessRefused' } }));
      expect(audit.items[0]).toMatchObject({ detail: 'conflict-of-interest' });
    });

    it('escalates a case only once', async () => {
      // #8 is already escalated.
      const error = await failure(
        cases.escalate(8, { rowVersion: await version(8), reason: 'Again.', isImmediate: false }),
      );
      expect(error.code).toBe('Conflict');
    });

    it('lets the case team write on a rejected case, not on a closed one', async () => {
      const rejected = await wait(
        cases.sendMessage(15, { rowVersion: await version(15), body: 'Hi.' }),
      );
      expect(rejected.messages.at(-1)).toMatchObject({ direction: 'ToSubmitter', kind: 'Message' });
      const closed = await failure(
        cases.sendMessage(13, { rowVersion: await version(13), body: 'Hi.' }),
      );
      expect(closed.code).toBe('Conflict');
    });

    it('escalates at once when triage raises the priority to Critical', async () => {
      const detail = await wait(cases.getById(5));
      const saved = await wait(
        cases.triage(5, {
          rowVersion: detail.rowVersion,
          categoryId: detail.categoryId,
          priority: 'Critical',
          targetResolutionDate: detail.targetResolutionDate,
          subjectEmployeeId: detail.subjectEmployeeId,
          involvedDepartmentId: detail.involvedDepartmentId,
          legalHold: false,
        }),
      );
      expect(saved.isEscalated).toBe(true);
      expect(saved.escalations[0]).toMatchObject({ isImmediate: true });
    });

    it('sets the retention date when a case is closed', async () => {
      const closed = await wait(cases.close(12, { rowVersion: await version(12) }));
      expect(closed.retentionUntil?.slice(0, 4)).toBe(String(new Date().getFullYear() + 7));
    });

    it('assigns a lead, and replaces one only with a reason', async () => {
      const assigned = await wait(
        cases.assign(6, { rowVersion: await version(6), investigatorEmployeeId: 9, role: 'Lead' }),
      );
      expect(assigned.status).toBe('Assigned');
      expect(assigned.leadInvestigatorEmployeeId).toBe(9);
      const noReason = await failure(
        cases.assign(8, { rowVersion: await version(8), investigatorEmployeeId: 2, role: 'Lead' }),
      );
      expect(noReason.fieldErrors?.['reason']).toBeDefined();
      const replaced = await wait(
        cases.assign(8, {
          rowVersion: await version(8),
          investigatorEmployeeId: 2,
          role: 'Lead',
          reason: 'Workload',
        }),
      );
      expect(replaced.status).toBe('UnderInvestigation');
      expect(replaced.leadInvestigatorEmployeeId).toBe(2);
      expect(
        replaced.assignments.find((a) => a.investigatorEmployeeId === 9)?.unassignedAtUtc,
      ).not.toBeNull();
    });

    it('never removes the active lead', async () => {
      const detail = await wait(cases.getById(8));
      const lead = detail.assignments.find((a) => a.role === 'Lead' && a.unassignedAtUtc === null)!;
      const error = await failure(
        cases.unassign(8, { rowVersion: detail.rowVersion, assignmentId: lead.id, reason: 'x' }),
      );
      expect(error.code).toBe('Conflict');
    });

    it('lets the lead investigate, request information and propose', async () => {
      session.as(HR_OFFICER);
      const started = await wait(cases.startInvestigation(7, { rowVersion: await version(7) }));
      expect(started.status).toBe('UnderInvestigation');
      const tooLong = await failure(
        cases.requestInfo(7, { rowVersion: started.rowVersion, message: 'x'.repeat(1001) }),
      );
      expect(tooLong.code).toBe('Validation');
      const waiting = await wait(
        cases.requestInfo(7, { rowVersion: started.rowVersion, message: 'Which day was it?' }),
      );
      expect(waiting).toMatchObject({
        status: 'AwaitingEmployeeResponse',
        returnStatus: 'UnderInvestigation',
      });
      const proposed = await wait(
        cases.proposeResolution(8, { rowVersion: await version(8), summary: 'Sample summary.' }),
      );
      expect(proposed.status).toBe('ResolutionProposed');
    });

    it('does not let management propose while the lead investigates', async () => {
      const error = await failure(
        cases.proposeResolution(8, { rowVersion: await version(8), summary: 'Sample summary.' }),
      );
      expect(error.code).toBe('Forbidden');
    });

    it('approves a proposal, but never by its proposer', async () => {
      session.as(HR_OFFICER);
      expect(
        (
          await failure(
            cases.approveResolution(11, {
              rowVersion: await version(11),
              visibleToSubmitter: true,
            }),
          )
        ).code,
      ).toBe('Forbidden');
      session.as(HR_ADMIN);
      const resolved = await wait(
        cases.approveResolution(11, { rowVersion: await version(11), visibleToSubmitter: true }),
      );
      expect(resolved).toMatchObject({ status: 'Resolved', resolutionVisibleToSubmitter: true });
      expect(resolved.resolutionSummary).toContain('Sample resolution');
      // Management proposes from Under Review, and may then not decide on it.
      const proposed = await wait(
        cases.proposeResolution(6, { rowVersion: await version(6), summary: 'Sample summary.' }),
      );
      const own = await failure(
        cases.approveResolution(6, { rowVersion: proposed.rowVersion, visibleToSubmitter: false }),
      );
      expect(own.code).toBe('Forbidden');
    });

    it('sends a rejected proposal back to the investigation', async () => {
      const back = await wait(
        cases.rejectResolution(11, {
          rowVersion: await version(11),
          reason: 'Add the order date.',
        }),
      );
      expect(back.status).toBe('UnderInvestigation');
      expect(back.proposal?.decision).toBe('Rejected');
    });

    it('rejects with a reason the submitter can read', async () => {
      expect(
        (await failure(cases.reject(3, { rowVersion: await version(3), reason: ' ' }))).code,
      ).toBe('Validation');
      await wait(cases.reject(3, { rowVersion: await version(3), reason: 'Sample reason.' }));
      const history = await wait(cases.history(3));
      expect(history.at(-1)).toMatchObject({ action: 'Reject', visibleToSubmitter: true });
    });

    it('reopens a closed case only with management and review rights', async () => {
      await grant(HR_OFFICER_ROLE, { 'grievance-management': ['View', 'Edit'] });
      session.as(HR_OFFICER);
      const closed = await wait(cases.getById(13));
      expect(
        (await failure(cases.reopen(13, { rowVersion: closed.rowVersion, reason: 'Again.' }))).code,
      ).toBe('Forbidden');
      session.as(HR_ADMIN);
      const reopened = await wait(
        cases.reopen(13, { rowVersion: closed.rowVersion, reason: 'Again.' }),
      );
      expect(reopened.status).toBe('Reopened');
    });

    it('reopening ends every assignment until HR assigns a lead again (the same person allowed)', async () => {
      // #12 (Resolved) was investigated by Ye Yint Naing (EMP009) as Lead.
      const reopened = await wait(
        cases.reopen(12, { rowVersion: await version(12), reason: 'New facts were reported.' }),
      );
      expect(reopened.status).toBe('Reopened');
      expect(reopened.assignments.filter((a) => a.unassignedAtUtc === null)).toEqual([]);
      const history = await wait(cases.history(12));
      expect(history.at(-1)).toMatchObject({
        action: 'Unassign',
        fromStatus: 'Reopened',
        toStatus: 'Reopened',
        reason: 'Case reopened',
      });
      session.as(HR_OFFICER);
      expect((await failure(cases.getById(12))).code).toBe('NotFound');
      expect((await wait(cases.investigations(PAGE))).items.map((c) => c.id)).not.toContain(12);
      session.as(HR_ADMIN);
      const again = await wait(
        cases.assign(12, {
          rowVersion: reopened.rowVersion,
          investigatorEmployeeId: 9,
          role: 'Lead',
        }),
      );
      expect(again.status).toBe('Assigned');
      session.as(HR_OFFICER);
      expect((await wait(cases.getById(12))).status).toBe('Assigned');
    });

    it('a stale rowVersion refuses only a Shared upload; team files keep the case version', async () => {
      const old = await version(8);
      await wait(cases.addNote(8, { rowVersion: old, kind: 'Finding', body: 'Sample finding.' }));
      await wait(cases.sendMessage(8, { rowVersion: old, body: 'Sample message.' }));
      const current = await version(8);
      expect(current).not.toBe(old);
      const team = await wait(cases.uploadAttachment(8, old, 'CaseTeam', pdfFile('team.pdf')));
      expect(team.caseRowVersion).toBe(current);
      expect((await failure(cases.uploadAttachment(8, old, 'Shared', pdfFile()))).code).toBe(
        'Conflict',
      );
      const shared = await wait(cases.uploadAttachment(8, current, 'Shared', pdfFile()));
      expect(shared.caseRowVersion).not.toBe(current);
    });

    it('the submitter’s response notifies management and the active investigators', async () => {
      // #10 waits for Daniel Carter; Ye Yint Naing (user 6) is its Lead.
      const my = TestBed.inject(MyGrievanceDataService);
      session.as(DANIEL);
      const ten = await wait(my.getById(10));
      await wait(my.sendMessage(10, { rowVersion: ten.rowVersion, body: 'March and April.' }));
      session.as(HR_OFFICER);
      const notes = await wait(TestBed.inject(NotificationDataService).getNotifications());
      expect(
        notes.filter((n) => n.link === '/grievances/investigations/10').map((n) => n.message),
      ).toEqual([`Grievance ${ten.caseNo}: the employee responded.`]);
    });

    it('closes, archives and then refuses further changes', async () => {
      const closed = await wait(cases.close(12, { rowVersion: await version(12) }));
      expect(closed.status).toBe('Closed');
      const archived = await wait(cases.archive(12, { rowVersion: closed.rowVersion }));
      expect(archived.archivedAtUtc).not.toBeNull();
      const error = await failure(
        cases.reopen(12, { rowVersion: archived.rowVersion, reason: 'Again.' }),
      );
      expect(error.code).toBe('Conflict');
    });

    it('escalates an open case into the review queue', async () => {
      session.as(HR_OFFICER);
      const escalated = await wait(
        cases.escalate(7, { rowVersion: await version(7), reason: 'Urgent.', isImmediate: false }),
      );
      expect(escalated.isEscalated).toBe(true);
      session.as(HR_ADMIN);
      const queue = await wait(cases.pendingReviews(PAGE));
      expect(queue.items.map((c) => c.id)).toContain(7);
    });
  });

  describe('case work', () => {
    it('triage: keeps priority after acknowledgement, refuses an investigator as subject', async () => {
      const detail = await wait(cases.getById(8));
      const base = {
        rowVersion: detail.rowVersion,
        categoryId: detail.categoryId,
        priority: detail.priority,
        targetResolutionDate: detail.targetResolutionDate,
        subjectEmployeeId: null,
        involvedDepartmentId: null,
        legalHold: false,
      };
      expect((await failure(cases.triage(8, { ...base, priority: null }))).code).toBe('Validation');
      expect((await failure(cases.triage(8, { ...base, subjectEmployeeId: 9 }))).code).toBe(
        'Validation',
      );
      const saved = await wait(cases.triage(8, { ...base, legalHold: true }));
      expect(saved.legalHold).toBe(true);
    });

    it('triage: refuses the lead investigator as the subject employee', async () => {
      const seven = await wait(cases.getById(7));
      // EMP009 leads #7: the API's generic conflict-of-interest Validation on the field.
      const error = await failure(
        cases.triage(7, {
          rowVersion: seven.rowVersion,
          categoryId: seven.categoryId,
          priority: seven.priority,
          targetResolutionDate: seven.targetResolutionDate,
          subjectEmployeeId: 9,
          involvedDepartmentId: null,
          legalHold: false,
        }),
      );
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['subjectEmployeeId']).toBe(GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE);
    });

    it('notes are append-only and do not change the rowVersion', async () => {
      session.as(HR_OFFICER);
      const before = await wait(cases.getById(8));
      const wrong = await failure(
        cases.addNote(8, {
          rowVersion: before.rowVersion,
          kind: 'Note',
          body: 'x',
          interviewDate: '2026-01-01',
        }),
      );
      expect(wrong.code).toBe('Validation');
      const after = await wait(
        cases.addNote(8, {
          rowVersion: before.rowVersion,
          kind: 'Finding',
          body: 'Sample finding.',
        }),
      );
      expect(after.rowVersion).toBe(before.rowVersion);
      expect(after.notes.at(-1)?.kind).toBe('Finding');
    });

    it('files: investigators cannot add Restricted ones; Shared ones reach the submitter', async () => {
      session.as(HR_OFFICER);
      const rowVersion = await version(8);
      expect(
        (await failure(cases.uploadAttachment(8, rowVersion, 'Restricted', pdfFile()))).code,
      ).toBe('Forbidden');
      const shared = await wait(
        cases.uploadAttachment(8, rowVersion, 'Shared', pdfFile('plan.pdf')),
      );
      expect(shared.attachment.visibility).toBe('Shared');
      // The officer may not open the Restricted file of #9; management may.
      expect((await failure(cases.downloadAttachment(2))).code).toBe('NotFound');
      session.as(HR_ADMIN);
      expect((await wait(cases.downloadAttachment(2))).fileName).toBe('witness-list.pdf');
    });

    it('files: the case team adds files to an open case only (EnsureFilesOpen)', async () => {
      // #12 is Resolved: management may still message, but no longer add files.
      const resolved = await wait(cases.getById(12));
      expect(resolved.status).toBe('Resolved');
      const refused = await failure(
        cases.uploadAttachment(12, resolved.rowVersion, 'CaseTeam', pdfFile()),
      );
      expect(refused.code).toBe('Conflict');
      expect(refused.message).toBe('Files can be added to an open case only.');
    });

    it('messages to the submitter notify a named submitter only', async () => {
      // #10 is Daniel Carter's named case, #4 his anonymous one.
      const named = await wait(
        cases.sendMessage(10, { rowVersion: await version(10), body: 'Hello.' }),
      );
      await wait(cases.acknowledge(4, { rowVersion: await version(4), priority: 'Low' }));
      session.as(DANIEL);
      const notes = await wait(TestBed.inject(NotificationDataService).getNotifications());
      const grievance = notes.filter((n) => n.title.startsWith('Grievance '));
      expect(grievance.map((n) => n.message)).toEqual([
        `Grievance ${named.caseNo}: there is an update on your grievance.`,
      ]);
      expect(grievance[0].link).toBe('/grievances/my/10');
    });

    it('notifies as the API does: management View holders, the assigned investigator, nobody for a plain message', async () => {
      const notes = () => wait(TestBed.inject(NotificationDataService).getNotifications());
      const grievanceNotes = async () =>
        (await notes()).filter((n) => n.title.startsWith('Grievance '));
      // The Department Manager role gets grievance-management:View only (no Edit).
      await grant(MANAGER_ROLE, { 'grievance-management': ['View'] });
      const my = TestBed.inject(MyGrievanceDataService);
      session.as(DANIEL);
      const draft = await wait(my.getById(1));
      const submitted = await wait(
        my.submit(1, { rowVersion: draft.rowVersion, declarationAccepted: true }),
      );
      // A plain message from the submitter notifies nobody.
      await wait(my.sendMessage(1, { rowVersion: submitted.rowVersion, body: 'One more detail.' }));
      session.as(MANAGER);
      expect((await grievanceNotes()).map((n) => [n.title, n.message, n.link])).toEqual([
        [
          `Grievance ${submitted.caseNo}`,
          `Grievance ${submitted.caseNo}: a new grievance was submitted.`,
          '/grievances/cases/1',
        ],
      ]);
      // The investigator hears of the assignment, nothing else of the case's own steps.
      session.as(HR_ADMIN);
      await wait(cases.acknowledge(1, { rowVersion: await version(1), priority: 'Low' }));
      await wait(
        cases.assign(1, { rowVersion: await version(1), investigatorEmployeeId: 9, role: 'Lead' }),
      );
      session.as(HR_OFFICER);
      const told = (await grievanceNotes()).filter(
        (n) => n.title === `Grievance ${submitted.caseNo}`,
      );
      expect(told.map((n) => [n.message, n.link])).toEqual([
        [
          `Grievance ${submitted.caseNo}: you were assigned to investigate it.`,
          '/grievances/investigations/1',
        ],
      ]);
    });

    it('corrective actions keep their own rowVersion', async () => {
      const action = await wait(
        cases.addCorrectiveAction(11, {
          rowVersion: await version(11),
          actionType: 'Training',
          description: 'Sample action.',
        }),
      );
      expect(action).toMatchObject({ caseId: 11, status: 'Proposed', rowVersion: '1' });
      const update = {
        rowVersion: action.rowVersion,
        actionType: 'Training',
        description: 'Sample action.',
        responsibleText: null,
        dueDate: null,
        completedOn: null,
        status: 'Implemented' as const,
      };
      expect((await failure(cases.updateCorrectiveAction(action.id, update))).code).toBe(
        'Validation',
      );
      const done = await wait(
        cases.updateCorrectiveAction(action.id, { ...update, completedOn: '2026-01-02' }),
      );
      expect(done.rowVersion).toBe('2');
      // The old token (a valid body otherwise) is stale now.
      const stale = await failure(
        cases.updateCorrectiveAction(action.id, { ...update, completedOn: '2026-01-02' }),
      );
      expect(stale.code).toBe('Conflict');
    });

    it('lists investigators without filtering per case, for management Edit only', async () => {
      const list = await wait(cases.investigators());
      expect(list.map((o) => o.employeeId).sort((a, b) => a - b)).toEqual([2, 9]);
      session.as(HR_OFFICER);
      expect((await failure(cases.investigators())).code).toBe('Forbidden');
    });

    it('audit log: administrators only, newest first, views recorded', async () => {
      await wait(cases.getById(7));
      const log = await wait(cases.audit(7, PAGE));
      expect(log.items[0]).toMatchObject({
        action: 'ViewCase',
        actorUserId: 1,
        caseNo: expect.any(String),
      });
      session.as(HR_OFFICER);
      expect((await failure(cases.audit(7, PAGE))).code).toBe('Forbidden');
    });
  });
});
