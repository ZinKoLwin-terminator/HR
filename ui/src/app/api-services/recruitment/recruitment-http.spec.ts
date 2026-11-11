import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../core/services/data/recruitment-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { ApplicationHttpService } from './application-http.service';
import { CandidateHttpService } from './candidate-http.service';
import { InterviewHttpService } from './interview-http.service';
import { JobOpeningHttpService } from './job-opening-http.service';
import { RecruitmentViewsHttpService } from './recruitment-views-http.service';

const opening = {
  id: 3,
  code: 'JOB-2026-003',
  title: 'QA Engineer',
  departmentId: 2,
  hiringManagerId: 5,
  employmentType: 'Internship',
  vacancies: 2,
  postedOn: '2026-11-01',
  closingDate: '2026-12-31',
  description: null,
  status: 'Draft',
  rowVersion: 'JOBV1',
};
const candidate = {
  id: 7,
  firstName: 'Ada',
  lastName: 'Sample',
  email: 'ada@example.test',
  phone: null,
  source: 'Referral',
  currentEmployer: null,
  experienceYears: 3.5,
  resumeName: null,
  notes: null,
  rowVersion: 'CANV1',
};
const application = {
  id: 11,
  jobOpeningId: 3,
  candidateId: 7,
  stage: 'Applied',
  appliedOn: '2026-11-02',
  rejectionReason: null,
  notes: null,
  rowVersion: 'APPV1',
};
const interview = {
  id: 21,
  applicationId: 11,
  round: 'Technical',
  date: '2026-11-20',
  startTime: '10:00',
  durationMinutes: 45,
  mode: 'Video',
  interviewerId: 5,
  location: null,
  status: 'Scheduled',
  score: null,
  recommendation: null,
  feedback: null,
  rowVersion: 'INTV1',
};

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  http
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return {
    http,
    jobs: TestBed.inject(JobOpeningDataService),
    candidates: TestBed.inject(CandidateDataService),
    applications: TestBed.inject(ApplicationDataService),
    interviews: TestBed.inject(InterviewDataService),
  };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

const tick = () => new Promise<void>((resolve) => setTimeout(resolve));

/** Reads the rows through the unpaged GET so their rowVersions are known. */
async function seed(
  http: HttpTestingController,
  service: { getAll(): Observable<unknown[]> },
  url: string,
  rows: object[],
) {
  const promise = firstValueFrom(service.getAll());
  http.expectOne((r) => r.method === 'GET' && r.url === url).flush(rows);
  return promise;
}

describe('Recruitment HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mock recruitment services only when API mode is on', () => {
    setup();
    expect(TestBed.inject(JobOpeningDataService)).toBeInstanceOf(JobOpeningHttpService);
    expect(TestBed.inject(CandidateDataService)).toBeInstanceOf(CandidateHttpService);
    expect(TestBed.inject(ApplicationDataService)).toBeInstanceOf(ApplicationHttpService);
    expect(TestBed.inject(InterviewDataService)).toBeInstanceOf(InterviewHttpService);
  });

  describe('reads', () => {
    it('queries job openings with paging, sort, search and filters, dropping unknown sort keys', async () => {
      const { http, jobs } = setup();
      const promise = firstValueFrom(
        jobs.query({
          pageIndex: 1,
          pageSize: 20,
          sort: { active: 'postedOn', direction: 'desc' },
          search: ' qa ',
          filters: { status: 'Open', departmentId: 2, employmentType: 'All', bogus: 'x' } as never,
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/job-openings/query');
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('20');
      expect(req.request.params.get('sort')).toBe('postedOn:desc');
      expect(req.request.params.get('search')).toBe('qa');
      expect(req.request.params.get('status')).toBe('Open');
      expect(req.request.params.get('departmentId')).toBe('2');
      expect(req.request.params.has('employmentType')).toBe(false);
      expect(req.request.params.has('bogus')).toBe(false);
      req.flush({ items: [opening], total: 1, pageIndex: 1, pageSize: 20 });
      const page = await promise;
      expect(page.total).toBe(1);
      expect(page.items[0].description).toBe('');
    });

    it('drops sort keys the API does not support', async () => {
      const { http, candidates, applications, interviews } = setup();
      const calls = [
        firstValueFrom(
          candidates.query({
            pageIndex: 0,
            pageSize: 10,
            sort: { active: 'applications', direction: 'asc' },
          }),
        ),
        firstValueFrom(
          applications.query({
            pageIndex: 0,
            pageSize: 10,
            sort: { active: 'notes', direction: 'asc' },
            filters: { jobOpeningId: 3, stage: 'Screening' },
          }),
        ),
        firstValueFrom(
          interviews.query({
            pageIndex: 0,
            pageSize: 10,
            sort: { active: 'location', direction: 'asc' },
            filters: { dateFrom: '2026-11-01', dateTo: '2026-11-30', interviewerId: 5 },
          }),
        ),
      ];
      const cand = http.expectOne((r) => r.url === '/api/candidates/query');
      expect(cand.request.params.has('sort')).toBe(false);
      const app = http.expectOne((r) => r.url === '/api/applications/query');
      expect(app.request.params.has('sort')).toBe(false);
      expect(app.request.params.get('jobOpeningId')).toBe('3');
      expect(app.request.params.get('stage')).toBe('Screening');
      const itv = http.expectOne((r) => r.url === '/api/interviews/query');
      expect(itv.request.params.has('sort')).toBe(false);
      expect(itv.request.params.get('dateFrom')).toBe('2026-11-01');
      expect(itv.request.params.get('dateTo')).toBe('2026-11-30');
      expect(itv.request.params.get('interviewerId')).toBe('5');
      for (const req of [cand, app, itv]) {
        req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      }
      await Promise.all(calls);
    });

    it('sends a supported candidate sort and the source filter', async () => {
      const { http, candidates } = setup();
      const promise = firstValueFrom(
        candidates.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'lastName', direction: 'asc' },
          filters: { source: 'Referral' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/candidates/query');
      expect(req.request.params.get('sort')).toBe('lastName:asc');
      expect(req.request.params.get('source')).toBe('Referral');
      req.flush({ items: [candidate], total: 1, pageIndex: 0, pageSize: 10 });
      const page = await promise;
      // Null text becomes "" for display; the resume stays null (the model allows it).
      expect(page.items[0]).toMatchObject({ phone: '', currentEmployer: '', notes: '' });
      expect(page.items[0].resumeName).toBeNull();
    });

    it('maps null text to "" on the unpaged lists', async () => {
      const { http, applications, interviews } = setup();
      const apps = await seed(http, applications, '/api/applications', [application]);
      expect(apps[0]).toMatchObject({ notes: '' });
      expect((apps[0] as { rejectionReason: unknown }).rejectionReason).toBeNull();
      const list = await seed(http, interviews, '/api/interviews', [interview]);
      expect(list[0]).toMatchObject({ location: '', feedback: '', score: null });
    });
  });

  describe('job openings', () => {
    it('creates with the user-owned fields only (no code, no status)', async () => {
      const { http, jobs } = setup();
      const promise = firstValueFrom(
        jobs.create({
          code: 'X',
          status: 'Open',
          title: 'QA Engineer',
          departmentId: 2,
          hiringManagerId: 5,
          employmentType: 'Internship',
          vacancies: '2' as unknown as number,
          postedOn: '2026-11-01',
          closingDate: '2026-12-31',
          description: '',
        } as never),
      );
      const req = http.expectOne('/api/job-openings');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        title: 'QA Engineer',
        departmentId: 2,
        hiringManagerId: 5,
        employmentType: 'Internship',
        vacancies: 2,
        postedOn: '2026-11-01',
        closingDate: '2026-12-31',
        description: null,
      });
      req.flush(opening);
      expect((await promise).status).toBe('Draft');
    });

    it('PUT never sends the status and carries the rowVersion', async () => {
      const { http, jobs } = setup();
      await seed(http, jobs, '/api/job-openings', [opening]);
      const promise = firstValueFrom(jobs.update(3, { title: 'QA Lead', status: 'Draft' }));
      await tick();
      const req = http.expectOne('/api/job-openings/3');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.status).toBeUndefined();
      expect(req.request.body.code).toBeUndefined();
      expect(req.request.body).toMatchObject({
        title: 'QA Lead',
        departmentId: 2,
        vacancies: 2,
        rowVersion: 'JOBV1',
      });
      req.flush({ ...opening, title: 'QA Lead', rowVersion: 'JOBV2' });
      await promise;
    });

    it('maps changeStatus to the open, reopen, hold and close commands', async () => {
      const { http, jobs } = setup();
      await seed(http, jobs, '/api/job-openings', [opening]);
      const run = async (status: 'Open' | 'On Hold' | 'Closed', path: string, next: string) => {
        const promise = firstValueFrom(jobs.changeStatus(3, status));
        await tick();
        const req = http.expectOne(`/api/job-openings/3/${path}`);
        expect(req.request.method).toBe('POST');
        expect(Object.keys(req.request.body)).toEqual(['rowVersion']);
        req.flush({ ...opening, status: status, rowVersion: next });
        await promise;
        return req.request.body.rowVersion;
      };
      expect(await run('Open', 'open', 'JOBV2')).toBe('JOBV1');
      expect(await run('On Hold', 'hold', 'JOBV3')).toBe('JOBV2');
      expect(await run('Closed', 'close', 'JOBV4')).toBe('JOBV3');
      expect(await run('Open', 'reopen', 'JOBV5')).toBe('JOBV4');
    });

    it('refuses Filled and Draft without a request', async () => {
      const { jobs } = setup();
      expect((await rejection(firstValueFrom(jobs.changeStatus(3, 'Filled')))).code).toBe(
        'Conflict',
      );
      expect((await rejection(firstValueFrom(jobs.changeStatus(3, 'Draft')))).code).toBe(
        'Conflict',
      );
    });

    it('deletes with the rowVersion in the body', async () => {
      const { http, jobs } = setup();
      await seed(http, jobs, '/api/job-openings', [opening]);
      const promise = firstValueFrom(jobs.delete(3));
      await tick();
      const req = http.expectOne('/api/job-openings/3');
      expect(req.request.method).toBe('DELETE');
      expect(req.request.body).toEqual({ rowVersion: 'JOBV1' });
      req.flush(null);
      await promise;
    });

    it('maps a 409 to the reload message', async () => {
      const { http, jobs } = setup();
      await seed(http, jobs, '/api/job-openings', [opening]);
      const promise = rejection(firstValueFrom(jobs.changeStatus(3, 'Closed')));
      await tick();
      http
        .expectOne('/api/job-openings/3/close')
        .flush({ code: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      const error = await promise;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('The record changed. Reload and try again.');
    });
  });

  describe('candidates', () => {
    it('creates with the lower-case email and null for blank optional text', async () => {
      const { http, candidates } = setup();
      const promise = firstValueFrom(
        candidates.create({
          firstName: 'Ada',
          lastName: 'Sample',
          email: ' Ada@Example.Test ',
          phone: '+1 555 0100',
          source: 'Referral',
          currentEmployer: '',
          experienceYears: 3.5,
          resumeName: null,
          notes: '',
        }),
      );
      const req = http.expectOne('/api/candidates');
      expect(req.request.body).toEqual({
        firstName: 'Ada',
        lastName: 'Sample',
        email: 'ada@example.test',
        phone: '+1 555 0100',
        source: 'Referral',
        currentEmployer: null,
        experienceYears: 3.5,
        resumeName: null,
        notes: null,
      });
      req.flush(candidate);
      await promise;
    });

    it('updates with a full PUT and the rowVersion', async () => {
      const { http, candidates } = setup();
      await seed(http, candidates, '/api/candidates', [candidate]);
      const promise = firstValueFrom(candidates.update(7, { phone: '+1 555 0111' }));
      await tick();
      const req = http.expectOne('/api/candidates/7');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toMatchObject({
        firstName: 'Ada',
        phone: '+1 555 0111',
        currentEmployer: null,
        rowVersion: 'CANV1',
      });
      req.flush({ ...candidate, phone: '+1 555 0111', rowVersion: 'CANV2' });
      await promise;
    });

    it('maps a 400 field error to the form control name', async () => {
      const { http, candidates } = setup();
      const promise = rejection(
        firstValueFrom(
          candidates.create({
            firstName: 'Ada',
            lastName: 'Sample',
            email: 'dup@example.test',
            phone: '1',
            source: 'Referral',
            currentEmployer: '',
            experienceYears: 1,
            resumeName: null,
            notes: '',
          }),
        ),
      );
      http
        .expectOne('/api/candidates')
        .flush(
          { detail: 'Fix the email.', code: 'Validation', errors: { Email: ['Already used.'] } },
          { status: 400, statusText: 'Bad Request' },
        );
      const error = await promise;
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors).toEqual({ email: 'Already used.' });
    });
  });

  describe('applications', () => {
    it('creates with the opening, candidate and notes only', async () => {
      const { http, applications } = setup();
      const promise = firstValueFrom(
        applications.create({
          jobOpeningId: 3,
          candidateId: 7,
          stage: 'Applied',
          appliedOn: '2026-11-02',
          rejectionReason: null,
          notes: '',
        }),
      );
      const req = http.expectOne('/api/applications');
      expect(req.request.body).toEqual({ jobOpeningId: 3, candidateId: 7, notes: null });
      req.flush(application);
      await promise;
    });

    it('moves a stage with { rowVersion, stage } and no reason', async () => {
      const { http, applications } = setup();
      await seed(http, applications, '/api/applications', [application]);
      const promise = firstValueFrom(applications.moveStage(11, 'Screening'));
      await tick();
      const req = http.expectOne('/api/applications/11/stage');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'APPV1', stage: 'Screening' });
      req.flush({ ...application, stage: 'Screening', rowVersion: 'APPV2' });
      expect((await promise).stage).toBe('Screening');
    });

    it('rejects with { rowVersion, stage, reason } and trims the reason', async () => {
      const { http, applications } = setup();
      await seed(http, applications, '/api/applications', [application]);
      const promise = firstValueFrom(applications.moveStage(11, 'Rejected', '  Not a fit  '));
      await tick();
      const req = http.expectOne('/api/applications/11/stage');
      expect(req.request.body).toEqual({
        rowVersion: 'APPV1',
        stage: 'Rejected',
        reason: 'Not a fit',
      });
      req.flush({ ...application, stage: 'Rejected', rejectionReason: 'Not a fit' });
      await promise;
    });

    it('refuses a blank rejection reason before any request', async () => {
      const { applications } = setup();
      const error = await rejection(firstValueFrom(applications.moveStage(11, 'Rejected', '   ')));
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors).toEqual({ reason: 'A reason is required.' });
      // No request was made: afterEach verify() would fail on an open request.
    });

    it('maps a 400 on reason to a field error', async () => {
      const { http, applications } = setup();
      await seed(http, applications, '/api/applications', [application]);
      const promise = rejection(firstValueFrom(applications.moveStage(11, 'Rejected', 'x')));
      await tick();
      http
        .expectOne('/api/applications/11/stage')
        .flush(
          { code: 'Validation', errors: { reason: ['Use at most 200 characters.'] } },
          { status: 400, statusText: 'Bad Request' },
        );
      const error = await promise;
      expect(error.fieldErrors).toEqual({ reason: 'Use at most 200 characters.' });
    });

    it('updates only the notes (opening and candidate as stored)', async () => {
      const { http, applications } = setup();
      await seed(http, applications, '/api/applications', [application]);
      const promise = firstValueFrom(applications.update(11, { notes: 'Strong CV' }));
      await tick();
      const req = http.expectOne('/api/applications/11');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        jobOpeningId: 3,
        candidateId: 7,
        notes: 'Strong CV',
        rowVersion: 'APPV1',
      });
      req.flush({ ...application, notes: 'Strong CV', rowVersion: 'APPV2' });
      await promise;
    });
  });

  describe('interviews', () => {
    it('creates a scheduled interview without status or outcome fields', async () => {
      const { http, interviews } = setup();
      const promise = firstValueFrom(
        interviews.create({
          applicationId: 11,
          round: 'Technical',
          date: '2026-11-20',
          startTime: '10:00:00',
          durationMinutes: 45,
          mode: 'Video',
          interviewerId: 5,
          location: '',
          status: 'Scheduled',
          score: null,
          recommendation: null,
          feedback: '',
        }),
      );
      const req = http.expectOne('/api/interviews');
      expect(req.request.body).toEqual({
        applicationId: 11,
        round: 'Technical',
        date: '2026-11-20',
        startTime: '10:00',
        durationMinutes: 45,
        mode: 'Video',
        interviewerId: 5,
        location: null,
      });
      req.flush(interview);
      expect((await promise).startTime).toBe('10:00');
    });

    it('creates a completed interview with the outcome in the same request', async () => {
      const { http, interviews } = setup();
      const promise = firstValueFrom(
        interviews.create({
          applicationId: 11,
          round: 'HR',
          date: '2026-11-05',
          startTime: '09:30',
          durationMinutes: 30,
          mode: 'Phone',
          interviewerId: 5,
          location: '',
          status: 'Completed',
          score: '4' as unknown as number,
          recommendation: 'Hire',
          feedback: 'Clear answers',
        }),
      );
      const req = http.expectOne('/api/interviews');
      expect(req.request.body).toMatchObject({
        status: 'Completed',
        score: 4,
        recommendation: 'Hire',
        feedback: 'Clear answers',
      });
      req.flush({ ...interview, status: 'Completed', score: 4, recommendation: 'Hire' });
      await promise;
    });

    it('cancels through the cancel command, never PUT', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [interview]);
      const promise = firstValueFrom(interviews.update(21, { status: 'Cancelled' }));
      await tick();
      const req = http.expectOne('/api/interviews/21/cancel');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'INTV1' });
      req.flush({ ...interview, status: 'Cancelled', rowVersion: 'INTV2' });
      expect((await promise).status).toBe('Cancelled');
    });

    it('records an outcome through the outcome command', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [interview]);
      const promise = firstValueFrom(
        interviews.update(21, { status: 'Completed', score: 5, recommendation: 'Strong hire' }),
      );
      await tick();
      const req = http.expectOne('/api/interviews/21/outcome');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        rowVersion: 'INTV1',
        score: 5,
        recommendation: 'Strong hire',
        feedback: null,
      });
      req.flush({ ...interview, status: 'Completed', score: 5, recommendation: 'Strong hire' });
      await promise;
    });

    it('an edit form save reschedules by PUT (no status) and then completes with the new rowVersion', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [interview]);
      const promise = firstValueFrom(
        interviews.update(21, {
          applicationId: 11,
          round: 'Technical',
          date: '2026-11-21',
          startTime: '10:00',
          durationMinutes: 45,
          mode: 'Video',
          interviewerId: 5,
          location: '',
          status: 'Completed',
          score: 4,
          recommendation: 'Hire',
          feedback: '',
        }),
      );
      await tick();
      const put = http.expectOne('/api/interviews/21');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body.status).toBeUndefined();
      expect(put.request.body.score).toBeUndefined();
      expect(put.request.body).toMatchObject({ date: '2026-11-21', rowVersion: 'INTV1' });
      put.flush({ ...interview, date: '2026-11-21', rowVersion: 'INTV2' });
      await tick();
      const outcome = http.expectOne('/api/interviews/21/outcome');
      expect(outcome.request.body.rowVersion).toBe('INTV2');
      outcome.flush({ ...interview, status: 'Completed', score: 4, recommendation: 'Hire' });
      await promise;
    });

    it('sends no PUT when the form changed nothing in the schedule', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [interview]);
      const promise = firstValueFrom(
        interviews.update(21, {
          round: 'Technical',
          date: '2026-11-20',
          startTime: '10:00',
          durationMinutes: 45,
          status: 'Scheduled',
        }),
      );
      expect((await promise).id).toBe(21);
    });

    it('refuses a change to a cancelled interview and a cancel of a completed one locally', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [
        { ...interview, status: 'Cancelled' },
        { ...interview, id: 22, status: 'Completed', score: 4, recommendation: 'Hire' },
      ]);
      const cancelled = await rejection(firstValueFrom(interviews.update(21, { round: 'HR' })));
      expect(cancelled.code).toBe('Conflict');
      const completed = await rejection(
        firstValueFrom(interviews.update(22, { status: 'Cancelled' })),
      );
      expect(completed.code).toBe('Conflict');
    });

    it('does not delete a completed interview locally; the API refuses with 409', async () => {
      const { http, interviews } = setup();
      await seed(http, interviews, '/api/interviews', [{ ...interview, status: 'Completed' }]);
      const promise = rejection(firstValueFrom(interviews.delete(21)));
      await tick();
      const req = http.expectOne('/api/interviews/21');
      expect(req.request.body).toEqual({ rowVersion: 'INTV1' });
      req.flush(
        { detail: 'A completed interview cannot be deleted.', code: 'Conflict' },
        { status: 409, statusText: 'Conflict' },
      );
      expect((await promise).message).toBe('A completed interview cannot be deleted.');
    });
  });

  describe('views', () => {
    it('reads the pipeline and the counts', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const views = TestBed.inject(RecruitmentViewsHttpService);
      const board = firstValueFrom(views.pipeline(3));
      const req = http.expectOne((r) => r.url === '/api/recruitment/pipeline');
      expect(req.request.params.get('jobOpeningId')).toBe('3');
      req.flush({
        jobOpeningId: 3,
        columns: ['Applied', 'Screening', 'Interview', 'Selected', 'Rejected'].map((stage) => ({
          stage,
          count: 0,
          cards: [],
        })),
      });
      expect((await board).columns.map((c) => c.stage)).toEqual([
        'Applied',
        'Screening',
        'Interview',
        'Selected',
        'Rejected',
      ]);

      const all = firstValueFrom(views.pipeline());
      const allReq = http.expectOne((r) => r.url === '/api/recruitment/pipeline');
      expect(allReq.request.params.has('jobOpeningId')).toBe(false);
      allReq.flush({ jobOpeningId: null, columns: [] });
      await all;

      const counts = firstValueFrom(views.counts());
      http.expectOne('/api/recruitment/counts').flush({
        jobOpenings: [{ jobOpeningId: 3, applications: 2, selected: 1 }],
        candidates: [{ candidateId: 7, applications: 1 }],
      });
      expect((await counts).jobOpenings[0].selected).toBe(1);
    });

    it('maps a failed view call to an AppError', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = rejection(
        firstValueFrom(TestBed.inject(RecruitmentViewsHttpService).counts()),
      );
      http
        .expectOne('/api/recruitment/counts')
        .flush(null, { status: 403, statusText: 'Forbidden' });
      expect((await promise).code).toBe('Forbidden');
    });
  });
});
