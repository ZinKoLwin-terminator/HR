import { TestBed } from '@angular/core/testing';
import {
  GRIEVANCE_OPEN_DRAFTS_MAX,
  GRIEVANCE_SUBMITTER_DAILY_UPLOAD_BYTES,
  GrievanceDraftInput,
} from '../../core/models/grievance.models';
import { PermissionAction } from '../../core/models/settings.models';
import {
  GrievanceCaseDataService,
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { todayIso } from '../../shared/utilities/date.util';
import { GrievanceInsightMockService } from './grievance-insight-mock.service';
import { GrievanceMockStore } from './grievance-mock-store';
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

// The security-review changes (anonymity, quotas, the reviewer tier, redaction, the access log).

const DRAFT: GrievanceDraftInput = {
  categoryId: 9,
  subject: 'Sample draft',
  description: '',
  incidentDate: null,
  location: null,
  involvedPersonText: null,
  involvedDepartmentId: null,
  isAnonymous: false,
};

describe('grievance mock: security review', () => {
  let cases: GrievanceCaseDataService;
  let my: MyGrievanceDataService;
  let session: ReturnType<typeof configureGrievanceTest>;

  beforeEach(() => {
    session = configureGrievanceTest(HR_ADMIN);
    cases = TestBed.inject(GrievanceCaseDataService);
    my = TestBed.inject(MyGrievanceDataService);
  });

  async function grant(roleId: number, extra: Record<string, PermissionAction[]>) {
    const permissions = TestBed.inject(RolePermissionDataService);
    const current = await wait(permissions.getForRole(roleId));
    await wait(permissions.save(roleId, { ...current.grants, ...extra }));
  }

  describe('the submitter', () => {
    beforeEach(() => session.as(DANIEL));

    it('fixes how a draft reports when it is created', async () => {
      const draft = await wait(my.createDraft(DRAFT));
      const error = await failure(
        my.updateDraft(draft.id, { ...DRAFT, isAnonymous: true, rowVersion: draft.rowVersion }),
      );
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['isAnonymous']).toBe(
        'Start a new draft to change how you report.',
      );
    });

    it(`allows at most ${GRIEVANCE_OPEN_DRAFTS_MAX} drafts`, async () => {
      // Daniel Carter starts with two drafts (#1 and #2).
      for (let i = 2; i < GRIEVANCE_OPEN_DRAFTS_MAX; i++) {
        await wait(my.createDraft(DRAFT));
      }
      const error = await failure(my.createDraft(DRAFT));
      expect(error.code).toBe('Validation');
      expect(error.message).toContain(`${GRIEVANCE_OPEN_DRAFTS_MAX} drafts`);
    });

    it('stops uploads past the daily quota', async () => {
      TestBed.inject(GrievanceMockStore).recordUpload(
        5,
        todayIso(),
        GRIEVANCE_SUBMITTER_DAILY_UPLOAD_BYTES - 4,
      );
      const four = await wait(my.getById(4));
      const error = await failure(my.uploadAttachment(4, four.rowVersion, pdfFile()));
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['file']).toContain('100 MB');
    });

    it('keeps the submitter’s file quota apart from the case team’s', async () => {
      // #10 has one file of the submitter; the case team adds one: the submitter can still add 9.
      session.as(HR_ADMIN);
      const ten = await wait(cases.getById(10));
      await wait(cases.uploadAttachment(10, ten.rowVersion, 'Shared', pdfFile('hr.pdf')));
      session.as(DANIEL);
      const mine = await wait(my.getById(10));
      for (let i = 0; i < 9; i++) {
        await wait(my.uploadAttachment(10, mine.rowVersion, pdfFile(`f${i}.pdf`)));
      }
      expect((await failure(my.uploadAttachment(10, mine.rowVersion, pdfFile()))).code).toBe(
        'Conflict',
      );
    });
  });

  describe('the reviewer tier', () => {
    it('never lets an active investigator decide a proposal', async () => {
      // Management proposes on #6 and adds the HR Officer as a member; the officer also reviews.
      const six = await wait(cases.getById(6));
      const proposed = await wait(
        cases.proposeResolution(6, { rowVersion: six.rowVersion, summary: 'Sample summary.' }),
      );
      await wait(
        cases.assign(6, {
          rowVersion: proposed.rowVersion,
          investigatorEmployeeId: 9,
          role: 'Member',
        }),
      );
      await grant(2, { 'grievance-review': ['View', 'Approve'] });
      session.as(HR_OFFICER);
      const seen = await wait(cases.getById(6));
      expect(seen.viewerRoles).toEqual(['Investigator', 'Reviewer']);
      const error = await failure(
        cases.approveResolution(6, { rowVersion: seen.rowVersion, visibleToSubmitter: true }),
      );
      expect(error.code).toBe('Forbidden');
    });

    it('gives a reviewer-only caller no assignment, escalation or history reasons', async () => {
      await grant(3, { 'grievance-review': ['View', 'Approve'] });
      session.as(MANAGER);
      const nine = await wait(cases.getById(9));
      expect(nine.viewerRoles).toEqual(['Reviewer']);
      expect(nine.escalations.every((e) => e.reason === null)).toBe(true);
      expect(nine.assignments.every((a) => a.reason === null)).toBe(true);
      const history = await wait(cases.history(9));
      expect(history.find((h) => h.action === 'Escalate')?.reason).toBeNull();
      // Proposal steps keep theirs (#11 has a pending proposal).
      const eleven = await wait(cases.history(11));
      expect(eleven.every((h) => h.action === 'ProposeResolution' || h.reason === null)).toBe(true);
    });
  });

  it('leaves the caller’s subject cases out of the recent activity', async () => {
    const insights = TestBed.inject(GrievanceInsightDataService);
    const seven = await wait(cases.getById(7));
    const escalated = await wait(
      cases.escalate(7, { rowVersion: seven.rowVersion, reason: 'Urgent.', isImmediate: false }),
    );
    expect((await wait(insights.hrDashboard())).recentActivity[0].caseId).toBe(7);
    await wait(
      cases.triage(7, {
        rowVersion: escalated.rowVersion,
        categoryId: escalated.categoryId,
        priority: escalated.priority,
        targetResolutionDate: escalated.targetResolutionDate,
        subjectEmployeeId: 2,
        involvedDepartmentId: escalated.involvedDepartmentId,
        legalHold: false,
      }),
    );
    const after = await wait(insights.hrDashboard());
    expect(after.recentActivity.some((a) => a.caseId === 7)).toBe(false);
  });

  describe('the module-wide access log', () => {
    it('lists exports (no case) and case events for administrators, with filters', async () => {
      TestBed.inject(GrievanceInsightMockService).exportRows(
        { moduleCode: 'grievance-cases', scope: 'all' },
        100,
      );
      await wait(cases.getById(3));
      const all = await wait(cases.auditLog(PAGE));
      expect(all.items[0]).toMatchObject({ action: 'ViewCase', caseId: 3 });
      expect(all.items.some((a) => a.action === 'Export' && a.caseId === null)).toBe(true);
      const exports = await wait(cases.auditLog({ ...PAGE, filters: { action: 'Export' } }));
      expect(exports.items.every((a) => a.action === 'Export')).toBe(true);
      const none = await wait(cases.auditLog({ ...PAGE, filters: { to: '2000-01-01' } }));
      expect(none.total).toBe(0);
    });

    it('is refused to a manager who is not an administrator', async () => {
      await grant(2, { 'grievance-management': ['View'] });
      session.as(HR_OFFICER);
      expect((await failure(cases.auditLog(PAGE))).code).toBe('Forbidden');
    });
  });
});
