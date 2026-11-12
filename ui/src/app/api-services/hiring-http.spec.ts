import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../core/models/common.models';
import { BlacklistDataService } from '../core/services/data/blacklist-data.service';
import { EmployeeLookupDataService } from '../core/services/data/employee-data.service';
import { MyPayslipDataService } from '../core/services/data/payroll-data.services';
import {
  ApplicationDataService,
  CandidateDataService,
} from '../core/services/data/recruitment-data.services';
import { provideMockDataServices } from '../mock-services/provide-mock-data-services';
import { BlacklistHttpService } from './employees/blacklist-http.service';
import { EmployeeLookupHttpService } from './employees/employee-lookup-http.service';
import { MyPayslipHttpService } from './payroll/my-payslip-http.service';
import { provideApiDataServices } from './provide-api-data-services';
import { ApplicationHttpService } from './recruitment/application-http.service';
import { CandidateHttpService } from './recruitment/candidate-http.service';

const entry = {
  id: 4,
  employeeId: 9,
  employeeCode: 'EMP009',
  employeeName: 'Sample Person',
  reason: 'Sample reason',
  blacklistedOn: '2026-11-01',
  blacklistedBy: 'Demo Admin',
  liftedOn: null,
  liftedBy: null,
  liftReason: null,
  rowVersion: 'BLV1',
};

const candidateRow = {
  id: 7,
  firstName: 'Ada',
  lastName: 'Sample',
  email: 'ada@example.test',
  phone: '09111222333',
  source: 'Referral',
  currentEmployer: null,
  experienceYears: 3,
  resumeName: null,
  notes: null,
  blacklistMatch: true,
  rowVersion: 'CANV1',
};

const applicationRow = {
  id: 11,
  jobOpeningId: 3,
  candidateId: 7,
  stage: 'Interview',
  appliedOn: '2026-11-02',
  rejectionReason: null,
  notes: null,
  blacklistMatch: true,
  rowVersion: 'APPV1',
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
    blacklist: TestBed.inject(BlacklistDataService),
    lookup: TestBed.inject(EmployeeLookupDataService),
    mine: TestBed.inject(MyPayslipDataService),
    candidates: TestBed.inject(CandidateDataService),
    applications: TestBed.inject(ApplicationDataService),
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

describe('Hiring HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('are bound only in API mode', () => {
    setup();
    expect(TestBed.inject(BlacklistDataService)).toBeInstanceOf(BlacklistHttpService);
    expect(TestBed.inject(EmployeeLookupDataService)).toBeInstanceOf(EmployeeLookupHttpService);
    expect(TestBed.inject(MyPayslipDataService)).toBeInstanceOf(MyPayslipHttpService);
    expect(TestBed.inject(CandidateDataService)).toBeInstanceOf(CandidateHttpService);
    expect(TestBed.inject(ApplicationDataService)).toBeInstanceOf(ApplicationHttpService);
  });

  describe('do-not-rehire list', () => {
    it('queries with paging, known sort keys, search and the active filter', async () => {
      const { http, blacklist } = setup();
      const promise = firstValueFrom(
        blacklist.query({
          pageIndex: 1,
          pageSize: 20,
          sort: { active: 'blacklistedOn', direction: 'desc' },
          search: ' emp009 ',
          filters: { active: 'true' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/employee-blacklist/query');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('20');
      expect(req.request.params.get('sort')).toBe('blacklistedOn:desc');
      expect(req.request.params.get('search')).toBe('emp009');
      expect(req.request.params.get('active')).toBe('true');
      req.flush({ items: [entry], total: 1, pageIndex: 1, pageSize: 20 });
      expect((await promise).items[0].reason).toBe('Sample reason');
    });

    it('drops unknown sort keys and sends no active parameter for "all"', async () => {
      const { http, blacklist } = setup();
      const promise = firstValueFrom(
        blacklist.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'employeeName', direction: 'asc' },
          filters: {},
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/employee-blacklist/query');
      expect(req.request.params.has('sort')).toBe(false);
      expect(req.request.params.has('active')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await promise;
    });

    it('reads the active entry of an employee and treats 404 as "not listed"', async () => {
      const { http, blacklist } = setup();
      const found = firstValueFrom(blacklist.getActiveForEmployee(9));
      http.expectOne('/api/employees/9/blacklist').flush(entry);
      expect((await found)?.id).toBe(4);

      const none = firstValueFrom(blacklist.getActiveForEmployee(10));
      http
        .expectOne('/api/employees/10/blacklist')
        .flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
      expect(await none).toBeNull();
    });

    it('does not hide other errors when reading the active entry', async () => {
      const { http, blacklist } = setup();
      const failed = rejection(firstValueFrom(blacklist.getActiveForEmployee(9)));
      http
        .expectOne('/api/employees/9/blacklist')
        .flush({ title: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
      expect((await failed).code).toBe('Forbidden');
    });

    it('adds with { employeeId, reason } and trims the reason', async () => {
      const { http, blacklist } = setup();
      const promise = firstValueFrom(blacklist.add(9, '  Sample reason  '));
      const req = http.expectOne('/api/employee-blacklist');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ employeeId: 9, reason: 'Sample reason' });
      req.flush(entry, { status: 201, statusText: 'Created' });
      expect((await promise).employeeCode).toBe('EMP009');
    });

    it('refuses a blank or over-long reason before any request', async () => {
      const { http, blacklist } = setup();
      const blank = await rejection(firstValueFrom(blacklist.add(9, '   ')));
      expect(blank.code).toBe('Validation');
      expect(blank.fieldErrors?.['reason']).toBeTruthy();
      const long = await rejection(firstValueFrom(blacklist.lift(4, 'x'.repeat(501))));
      expect(long.code).toBe('Validation');
      http.expectNone(() => true);
    });

    it('shows the API conflict messages as they are', async () => {
      const { http, blacklist } = setup();
      const failed = rejection(firstValueFrom(blacklist.add(9, 'Sample')));
      http.expectOne('/api/employee-blacklist').flush(
        {
          title: 'Conflict',
          detail: 'Only a former employee can be put on the do-not-rehire list.',
        },
        { status: 409, statusText: 'Conflict' },
      );
      const error = await failed;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('Only a former employee can be put on the do-not-rehire list.');
    });

    it('lifts with the rowVersion last seen for the entry', async () => {
      const { http, blacklist } = setup();
      const seen = firstValueFrom(blacklist.query({ pageIndex: 0, pageSize: 10 }));
      http
        .expectOne((r) => r.url === '/api/employee-blacklist/query')
        .flush({ items: [entry], total: 1, pageIndex: 0, pageSize: 10 });
      await seen;

      const promise = firstValueFrom(blacklist.lift(4, ' Cleared after review '));
      const req = http.expectOne('/api/employee-blacklist/4/lift');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'BLV1', reason: 'Cleared after review' });
      req.flush({ ...entry, liftedOn: '2026-11-10', rowVersion: 'BLV2' });
      expect((await promise).liftedOn).toBe('2026-11-10');

      // The new version is remembered for the next write.
      const again = rejection(firstValueFrom(blacklist.lift(4, 'Again')));
      const second = http.expectOne('/api/employee-blacklist/4/lift');
      expect(second.request.body.rowVersion).toBe('BLV2');
      second.flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      expect((await again).code).toBe('Conflict');
    });

    it('prefers an explicit rowVersion and reads the entry first when none is known', async () => {
      const { http, blacklist } = setup();
      const explicit = firstValueFrom(blacklist.lift(4, 'Reason', 'EXPLICIT'));
      const req = http.expectOne('/api/employee-blacklist/4/lift');
      expect(req.request.body.rowVersion).toBe('EXPLICIT');
      req.flush({ ...entry, liftedOn: '2026-11-10' });
      await explicit;

      const unknown = firstValueFrom(blacklist.lift(5, 'Reason'));
      http.expectOne('/api/employee-blacklist/5').flush({ ...entry, id: 5, rowVersion: 'BLV9' });
      await tick();
      const lift = http.expectOne('/api/employee-blacklist/5/lift');
      expect(lift.request.body.rowVersion).toBe('BLV9');
      lift.flush({ ...entry, id: 5, liftedOn: '2026-11-10' });
      await unknown;
    });
  });

  describe('employee lookup', () => {
    it('sends search, page index and page size, capping the size at 200', async () => {
      const { http, lookup } = setup();
      const promise = firstValueFrom(
        lookup.query({ search: ' aye ', pageIndex: 2, pageSize: 999 }),
      );
      const req = http.expectOne((r) => r.url === '/api/employees/lookup');
      expect(req.request.params.get('search')).toBe('aye');
      expect(req.request.params.get('pageIndex')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('200');
      req.flush({
        items: [
          {
            id: 1,
            employeeCode: 'EMP001',
            displayName: 'Aye',
            jobTitle: 'QA',
            departmentName: 'IT',
          },
        ],
        total: 1,
        pageIndex: 2,
        pageSize: 200,
      });
      expect((await promise).items[0].displayName).toBe('Aye');
    });
  });

  describe('my payslips', () => {
    it('lists with paging only and reads one by id, never sending an employee id', async () => {
      const { http, mine } = setup();
      const payslip = {
        id: 3,
        runId: 1,
        periodName: 'October 2026',
        earnings: null,
        deductions: null,
      };
      const page = firstValueFrom(mine.query({ pageIndex: 0, pageSize: 12 }));
      const req = http.expectOne((r) => r.url === '/api/me/payslips');
      expect(req.request.params.get('pageIndex')).toBe('0');
      expect(req.request.params.get('pageSize')).toBe('12');
      expect(req.request.params.keys().sort()).toEqual(['pageIndex', 'pageSize']);
      req.flush({ items: [payslip], total: 1, pageIndex: 0, pageSize: 12 });
      const result = await page;
      expect(result.items[0].earnings).toEqual([]);

      const one = firstValueFrom(mine.getById(3));
      http.expectOne('/api/me/payslips/3').flush(payslip);
      expect((await one).periodName).toBe('October 2026');
    });

    it('maps 404 to NotFound', async () => {
      const { http, mine } = setup();
      const failed = rejection(firstValueFrom(mine.getById(99)));
      http
        .expectOne('/api/me/payslips/99')
        .flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
      expect((await failed).code).toBe('NotFound');
    });
  });

  describe('candidates', () => {
    it('never returns the national id and reads the match flag', async () => {
      const { http, candidates } = setup();
      const promise = firstValueFrom(candidates.getById(7));
      http.expectOne('/api/candidates/7').flush({ ...candidateRow, nationalId: 'LEAK' });
      const row = await promise;
      expect(row.blacklistMatch).toBe(true);
      expect('nationalId' in row).toBe(false);
    });

    it('sends the national id on create, trimmed, and omits it when not supplied', async () => {
      const { http, candidates } = setup();
      const base = {
        firstName: 'Ada',
        lastName: 'Sample',
        email: 'Ada@Example.test',
        phone: '09111222333',
        source: 'Referral',
        currentEmployer: '',
        experienceYears: 3,
        resumeName: null,
        notes: '',
      } as Parameters<CandidateDataService['create']>[0];

      const withId = firstValueFrom(
        candidates.create({ ...base, nationalId: '  12/ABC(N)123456 ' }),
      );
      const req = http.expectOne('/api/candidates');
      expect(req.request.body.nationalId).toBe('12/ABC(N)123456');
      req.flush(candidateRow);
      await withId;

      const without = firstValueFrom(candidates.create(base));
      const plain = http.expectOne('/api/candidates');
      expect('nationalId' in plain.request.body).toBe(false);
      plain.flush(candidateRow);
      await without;
    });

    it('omits the national id on update unless one is typed (a blank field keeps it)', async () => {
      const { http, candidates } = setup();
      const seen = firstValueFrom(candidates.getById(7));
      http.expectOne('/api/candidates/7').flush(candidateRow);
      await seen;

      const keep = firstValueFrom(candidates.update(7, { lastName: 'Renamed' }));
      const keepReq = http.expectOne('/api/candidates/7');
      expect(keepReq.request.method).toBe('PUT');
      expect('nationalId' in keepReq.request.body).toBe(false);
      expect(keepReq.request.body.rowVersion).toBe('CANV1');
      keepReq.flush({ ...candidateRow, lastName: 'Renamed' });
      await keep;

      const blank = firstValueFrom(candidates.update(7, { nationalId: '   ' }));
      const blankReq = http.expectOne('/api/candidates/7');
      expect('nationalId' in blankReq.request.body).toBe(false);
      blankReq.flush(candidateRow);
      await blank;
    });
  });

  describe('applications', () => {
    it('reads the match flag', async () => {
      const { http, applications } = setup();
      const promise = firstValueFrom(applications.getById(11));
      http.expectOne('/api/applications/11').flush(applicationRow);
      expect((await promise).blacklistMatch).toBe(true);
    });

    it('moves to Selected without an override unless one is given', async () => {
      const { http, applications } = setup();
      const seen = firstValueFrom(applications.getById(11));
      http.expectOne('/api/applications/11').flush(applicationRow);
      await seen;

      const plain = rejection(firstValueFrom(applications.moveStage(11, 'Selected')));
      const first = http.expectOne('/api/applications/11/stage');
      expect(first.request.body).toEqual({ rowVersion: 'APPV1', stage: 'Selected' });
      first.flush(
        {
          title: 'Conflict',
          detail:
            'This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason.',
        },
        { status: 409, statusText: 'Conflict' },
      );
      const error = await plain;
      expect(error.code).toBe('Conflict');
      expect(error.message).toContain('do-not-rehire');

      const approved = firstValueFrom(
        applications.moveStage(11, 'Selected', '', '  Approved after review  '),
      );
      const second = http.expectOne('/api/applications/11/stage');
      expect(second.request.body).toEqual({
        rowVersion: 'APPV1',
        stage: 'Selected',
        blacklistOverrideReason: 'Approved after review',
      });
      second.flush({ ...applicationRow, stage: 'Selected', rowVersion: 'APPV2' });
      expect((await approved).stage).toBe('Selected');
    });

    it('never sends an override for other stages, and shows the 403 message', async () => {
      const { http, applications } = setup();
      const seen = firstValueFrom(applications.getById(11));
      http.expectOne('/api/applications/11').flush(applicationRow);
      await seen;

      const reject = firstValueFrom(applications.moveStage(11, 'Rejected', 'No fit', 'ignored'));
      const req = http.expectOne('/api/applications/11/stage');
      expect(req.request.body).toEqual({
        rowVersion: 'APPV1',
        stage: 'Rejected',
        reason: 'No fit',
      });
      req.flush({ ...applicationRow, stage: 'Rejected', rowVersion: 'APPV2' });
      await reject;

      const forbidden = rejection(
        firstValueFrom(applications.moveStage(11, 'Selected', '', 'Trying')),
      );
      http.expectOne('/api/applications/11/stage').flush(
        {
          title: 'Forbidden',
          detail:
            'Only an HR Administrator can approve the hire of a candidate who matches the do-not-rehire list.',
        },
        { status: 403, statusText: 'Forbidden' },
      );
      const error = await forbidden;
      expect(error.code).toBe('Forbidden');
      expect(error.message).toContain('Only an HR Administrator');
    });

    it('maps an out-of-scope record (404) to NotFound', async () => {
      const { http, applications } = setup();
      const failed = rejection(firstValueFrom(applications.getById(404)));
      http
        .expectOne('/api/applications/404')
        .flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
      expect((await failed).code).toBe('NotFound');
    });
  });
});
