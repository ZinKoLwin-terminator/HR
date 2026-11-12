import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { Application, Candidate, Interview } from '../../core/models/recruitment.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { BlacklistDataService } from '../../core/services/data/blacklist-data.service';
import {
  EmployeeDataService,
  EmployeeLookupDataService,
} from '../../core/services/data/employee-data.service';
import { MyPayslipDataService } from '../../core/services/data/payroll-data.services';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../core/services/data/recruitment-data.services';
import { MOCK_BLACKLIST } from '../../mock-data/employees/blacklist.mock';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { MOCK_PAYROLL_RUNS, MOCK_PAYSLIPS } from '../../mock-data/payroll/payroll-runs.mock';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { addDays, todayIso } from '../../shared/utilities/date.util';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

const ADMIN: CurrentUser = {
  id: 1,
  employeeId: 2,
  displayName: 'Demo Admin',
  email: 'admin@example.test',
  jobTitle: '',
  roles: ['HR Administrator'],
};
const user = signal<CurrentUser>(ADMIN);

function configure(): void {
  user.set(ADMIN);
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: user } },
    ],
  });
}

const currentEmployee = MOCK_EMPLOYEES.find((e) => !e.exitDate)!;
const listed = MOCK_EMPLOYEES.find((e) => e.id === MOCK_BLACKLIST[0].employeeId)!;
const otherFormer = MOCK_EMPLOYEES.find((e) => !!e.exitDate && e.id !== listed.id)!;

describe('do-not-rehire list (mock)', () => {
  let list: BlacklistDataService;

  beforeEach(() => {
    configure();
    list = TestBed.inject(BlacklistDataService);
  });

  it('starts with one fictional entry in force', async () => {
    const page = await firstValueFrom(list.query({ pageIndex: 0, pageSize: 10 }));
    expect(page.total).toBe(1);
    expect(page.items[0].liftedOn).toBeNull();
    expect(page.items[0].employeeId).toBe(listed.id);
  });

  it('lists only a former employee, with a required reason of at most 500 characters', async () => {
    await expectError(firstValueFrom(list.add(currentEmployee.id, 'Not allowed')), 'Conflict');
    const blank = await expectError(firstValueFrom(list.add(otherFormer.id, '   ')), 'Validation');
    expect(blank.fieldErrors?.['reason']).toBeTruthy();
    await expectError(firstValueFrom(list.add(otherFormer.id, 'x'.repeat(501))), 'Validation');
    await expectError(firstValueFrom(list.add(999999, 'Unknown employee')), 'Validation');
    const entry = await firstValueFrom(list.add(otherFormer.id, '  Sample reason  '));
    expect(entry.reason).toBe('Sample reason');
    expect(entry.liftedOn).toBeNull();
    expect(entry.blacklistedOn).toBe(todayIso());
    expect(entry.blacklistedBy).toBe('Demo Admin');
  });

  it('allows one entry in force per employee', async () => {
    const error = await expectError(firstValueFrom(list.add(listed.id, 'Again')), 'Conflict');
    expect(error.message).toBe('This employee is already on the do-not-rehire list.');
  });

  it('lifts with a reason, keeps the history and allows listing again', async () => {
    const [entry] = (await firstValueFrom(list.query({ pageIndex: 0, pageSize: 10 }))).items;
    await expectError(firstValueFrom(list.lift(entry.id, ' ')), 'Validation');
    const lifted = await firstValueFrom(list.lift(entry.id, 'Sample: cleared after review'));
    expect(lifted.liftedOn).toBe(todayIso());
    expect(lifted.liftReason).toBe('Sample: cleared after review');
    await expectError(firstValueFrom(list.lift(entry.id, 'Again')), 'Conflict');
    expect(await firstValueFrom(list.getActiveForEmployee(listed.id))).toBeNull();

    const inForce = await firstValueFrom(
      list.query({ pageIndex: 0, pageSize: 10, filters: { active: 'true' } }),
    );
    const history = await firstValueFrom(
      list.query({ pageIndex: 0, pageSize: 10, filters: { active: 'false' } }),
    );
    expect(inForce.total).toBe(0);
    expect(history.total).toBe(1);

    await firstValueFrom(list.add(listed.id, 'Sample: listed again'));
    const all = await firstValueFrom(list.query({ pageIndex: 0, pageSize: 10 }));
    expect(all.total).toBe(2);
  });

  it('searches by employee code or name and sorts newest listing first', async () => {
    await firstValueFrom(list.add(otherFormer.id, 'Sample reason'));
    const byCode = await firstValueFrom(
      list.query({ pageIndex: 0, pageSize: 10, search: listed.employeeCode }),
    );
    expect(byCode.items.map((e) => e.employeeId)).toEqual([listed.id]);
    const all = await firstValueFrom(list.query({ pageIndex: 0, pageSize: 10 }));
    expect(all.items[0].employeeId).toBe(otherFormer.id);
  });

  it('returns the active entry of an employee, or null', async () => {
    expect((await firstValueFrom(list.getActiveForEmployee(listed.id)))?.employeeId).toBe(
      listed.id,
    );
    expect(await firstValueFrom(list.getActiveForEmployee(currentEmployee.id))).toBeNull();
  });

  it('keeps an employee with do-not-rehire history from being deleted', async () => {
    const employees = TestBed.inject(EmployeeDataService);
    const error = await expectError(firstValueFrom(employees.delete(listed.id)), 'Conflict');
    expect(error.message).toContain('do-not-rehire');
  });

  it('never writes the reason to the activity feed', async () => {
    await firstValueFrom(list.add(otherFormer.id, 'Secret sample reason'));
    const { ActivityDataService } = await import('../../core/services/data/activity-data.service');
    const feed = await firstValueFrom(TestBed.inject(ActivityDataService).getRecent(500));
    const lines = feed.map((a) => a.message).filter((m) => m.includes('do-not-rehire'));
    expect(lines).toContain(`Employee ${otherFormer.employeeCode} added to the do-not-rehire list`);
    expect(feed.some((a) => a.message.includes('Secret sample reason'))).toBe(false);
  });
});

describe('recruitment and the do-not-rehire list (mock)', () => {
  let candidates: CandidateDataService;
  let applications: ApplicationDataService;
  let interviews: InterviewDataService;
  let jobs: JobOpeningDataService;
  let list: BlacklistDataService;
  const today = todayIso();

  beforeEach(() => {
    configure();
    candidates = TestBed.inject(CandidateDataService);
    applications = TestBed.inject(ApplicationDataService);
    interviews = TestBed.inject(InterviewDataService);
    jobs = TestBed.inject(JobOpeningDataService);
    list = TestBed.inject(BlacklistDataService);
  });

  const newCandidate = (overrides: Partial<Candidate> = {}) =>
    ({
      firstName: 'Sample',
      lastName: 'Applicant',
      email: 'sample.applicant@example.test',
      phone: '09 111 222 333',
      source: 'Referral',
      currentEmployer: '',
      experienceYears: 2,
      resumeName: null,
      notes: '',
      ...overrides,
    }) as Parameters<CandidateDataService['create']>[0];

  it('flags a candidate whose email matches a listed employee, ignoring case and spaces', async () => {
    const created = await firstValueFrom(
      candidates.create(newCandidate({ email: `  ${listed.workEmail.toUpperCase()} ` })),
    );
    expect(created.blacklistMatch).toBe(true);
    const plain = await firstValueFrom(candidates.create(newCandidate({ phone: '09000000001' })));
    expect(plain.blacklistMatch).toBe(false);
  });

  it('matches by personal email, by phone digits only and by national id', async () => {
    const byPersonal = await firstValueFrom(
      candidates.create(newCandidate({ email: listed.personalEmail })),
    );
    expect(byPersonal.blacklistMatch).toBe(true);

    const digits = listed.phone.replace(/\D/g, '');
    const spaced = digits.replace(/(\d{2})(\d+)/, '$1 $2');
    const byPhone = await firstValueFrom(
      candidates.create(newCandidate({ email: 'phone.match@example.test', phone: spaced })),
    );
    expect(byPhone.blacklistMatch).toBe(true);

    const byNational = await firstValueFrom(
      candidates.create(
        newCandidate({
          email: 'national.match@example.test',
          phone: '09000000002',
          nationalId: ` ${listed.nationalId.toLowerCase()} `,
        }),
      ),
    );
    expect(byNational.blacklistMatch).toBe(true);
  });

  it('never returns the national id; omitting it or a blank field keeps it (the form never prefills it)', async () => {
    const created = await firstValueFrom(
      candidates.create(
        newCandidate({
          email: 'keeper@example.test',
          phone: '09000000003',
          nationalId: listed.nationalId,
        }),
      ),
    );
    expect(created.nationalId).toBeUndefined();
    expect(created.blacklistMatch).toBe(true);

    const renamed = await firstValueFrom(candidates.update(created.id, { lastName: 'Renamed' }));
    expect(renamed.nationalId).toBeUndefined();
    expect(renamed.blacklistMatch).toBe(true); // the stored national id was kept

    const blank = await firstValueFrom(candidates.update(created.id, { nationalId: '' }));
    expect(blank.blacklistMatch).toBe(true); // a blank (not prefilled) field keeps the stored value
    const read = await firstValueFrom(candidates.getById(created.id));
    expect('nationalId' in read).toBe(false);
  });

  it('stops matching once the entry is lifted, and shows the flag on applications', async () => {
    const created = await firstValueFrom(
      candidates.create(newCandidate({ email: listed.workEmail })),
    );
    const job = (await firstValueFrom(jobs.getAll())).find((j) => j.status === 'Open')!;
    const application = await firstValueFrom(
      applications.create({
        jobOpeningId: job.id,
        candidateId: created.id,
        stage: 'Applied',
        appliedOn: today,
        rejectionReason: null,
        notes: '',
      }),
    );
    expect(application.blacklistMatch).toBe(true);
    expect(
      (await firstValueFrom(applications.getAll())).find((a) => a.id === application.id)
        ?.blacklistMatch,
    ).toBe(true);

    const entry = await firstValueFrom(list.getActiveForEmployee(listed.id));
    await firstValueFrom(list.lift(entry!.id, 'Sample: cleared'));
    expect((await firstValueFrom(applications.getById(application.id))).blacklistMatch).toBe(false);
    expect((await firstValueFrom(candidates.getById(created.id))).blacklistMatch).toBe(false);
  });

  /** An application at the Interview stage with a completed "Hire" interview, ready to select. */
  async function readyToSelect(candidate: Candidate): Promise<Application> {
    const job = await firstValueFrom(
      jobs.create({
        title: 'Sample Analyst',
        departmentId: 3,
        hiringManagerId: currentEmployee.id,
        employmentType: 'Full-time',
        vacancies: 1,
        postedOn: today,
        closingDate: addDays(today, 30),
        description: '',
        code: '',
        status: 'Open',
      } as Parameters<JobOpeningDataService['create']>[0]),
    );
    await firstValueFrom(jobs.changeStatus(job.id, 'Open'));
    const application = await firstValueFrom(
      applications.create({
        jobOpeningId: job.id,
        candidateId: candidate.id,
        stage: 'Applied',
        appliedOn: today,
        rejectionReason: null,
        notes: '',
      }),
    );
    await firstValueFrom(applications.moveStage(application.id, 'Screening'));
    await firstValueFrom(applications.moveStage(application.id, 'Interview'));
    await firstValueFrom(
      interviews.create({
        applicationId: application.id,
        round: 'HR',
        date: addDays(today, -1),
        startTime: '10:00',
        durationMinutes: 30,
        mode: 'Video',
        interviewerId: currentEmployee.id,
        location: '',
        status: 'Completed',
        score: 4,
        recommendation: 'Hire',
        feedback: '',
      } as Omit<Interview, 'id'>),
    );
    return application;
  }

  it('refuses to select a matching candidate without an override', async () => {
    const candidate = await firstValueFrom(
      candidates.create(newCandidate({ email: listed.workEmail })),
    );
    const application = await readyToSelect(candidate);
    const error = await expectError(
      firstValueFrom(applications.moveStage(application.id, 'Selected')),
      'Conflict',
    );
    expect(error.message).toBe(
      'This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason.',
    );
    expect((await firstValueFrom(applications.getById(application.id))).stage).toBe('Interview');
  });

  it('lets an HR Administrator approve with a reason of at most 300 characters', async () => {
    const candidate = await firstValueFrom(
      candidates.create(newCandidate({ email: listed.workEmail })),
    );
    const application = await readyToSelect(candidate);
    await expectError(
      firstValueFrom(applications.moveStage(application.id, 'Selected', '', 'x'.repeat(301))),
      'Validation',
    );
    const moved = await firstValueFrom(
      applications.moveStage(application.id, 'Selected', '', 'Sample: approved after review'),
    );
    expect(moved.stage).toBe('Selected');
    expect(moved.blacklistMatch).toBe(true);
  });

  it('refuses an override from anyone who is not an HR Administrator', async () => {
    const candidate = await firstValueFrom(
      candidates.create(newCandidate({ email: listed.workEmail })),
    );
    const application = await readyToSelect(candidate);
    user.set({ ...ADMIN, roles: ['Recruiter'] });
    const error = await expectError(
      firstValueFrom(applications.moveStage(application.id, 'Selected', '', 'Trying anyway')),
      'Forbidden',
    );
    expect(error.message).toBe(
      'Only an HR Administrator can approve the hire of a candidate who matches the do-not-rehire list.',
    );
  });

  it('ignores an override for a candidate without a match', async () => {
    const candidate = await firstValueFrom(
      candidates.create(newCandidate({ phone: '09000000009' })),
    );
    const application = await readyToSelect(candidate);
    const moved = await firstValueFrom(
      applications.moveStage(application.id, 'Selected', '', 'Not needed'),
    );
    expect(moved.stage).toBe('Selected');
  });
});

describe('employee lookup (mock)', () => {
  beforeEach(configure);

  it('returns current employees only, ordered by first name, with only non-personal fields', async () => {
    const lookup = TestBed.inject(EmployeeLookupDataService);
    const page = await firstValueFrom(lookup.query({ pageIndex: 0, pageSize: 200 }));
    const ids = page.items.map((i) => i.id);
    expect(ids).not.toContain(listed.id);
    expect(ids).not.toContain(otherFormer.id);
    expect(Object.keys(page.items[0]).sort()).toEqual(
      ['departmentName', 'displayName', 'employeeCode', 'id', 'jobTitle'].sort(),
    );
    const names = page.items.map((i) => i.displayName);
    expect([...names].sort((a, b) => a.localeCompare(b))).toEqual(names.length ? names : []);
  });

  it('searches by code or name and caps the page size at 200', async () => {
    const lookup = TestBed.inject(EmployeeLookupDataService);
    const byCode = await firstValueFrom(
      lookup.query({ pageIndex: 0, pageSize: 20, search: currentEmployee.employeeCode }),
    );
    expect(byCode.items.map((i) => i.id)).toContain(currentEmployee.id);
    const huge = await firstValueFrom(lookup.query({ pageIndex: 0, pageSize: 5000 }));
    expect(huge.pageSize).toBe(200);
  });
});

describe('my payslips (mock)', () => {
  beforeEach(configure);

  const released = new Set(
    MOCK_PAYROLL_RUNS.filter((r) => r.status === 'Approved' || r.status === 'Paid').map(
      (r) => r.id,
    ),
  );
  const owner = MOCK_PAYSLIPS.find((p) => released.has(p.runId))!;

  it("returns the signed-in employee's payslips from approved or paid runs, newest first", async () => {
    user.set({ ...ADMIN, employeeId: owner.employeeId });
    const mine = TestBed.inject(MyPayslipDataService);
    const page = await firstValueFrom(mine.query({ pageIndex: 0, pageSize: 12 }));
    expect(page.items.length).toBeGreaterThan(0);
    expect(page.items.every((p) => p.employeeId === owner.employeeId)).toBe(true);
    expect(page.items.every((p) => released.has(p.runId))).toBe(true);
    const runs = new Map(MOCK_PAYROLL_RUNS.map((r) => [r.id, r.startDate]));
    const starts = page.items.map((p) => runs.get(p.runId)!);
    expect([...starts].sort().reverse()).toEqual(starts);
  });

  it('is empty without a linked employee, and hides other employees payslips', async () => {
    const mine = TestBed.inject(MyPayslipDataService);
    user.set({ ...ADMIN, employeeId: null });
    expect((await firstValueFrom(mine.query({ pageIndex: 0, pageSize: 12 }))).total).toBe(0);
    await expectError(firstValueFrom(mine.getById(owner.id)), 'NotFound');

    user.set({ ...ADMIN, employeeId: owner.employeeId + 1000 });
    await expectError(firstValueFrom(mine.getById(owner.id)), 'NotFound');

    user.set({ ...ADMIN, employeeId: owner.employeeId });
    expect((await firstValueFrom(mine.getById(owner.id))).id).toBe(owner.id);
  });
});
