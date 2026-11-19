import { TestBed } from '@angular/core/testing';
import { GrievanceIntakeRequest } from '../../core/models/grievance.models';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import {
  GrievanceCaseDataService,
  GrievanceCategoryDataService,
  MyGrievanceDataService,
} from '../../core/services/data/grievance-data.services';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { grievanceIntakeReason } from './grievance-case-mock.service';
import {
  DANIEL,
  HR_ADMIN,
  HR_OFFICER,
  PAGE,
  configureGrievanceTest,
  failure,
  wait,
} from './grievance-test-support';

/** Daniel Carter (EMP005) told HR by phone; HR Administrator (EMP002) records it. */
const intake = (changes: Partial<GrievanceIntakeRequest> = {}): GrievanceIntakeRequest => ({
  employeeId: 5,
  categoryId: 1,
  subject: 'Raised by phone with HR',
  description: 'Sample account given by phone, long enough for the rule.',
  incidentDate: null,
  location: null,
  involvedPersonText: null,
  involvedDepartmentId: null,
  receivedVia: 'Phone',
  declarationAccepted: true,
  ...changes,
});

describe('grievance mock: HR records a grievance for an employee', () => {
  let cases: GrievanceCaseDataService;
  let session: ReturnType<typeof configureGrievanceTest>;

  beforeEach(() => {
    session = configureGrievanceTest(HR_ADMIN);
    cases = TestBed.inject(GrievanceCaseDataService);
  });

  it('creates a Submitted named case of that employee with one Submit step by HR', async () => {
    const recorded = await wait(cases.recordForEmployee(intake()));
    expect(recorded).toMatchObject({
      status: 'Submitted',
      isAnonymous: false,
      subject: 'Raised by phone with HR',
    });
    expect(recorded.caseNo).toMatch(new RegExp(`^GRV-${todayIso().slice(0, 4)}-\\d{5}$`));
    expect(recorded.submittedAtUtc).not.toBeNull();

    const history = await wait(cases.history(recorded.id));
    expect(history).toHaveLength(1);
    expect(history[0]).toMatchObject({
      action: 'Submit',
      fromStatus: 'Draft',
      toStatus: 'Submitted',
      actorRole: 'Management',
      actorUserId: 1,
      actorName: 'Su Myat Noe',
      reason: 'Recorded by HR on behalf of the employee (received by phone).',
      visibleToSubmitter: true,
    });

    const listed = await wait(cases.query(PAGE));
    expect(listed.items.map((c) => c.id)).toContain(recorded.id);

    const log = await wait(cases.audit(recorded.id, PAGE));
    expect(log.items.map((a) => a.detail)).toContain('intake:Draft>Submitted');

    // The employee finds it in their own grievances, unread, and is told.
    session.as(DANIEL);
    const my = TestBed.inject(MyGrievanceDataService);
    const mine = await wait(my.query(PAGE));
    const row = mine.items.find((c) => c.id === recorded.id);
    expect(row).toMatchObject({ status: 'Submitted', isAnonymous: false, hasUnreadUpdate: true });
    const detail = await wait(my.getById(recorded.id));
    expect(detail.timeline[0]).toMatchObject({
      action: 'Submit',
      actor: 'HR team',
      reason: 'Recorded by HR on behalf of the employee (received by phone).',
    });
    const notes = await wait(TestBed.inject(NotificationDataService).getNotifications());
    expect(notes).toContainEqual(
      expect.objectContaining({
        message: `Grievance ${recorded.caseNo}: HR recorded a grievance for you.`,
        link: `/grievances/my/${recorded.id}`,
      }),
    );
  });

  it('words the history reason for every channel', () => {
    expect(grievanceIntakeReason('InPerson')).toContain('(received in person)');
    expect(grievanceIntakeReason('Email')).toContain('(received by email)');
    expect(grievanceIntakeReason('Letter')).toContain('(received by letter)');
    expect(grievanceIntakeReason('Other')).toContain('(received another way)');
  });

  it('refuses to record the caller’s own grievance', async () => {
    const error = await failure(cases.recordForEmployee(intake({ employeeId: 2 })));
    expect(error.code).toBe('Validation');
    expect(error.fieldErrors).toEqual({
      employeeId: 'Use My Grievances to raise your own grievance.',
    });
  });

  it('refuses an inactive category', async () => {
    const categories = TestBed.inject(GrievanceCategoryDataService);
    await wait(categories.update(1, { status: 'Inactive' }));
    const error = await failure(cases.recordForEmployee(intake()));
    expect(error.fieldErrors).toEqual({ categoryId: 'Choose an active category.' });
  });

  it('refuses a missing declaration', async () => {
    const error = await failure(cases.recordForEmployee(intake({ declarationAccepted: false })));
    expect(error.fieldErrors).toEqual({
      declarationAccepted:
        'Confirm that the employee gave this account and agreed that HR records it.',
    });
  });

  it('refuses an incident date in the future', async () => {
    const error = await failure(
      cases.recordForEmployee(intake({ incidentDate: addDays(todayIso(), 1) })),
    );
    expect(error.fieldErrors).toEqual({
      incidentDate: 'The incident date cannot be in the future.',
    });
  });

  it('refuses an unknown channel, a short description and an unknown employee', async () => {
    expect(
      (
        await failure(
          cases.recordForEmployee(
            intake({ receivedVia: 'Fax' as GrievanceIntakeRequest['receivedVia'] }),
          ),
        )
      ).fieldErrors,
    ).toEqual({ receivedVia: 'Choose how the grievance was received.' });
    expect(
      (await failure(cases.recordForEmployee(intake({ description: 'Too short.' })))).fieldErrors,
    ).toEqual({ description: 'Description must be at least 20 characters.' });
    expect(
      (await failure(cases.recordForEmployee(intake({ employeeId: 9999 })))).fieldErrors,
    ).toEqual({ employeeId: 'Choose an active employee.' });
  });

  it('needs grievance-management:Create', async () => {
    session.as(HR_OFFICER);
    expect((await failure(cases.recordForEmployee(intake()))).code).toBe('Forbidden');
  });
});
