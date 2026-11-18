import { TestBed } from '@angular/core/testing';
import { GrievanceDraftInput } from '../../core/models/grievance.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import {
  GRIEVANCE_MY_METHODS,
  GrievanceCaseDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { grievanceCaseNo } from '../../mock-data/grievance/grievance-records';
import { todayIso } from '../../shared/utilities/date.util';
import { GrievanceMockStore } from './grievance-mock-store';
import { MyGrievanceMockService } from './my-grievance-mock.service';
import {
  BUILT_IN_ADMIN,
  DANIEL,
  EMPLOYEE_36,
  HR_ADMIN,
  NOBODY,
  PAGE,
  configureGrievanceTest,
  failure,
  pdfFile,
  textFile,
  wait,
} from './grievance-test-support';

const DRAFT: GrievanceDraftInput = {
  categoryId: 9,
  subject: 'Noise from the loading bay',
  description:
    'Sample text: the loading bay doors stay open all morning and the noise is constant.',
  incidentDate: null,
  location: null,
  involvedPersonText: null,
  involvedDepartmentId: null,
  isAnonymous: false,
};

describe('grievance mock: my grievances (submitter)', () => {
  let my: MyGrievanceDataService;
  let cases: GrievanceCaseDataService;
  let session: ReturnType<typeof configureGrievanceTest>;

  beforeEach(() => {
    session = configureGrievanceTest(DANIEL);
    my = TestBed.inject(MyGrievanceDataService);
    cases = TestBed.inject(GrievanceCaseDataService);
  });

  it('binds every contract method to the mock', () => {
    expect(new Set(GRIEVANCE_MY_METHODS).size).toBe(GRIEVANCE_MY_METHODS.length);
    for (const method of GRIEVANCE_MY_METHODS) {
      expect(typeof MyGrievanceMockService.prototype[method], method).toBe('function');
    }
  });

  describe('scope and redaction', () => {
    it('lists only the caller’s cases, named and anonymous, drafts included', async () => {
      const page = await wait(my.query(PAGE));
      expect(page.items.map((c) => c.id).sort((a, b) => a - b)).toEqual([1, 2, 4, 10]);
      expect(page.items.find((c) => c.id === 4)?.isAnonymous).toBe(true);
    });

    it('never puts the submitter link on a returned object', async () => {
      const detail = await wait(my.getById(4));
      const json = JSON.stringify(detail);
      expect(json).not.toContain('pseudonym');
      expect(json).not.toContain('employeeId');
    });

    it('answers NotFound for someone else’s case', async () => {
      expect((await failure(my.getById(3))).code).toBe('NotFound');
    });

    it('names nobody on the HR side', async () => {
      const detail = await wait(my.getById(10));
      expect(detail.timeline.every((t) => t.actor === 'You' || t.actor === 'HR team')).toBe(true);
      expect(detail.messages.every((m) => m.actor === 'You' || m.actor === 'HR team')).toBe(true);
      expect(JSON.stringify(detail)).not.toContain('Ye Yint Naing');
      expect('notes' in detail).toBe(false);
    });

    it('shows only Shared attachments', async () => {
      const detail = await wait(my.getById(10));
      expect(detail.attachments.map((a) => a.fileName)).toEqual(['allowance-note.pdf']);
    });

    it('requires grievance:View', async () => {
      session.as(NOBODY);
      expect((await failure(my.query(PAGE))).code).toBe('Forbidden');
    });
  });

  describe('unread badge', () => {
    it('marks seen without changing the case rowVersion', async () => {
      const before = await wait(my.getById(10));
      expect(before.hasUnreadUpdate).toBe(true);
      await wait(my.markSeen(10));
      const after = await wait(my.getById(10));
      expect(after.hasUnreadUpdate).toBe(false);
      expect(after.rowVersion).toBe(before.rowVersion);
    });

    it('counts unread updates on the dashboard', async () => {
      const dashboard = await wait(my.dashboard());
      expect(dashboard).toMatchObject({ drafts: 2, open: 2, awaitingMyResponse: 1 });
      expect(dashboard.unreadUpdates).toBeGreaterThanOrEqual(1);
      expect(dashboard.recent.length).toBeLessThanOrEqual(5);
    });
  });

  describe('drafts', () => {
    it('saves a draft with a category and a subject; the minimums apply at submission', async () => {
      const empty = await failure(my.createDraft({ ...DRAFT, subject: ' ' }));
      expect(empty.fieldErrors?.['subject']).toBeDefined();
      const future = await failure(my.createDraft({ ...DRAFT, incidentDate: '2999-01-01' }));
      expect(future.fieldErrors?.['incidentDate']).toBeDefined();
      const rough = await wait(my.createDraft({ ...DRAFT, subject: 'Hi', description: '' }));
      const tooShort = await failure(
        my.submit(rough.id, { rowVersion: rough.rowVersion, declarationAccepted: true }),
      );
      expect(tooShort.code).toBe('Validation');
      expect(tooShort.fieldErrors?.['subject']).toBeDefined();
    });

    it('does not audit the submitter’s own views', async () => {
      await wait(my.getById(10));
      session.as(HR_ADMIN);
      const log = await wait(cases.audit(10, PAGE));
      expect(log.items.filter((a) => a.actorRole === 'Submitter')).toEqual([]);
    });

    it('refuses anonymity where the category does not allow it', async () => {
      const error = await failure(my.createDraft({ ...DRAFT, categoryId: 3, isAnonymous: true }));
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['isAnonymous']).toBeDefined();
    });

    it('refuses an account without an employee', async () => {
      session.as(BUILT_IN_ADMIN);
      expect((await failure(my.createDraft(DRAFT))).code).toBe('Forbidden');
    });

    it('checks the rowVersion: missing 400, stale 409', async () => {
      const draft = await wait(my.createDraft(DRAFT));
      expect((await failure(my.updateDraft(draft.id, { ...DRAFT, rowVersion: '' }))).code).toBe(
        'Validation',
      );
      await wait(my.updateDraft(draft.id, { ...DRAFT, rowVersion: draft.rowVersion }));
      const stale = await failure(
        my.updateDraft(draft.id, { ...DRAFT, rowVersion: draft.rowVersion }),
      );
      expect(stale.code).toBe('Conflict');
    });

    it('keeps drafts away from the case team', async () => {
      const draft = await wait(my.createDraft(DRAFT));
      session.as(HR_ADMIN);
      expect((await failure(cases.getById(draft.id))).code).toBe('NotFound');
      const all = await wait(cases.query(PAGE));
      expect(all.items.some((c) => c.status === 'Draft')).toBe(false);
    });

    it('deletes a draft but not a submitted case', async () => {
      const draft = await wait(my.createDraft(DRAFT));
      await wait(my.deleteDraft(draft.id, draft.rowVersion));
      expect((await failure(my.getById(draft.id))).code).toBe('NotFound');
      const submitted = await wait(my.getById(4));
      expect((await failure(my.deleteDraft(4, submitted.rowVersion))).code).toBe('Conflict');
    });
  });

  describe('submission', () => {
    it('needs the declaration', async () => {
      const draft = await wait(my.getById(1));
      const error = await failure(
        my.submit(1, { rowVersion: draft.rowVersion, declarationAccepted: false }),
      );
      expect(error.fieldErrors?.['declarationAccepted']).toBeDefined();
    });

    it('validates everything before numbering: a refused submit takes no case number', async () => {
      const expected = TestBed.inject(GrievanceMockStore).nextCaseSequence();
      const draft = await wait(my.getById(1));
      const refused = await failure(
        my.submit(1, { rowVersion: draft.rowVersion, declarationAccepted: false }),
      );
      expect(refused.code).toBe('Validation');
      const submitted = await wait(
        my.submit(1, { rowVersion: draft.rowVersion, declarationAccepted: true }),
      );
      expect(submitted.caseNo).toBe(grievanceCaseNo(Number(todayIso().slice(0, 4)), expected));
    });

    it('assigns a case number and notifies HR with the number and event only', async () => {
      const draft = await wait(my.getById(1));
      const submitted = await wait(
        my.submit(1, { rowVersion: draft.rowVersion, declarationAccepted: true }),
      );
      expect(submitted.status).toBe('Submitted');
      expect(submitted.caseNo).toMatch(/^GRV-\d{4}-\d{5}$/);
      expect(submitted.hasUnreadUpdate).toBe(false);
      session.as(HR_ADMIN);
      const notes = await wait(TestBed.inject(NotificationDataService).getNotifications());
      const mine = notes.find((n) => n.title === `Grievance ${submitted.caseNo}`);
      expect(mine?.message).toBe(`Grievance ${submitted.caseNo}: a new grievance was submitted.`);
      expect(mine?.message).not.toContain(draft.subject);
    });

    it('redacts an anonymous submission for the case team', async () => {
      const draft = await wait(my.getById(2));
      await wait(my.submit(2, { rowVersion: draft.rowVersion, declarationAccepted: true }));
      session.as(HR_ADMIN);
      const detail = await wait(cases.getById(2));
      expect(detail.submitter).toBeNull();
      expect(detail.submitterLabel).toBe('Anonymous');
      expect(detail.departmentId).toBeNull();
      expect(JSON.stringify(detail)).not.toContain('Daniel');
      const history = await wait(cases.history(2));
      expect(history[0]).toMatchObject({
        action: 'Submit',
        actorName: 'Anonymous submitter',
        actorUserId: null,
        actorEmployeeId: null,
      });
    });
  });

  describe('conversation', () => {
    it('a plain message does not change the rowVersion', async () => {
      const before = await wait(my.getById(4));
      const after = await wait(my.sendMessage(4, { rowVersion: 'ignored', body: 'More detail.' }));
      expect(after.rowVersion).toBe(before.rowVersion);
      expect(after.messages.at(-1)).toMatchObject({ kind: 'Message', actor: 'You' });
    });

    it('a response checks the rowVersion and returns the case to its status', async () => {
      const before = await wait(my.getById(10));
      expect((await failure(my.sendMessage(10, { rowVersion: '1', body: 'x' }))).code).toBe(
        'Conflict',
      );
      const after = await wait(
        my.sendMessage(10, { rowVersion: before.rowVersion, body: 'March and April.' }),
      );
      expect(after.status).toBe('UnderInvestigation');
      expect(after.rowVersion).not.toBe(before.rowVersion);
      expect(after.messages.at(-1)).toMatchObject({ kind: 'InfoResponse' });
      expect(after.timeline.at(-1)).toMatchObject({ action: 'Respond', actor: 'You' });
    });

    it('refuses messages on a draft', async () => {
      expect((await failure(my.sendMessage(1, { rowVersion: '1', body: 'x' }))).code).toBe(
        'Conflict',
      );
    });
  });

  describe('files', () => {
    it('uploads a Shared file without changing the case rowVersion', async () => {
      const before = await wait(my.getById(4));
      const result = await wait(my.uploadAttachment(4, before.rowVersion, pdfFile('proof.pdf')));
      expect(result.attachment).toMatchObject({ fileName: 'proof.pdf', uploadedBy: 'You' });
      expect(result.caseRowVersion).toBe(before.rowVersion);
      const download = await wait(my.downloadAttachment(result.attachment.id));
      expect(download.fileName).toBe('proof.pdf');
    });

    it('checks the content type and the per-case limit', async () => {
      const draft = await wait(my.createDraft(DRAFT));
      const bad = await failure(my.uploadAttachment(draft.id, draft.rowVersion, textFile()));
      expect(bad.code).toBe('Validation');
      let version = draft.rowVersion;
      for (let i = 0; i < 10; i++) {
        version = (await wait(my.uploadAttachment(draft.id, version, pdfFile(`f${i}.pdf`))))
          .caseRowVersion;
      }
      expect((await failure(my.uploadAttachment(draft.id, version, pdfFile()))).code).toBe(
        'Conflict',
      );
    });

    it('never serves a case-team file to the submitter', async () => {
      // Attachment 1 is a CaseTeam file of #9, the anonymous case of this employee.
      session.as(EMPLOYEE_36);
      expect((await wait(my.getById(9))).attachments).toEqual([]);
      expect((await failure(my.downloadAttachment(1))).code).toBe('NotFound');
    });
  });
});
