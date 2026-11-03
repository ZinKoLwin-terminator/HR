import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { Interview } from '../../core/models/recruitment.models';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../core/services/data/recruitment-data.services';
import {
  MOCK_APPLICATIONS,
  MOCK_INTERVIEWS,
  MOCK_JOB_OPENINGS,
} from '../../mock-data/recruitment/recruitment.mock';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { addDays, dayOfWeek, todayIso } from '../../shared/utilities/date.util';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('recruitment seed data', () => {
  it('is consistent', () => {
    const jobs = new Set(MOCK_JOB_OPENINGS.map((j) => j.id));
    const apps = new Set(MOCK_APPLICATIONS.map((a) => a.id));
    MOCK_APPLICATIONS.forEach((a) => expect(jobs.has(a.jobOpeningId)).toBe(true));
    MOCK_INTERVIEWS.forEach((i) => expect(apps.has(i.applicationId)).toBe(true));
    // no candidate applies twice to one opening
    const pairs = MOCK_APPLICATIONS.map((a) => `${a.jobOpeningId}:${a.candidateId}`);
    expect(new Set(pairs).size).toBe(pairs.length);
    // filled openings have enough selected candidates
    for (const job of MOCK_JOB_OPENINGS.filter((j) => j.status === 'Filled')) {
      const selected = MOCK_APPLICATIONS.filter(
        (a) => a.jobOpeningId === job.id && a.stage === 'Selected',
      );
      expect(selected.length).toBe(job.vacancies);
    }
    // completed interviews are scored; none are on weekends
    MOCK_INTERVIEWS.filter((i) => i.status === 'Completed').forEach((i) => {
      expect(i.score).not.toBeNull();
      expect(i.recommendation).not.toBeNull();
    });
    MOCK_INTERVIEWS.forEach((i) => expect([0, 6]).not.toContain(dayOfWeek(i.date)));
  });
});

describe('recruitment data services', () => {
  let jobs: JobOpeningDataService;
  let candidates: CandidateDataService;
  let applications: ApplicationDataService;
  let interviews: InterviewDataService;
  const today = todayIso();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    jobs = TestBed.inject(JobOpeningDataService);
    candidates = TestBed.inject(CandidateDataService);
    applications = TestBed.inject(ApplicationDataService);
    interviews = TestBed.inject(InterviewDataService);
  });

  const newJob = (overrides = {}) =>
    ({
      title: 'Data Analyst',
      departmentId: 3,
      hiringManagerId: 4,
      employmentType: 'Full-time',
      vacancies: 1,
      postedOn: today,
      closingDate: addDays(today, 30),
      description: '',
      code: '',
      status: 'Open',
      ...overrides,
    }) as Parameters<JobOpeningDataService['create']>[0];
  const nextWeekday = (from: number): string => {
    let d = addDays(today, from);
    while ([0, 6].includes(dayOfWeek(d))) d = addDays(d, 1);
    return d;
  };
  const newInterview = (applicationId: number, overrides: Partial<Interview> = {}) =>
    ({
      applicationId,
      round: 'Technical',
      date: nextWeekday(5),
      startTime: '10:00',
      durationMinutes: 60,
      mode: 'Video',
      interviewerId: 5,
      location: '',
      status: 'Scheduled',
      score: null,
      recommendation: null,
      feedback: '',
      ...overrides,
    }) as Omit<Interview, 'id'>;

  describe('job openings', () => {
    it('creates a draft with a generated code and validates', async () => {
      const job = await firstValueFrom(jobs.create(newJob()));
      expect(job.status).toBe('Draft');
      expect(job.code).toBe(`JOB-${today.slice(0, 4)}-${String(job.id).padStart(3, '0')}`);
      await expectError(firstValueFrom(jobs.create(newJob({ vacancies: 0 }))), 'Validation');
      await expectError(firstValueFrom(jobs.create(newJob({ departmentId: 999 }))), 'Validation');
      await expectError(
        firstValueFrom(jobs.create(newJob({ closingDate: addDays(today, -40) }))),
        'Validation',
      );
    });

    it('follows the status rules', async () => {
      const job = await firstValueFrom(jobs.create(newJob()));
      expect((await firstValueFrom(jobs.changeStatus(job.id, 'Open'))).status).toBe('Open');
      await expectError(firstValueFrom(jobs.changeStatus(job.id, 'Draft')), 'Conflict');
      expect((await firstValueFrom(jobs.changeStatus(job.id, 'On Hold'))).status).toBe('On Hold');
      await firstValueFrom(jobs.changeStatus(job.id, 'Closed'));
      await expectError(firstValueFrom(jobs.update(job.id, { title: 'Other' })), 'Conflict');
      // Filled openings cannot be changed by hand (id 2 is the seeded filled one)
      await expectError(firstValueFrom(jobs.changeStatus(2, 'Open')), 'Conflict');
      // an opening whose closing date passed cannot be reopened
      const old = await firstValueFrom(jobs.changeStatus(7, 'Open').pipe()).catch((e) => e);
      expect((old as AppError).code).toBe('Conflict');
    });

    it('is protected from deletion while it has applications', async () => {
      const error = await expectError(firstValueFrom(jobs.delete(1)), 'Conflict');
      expect(error.message).toContain('application(s)');
      const job = await firstValueFrom(jobs.create(newJob()));
      await firstValueFrom(jobs.delete(job.id));
    });
  });

  describe('candidates', () => {
    it('requires a unique email and protects candidates with applications', async () => {
      const first = await firstValueFrom(candidates.getById(1));
      const { id: _id, ...model } = first;
      void _id;
      await expectError(firstValueFrom(candidates.create({ ...model })), 'Validation');
      const created = await firstValueFrom(
        candidates.create({ ...model, email: 'New.Person@Example.com' }),
      );
      expect(created.email).toBe('new.person@example.com');
      await firstValueFrom(candidates.delete(created.id));
      await expectError(firstValueFrom(candidates.delete(1)), 'Conflict');
    });
  });

  describe('applications', () => {
    it('only accepts new applications for open jobs and rejects duplicates', async () => {
      const base = {
        stage: 'Applied',
        appliedOn: today,
        rejectionReason: null,
        notes: '',
      } as const;
      const created = await firstValueFrom(
        applications.create({ ...base, jobOpeningId: 1, candidateId: 20 }),
      );
      expect(created.stage).toBe('Applied');
      await expectError(
        firstValueFrom(applications.create({ ...base, jobOpeningId: 1, candidateId: 20 })),
        'Validation',
      );
      await expectError(
        firstValueFrom(applications.create({ ...base, jobOpeningId: 8, candidateId: 20 })),
        'Validation',
      ); // draft
      await expectError(
        firstValueFrom(applications.create({ ...base, jobOpeningId: 6, candidateId: 19 })),
        'Validation',
      ); // on hold
    });

    it('moves along the pipeline and enforces the transition rules', async () => {
      // application 3: Applied → Screening
      expect((await firstValueFrom(applications.moveStage(3, 'Screening'))).stage).toBe(
        'Screening',
      );
      await expectError(firstValueFrom(applications.moveStage(3, 'Selected')), 'Conflict');
      await expectError(firstValueFrom(applications.moveStage(3, 'Applied')), 'Conflict');
      await expectError(firstValueFrom(applications.moveStage(3, 'Rejected', '  ')), 'Validation');
      const rejected = await firstValueFrom(
        applications.moveStage(3, 'Rejected', 'Salary mismatch'),
      );
      expect(rejected).toMatchObject({ stage: 'Rejected', rejectionReason: 'Salary mismatch' });
      await expectError(firstValueFrom(applications.moveStage(3, 'Screening')), 'Conflict'); // final
    });

    it('needs a positive completed interview to select, and respects the vacancy count', async () => {
      // application 12 (Screening) has no interviews
      await expectError(
        firstValueFrom(applications.moveStage(12, 'Interview'))
          .then(() => applications.moveStage(12, 'Selected'))
          .then((o) => firstValueFrom(o)),
        'Conflict',
      );
      // application 5 is at Interview with a "Hire" phone screen → can be selected
      await firstValueFrom(applications.moveStage(5, 'Selected'));
      // job 1 has 2 vacancies; application 1 can also be selected, then nobody else
      await firstValueFrom(applications.moveStage(1, 'Selected'));
      expect((await firstValueFrom(jobs.getById(1))).status).toBe('Filled');
      // dropping one selected candidate reopens the job
      await firstValueFrom(applications.moveStage(5, 'Rejected', 'Offer declined'));
      expect((await firstValueFrom(jobs.getById(1))).status).toBe('Open');
    });

    it('cancels scheduled interviews when an application is rejected', async () => {
      await firstValueFrom(applications.moveStage(8, 'Rejected', 'Not a fit'));
      const mine = (await firstValueFrom(interviews.getAll())).filter((i) => i.applicationId === 8);
      expect(mine.filter((i) => i.status === 'Scheduled')).toHaveLength(0);
      expect(mine.some((i) => i.status === 'Cancelled')).toBe(true);
    });

    it('cannot delete selected applications or ones with interviews', async () => {
      await expectError(firstValueFrom(applications.delete(6)), 'Conflict'); // selected
      await expectError(firstValueFrom(applications.delete(1)), 'Conflict'); // has interviews
      await firstValueFrom(applications.delete(3)); // applied, no interviews
    });
  });

  describe('interviews', () => {
    it('schedules an interview and advances a screened candidate to Interview', async () => {
      expect((await firstValueFrom(applications.getById(2))).stage).toBe('Screening');
      const created = await firstValueFrom(interviews.create(newInterview(2)));
      expect(created.status).toBe('Scheduled');
      expect((await firstValueFrom(applications.getById(2))).stage).toBe('Interview');
    });

    it('validates scheduling', async () => {
      await expectError(firstValueFrom(interviews.create(newInterview(3))), 'Validation'); // Applied
      await expectError(
        firstValueFrom(interviews.create(newInterview(2, { date: addDays(today, -3) }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(interviews.create(newInterview(2, { interviewerId: 999 }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(interviews.create(newInterview(2, { durationMinutes: 5 }))),
        'Validation',
      );
    });

    it('stops an interviewer being double-booked', async () => {
      await firstValueFrom(
        interviews.create(newInterview(2, { date: nextWeekday(7), startTime: '10:00' })),
      );
      const clash = await expectError(
        firstValueFrom(
          interviews.create(newInterview(9, { date: nextWeekday(7), startTime: '10:30' })),
        ),
        'Validation',
      );
      expect(clash.fieldErrors?.['startTime']).toBeTruthy();
      await firstValueFrom(
        interviews.create(newInterview(9, { date: nextWeekday(7), startTime: '11:00' })),
      ); // back to back is fine
    });

    it('requires a score and recommendation to complete, then protects the record', async () => {
      const created = await firstValueFrom(
        interviews.create(newInterview(2, { date: nextWeekday(1) })),
      );
      // a future interview cannot be completed
      await expectError(
        firstValueFrom(
          interviews.update(created.id, { status: 'Completed', score: 4, recommendation: 'Hire' }),
        ),
        'Validation',
      );
      // interview 2 (seed) was completed in the past: scoring rules
      await expectError(firstValueFrom(interviews.update(2, { score: null })), 'Validation');
      await expectError(firstValueFrom(interviews.update(2, { score: 9 })), 'Validation');
      await expectError(
        firstValueFrom(interviews.update(2, { recommendation: null })),
        'Validation',
      );
      expect(
        (await firstValueFrom(interviews.update(2, { score: 5, feedback: 'Great' }))).score,
      ).toBe(5);
      await expectError(firstValueFrom(interviews.delete(2)), 'Conflict');
    });

    it('cancels and then locks an interview', async () => {
      const cancelled = await firstValueFrom(interviews.update(3, { status: 'Cancelled' }));
      expect(cancelled.status).toBe('Cancelled');
      await expectError(firstValueFrom(interviews.update(3, { status: 'Scheduled' })), 'Conflict');
      await firstValueFrom(interviews.delete(3));
    });

    it('filters by status, interviewer and date range', async () => {
      const page = (filters: object) =>
        firstValueFrom(interviews.query({ pageIndex: 0, pageSize: 100, filters })).then(
          (r) => r.items,
        );
      expect((await page({ status: 'Cancelled' })).map((i) => i.id)).toEqual([11]);
      expect((await page({ interviewerId: 10 })).every((i) => i.interviewerId === 10)).toBe(true);
      const range = await page({ dateFrom: today, dateTo: addDays(today, 60) });
      expect(range.length).toBeGreaterThan(0);
      expect(range.every((i) => i.date >= today)).toBe(true);
    });
  });
});
