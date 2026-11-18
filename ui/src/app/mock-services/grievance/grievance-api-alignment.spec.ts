import { TestBed } from '@angular/core/testing';
import { PermissionAction } from '../../core/models/settings.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import {
  GrievanceCaseDataService,
  GrievanceInsightDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { GrievanceInsightMockService } from './grievance-insight-mock.service';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { MOCK_GRIEVANCE_CASES } from '../../mock-data/grievance/grievance-cases.mock';
import {
  DANIEL,
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
  PAGE,
  configureGrievanceTest,
  failure,
  wait,
} from './grievance-test-support';

// The rules of API slice 5 that the shared golden file does not cover.

describe('grievance mock: alignment with the API', () => {
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

  it('stores a named submitter as "Submitter" without ids, and shows the name to the case team', async () => {
    const stored = MOCK_GRIEVANCE_CASES.find((c) => c.id === 10)!.history[0];
    expect(stored).toMatchObject({
      actorName: 'Submitter',
      actorUserId: null,
      actorEmployeeId: null,
    });
    const history = await wait(cases.history(10));
    expect(history[0]).toMatchObject({
      action: 'Submit',
      actorName: 'Daniel Carter',
      actorUserId: null,
      actorEmployeeId: null,
    });
  });

  it('hides the information-request question from a reviewer-only caller', async () => {
    session.as(HR_OFFICER);
    const eight = await wait(cases.getById(8));
    await wait(cases.requestInfo(8, { rowVersion: eight.rowVersion, message: 'Which date?' }));
    await grant(3, { 'grievance-review': ['View', 'Approve'] });
    session.as(MANAGER);
    const step = (await wait(cases.history(8))).find((h) => h.action === 'RequestInfo')!;
    expect(step.reason).toBeNull();
    session.as(HR_ADMIN);
    const asManagement = (await wait(cases.history(8))).find((h) => h.action === 'RequestInfo')!;
    expect(asManagement.reason).toBe('Which date?');
  });

  it('ignores the Archived filter outside Case management', async () => {
    session.as(HR_OFFICER);
    const mine = await wait(cases.investigations({ ...PAGE, filters: { archived: true } }));
    expect(mine.items.length).toBeGreaterThan(0);
    expect(mine.items.every((c) => c.archivedAtUtc === null)).toBe(true);
  });

  it('lets triage change only the legal hold of a case that is no longer open', async () => {
    const closed = await wait(cases.getById(13));
    const base = {
      rowVersion: closed.rowVersion,
      categoryId: closed.categoryId,
      priority: closed.priority,
      targetResolutionDate: closed.targetResolutionDate,
      subjectEmployeeId: closed.subjectEmployeeId,
      involvedDepartmentId: closed.involvedDepartmentId,
      legalHold: false,
    };
    expect((await failure(cases.triage(13, { ...base, priority: 'High' }))).code).toBe('Conflict');
    const held = await wait(cases.triage(13, { ...base, legalHold: true }));
    expect(held.legalHold).toBe(true);
    expect((await failure(cases.archive(13, { rowVersion: held.rowVersion }))).code).toBe(
      'Conflict',
    );
  });

  it('audits the submitter’s actions on a submitted case without a user id', async () => {
    session.as(DANIEL);
    await wait(my.sendMessage(4, { rowVersion: 'ignored', body: 'More detail.' }));
    session.as(HR_ADMIN);
    const log = await wait(cases.audit(4, { ...PAGE, filters: { action: 'SendMessage' } }));
    expect(log.items[0]).toMatchObject({
      actorRole: 'Submitter',
      actorUserId: null,
      actorName: null,
    });
  });

  it('does not leave the submitter out of a notification their own action caused', async () => {
    // The administrator (also HR management) submits a named grievance.
    const draft = await wait(
      my.createDraft({
        categoryId: 1,
        subject: 'Sample case of the administrator',
        description: 'Sample text long enough to pass the description rule.',
        incidentDate: null,
        location: null,
        involvedPersonText: null,
        involvedDepartmentId: null,
        isAnonymous: false,
      }),
    );
    const submitted = await wait(
      my.submit(draft.id, { rowVersion: draft.rowVersion, declarationAccepted: true }),
    );
    const notes = await wait(TestBed.inject(NotificationDataService).getNotifications());
    expect(notes.map((n) => n.message)).toContain(
      `Grievance ${submitted.caseNo}: a new grievance was submitted.`,
    );
  });

  it('leaves the caller’s subject cases out of report rows, not their own submissions or the totals', async () => {
    const insights = TestBed.inject(GrievanceInsightDataService);
    // The administrator (EMP002) submits a named case, then is named the subject of #7.
    const draft = await wait(
      my.createDraft({
        categoryId: 1,
        subject: 'Sample case of the administrator',
        description: 'Sample text long enough to pass the description rule.',
        incidentDate: null,
        location: null,
        involvedPersonText: null,
        involvedDepartmentId: null,
        isAnonymous: false,
      }),
    );
    await wait(my.submit(draft.id, { rowVersion: draft.rowVersion, declarationAccepted: true }));
    const seven = await wait(cases.getById(7));
    await wait(
      cases.triage(7, {
        rowVersion: seven.rowVersion,
        categoryId: seven.categoryId,
        priority: seven.priority,
        targetResolutionDate: seven.targetResolutionDate,
        subjectEmployeeId: 2,
        involvedDepartmentId: seven.involvedDepartmentId,
        legalHold: false,
      }),
    );
    const report = await wait(insights.casesReport(PAGE));
    const ids = report.items.map((r) => r.caseId);
    expect(ids).not.toContain(7);
    expect(ids).toContain(draft.id);
    expect(report.total).toBe(16);
    const exported = TestBed.inject(GrievanceInsightMockService).exportRows(
      { moduleCode: 'grievance-cases', scope: 'all' },
      100,
    );
    expect(exported.map((r) => r['caseNo'])).not.toContain(seven.caseNo);
    // The aggregates count every case for everyone (16 samples + the new one).
    expect((await wait(insights.hrDashboard())).total).toBe(17);
  });

  it('allows a submitter message on a Resolved case', async () => {
    // #12 is Resolved; its submitter EMP037 has no seeded account, so act as them.
    session.as({ ...DANIEL, id: 50, employeeId: 37, displayName: 'EMP037' });
    const detail = await wait(my.sendMessage(12, { rowVersion: 'ignored', body: 'Thank you.' }));
    expect(detail.messages.at(-1)).toMatchObject({ kind: 'Message', actor: 'You' });
  });
});
