import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../../core/services/data/leave-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { LeaveApplicationHttpService } from './leave-application-http.service';
import { LeaveBalanceHttpService } from './leave-balance-http.service';
import { LeaveCalendarHttpService } from './leave-calendar-http.service';
import { LeavePolicyHttpService, LeaveTypeHttpService } from './leave-http.services';

const APPS = '/api/leave-applications';

const application = {
  id: 4,
  applicationNo: 'LV-2026-0004',
  employeeId: 5,
  leaveTypeId: 2,
  startDate: '2026-12-01',
  endDate: '2026-12-02',
  days: 2,
  halfDay: false,
  reason: 'Family event',
  documentName: null,
  status: 'Draft',
  appliedOn: '2026-11-01',
  approverId: null,
  actionedByName: null,
  actionedOn: null,
  approverRemarks: null,
  rowVersion: 'AAAAV1',
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
  return { http, apps: TestBed.inject(LeaveApplicationDataService) };
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

/** Loads application 4 through a query so its rowVersion is known. */
async function seed(http: HttpTestingController, apps: LeaveApplicationDataService) {
  const promise = firstValueFrom(apps.query({ pageIndex: 0, pageSize: 10 }));
  http
    .expectOne((r) => r.url === `${APPS}/query`)
    .flush({ items: [application], total: 1, pageIndex: 0, pageSize: 10 });
  await promise;
}

describe('Leave HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mock leave services only when API mode is on', () => {
    setup();
    expect(TestBed.inject(LeaveTypeDataService)).toBeInstanceOf(LeaveTypeHttpService);
    expect(TestBed.inject(LeavePolicyDataService)).toBeInstanceOf(LeavePolicyHttpService);
    expect(TestBed.inject(LeaveApplicationDataService)).toBeInstanceOf(LeaveApplicationHttpService);
    expect(TestBed.inject(LeaveBalanceDataService)).toBeInstanceOf(LeaveBalanceHttpService);
    expect(TestBed.inject(LeaveCalendarDataService)).toBeInstanceOf(LeaveCalendarHttpService);
  });

  describe('leave types and policies', () => {
    it('queries types with paging, sort, search and the status filter', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = firstValueFrom(
        TestBed.inject(LeaveTypeDataService).query({
          pageIndex: 1,
          pageSize: 20,
          sort: { active: 'name', direction: 'asc' },
          search: ' annual ',
          filters: { status: 'Active' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/leave-types/query');
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('20');
      expect(req.request.params.get('sort')).toBe('name:asc');
      expect(req.request.params.get('search')).toBe('annual');
      expect(req.request.params.get('status')).toBe('Active');
      req.flush({ items: [], total: 0, pageIndex: 1, pageSize: 20 });
      await promise;
    });

    it('drops a policy sort key the API cannot sort by', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = firstValueFrom(
        TestBed.inject(LeavePolicyDataService).query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'accrual', direction: 'asc' },
          filters: { leaveTypeId: 2, status: 'all' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/leave-policies/query');
      expect(req.request.params.has('sort')).toBe(false);
      expect(req.request.params.get('leaveTypeId')).toBe('2');
      expect(req.request.params.has('status')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await promise;
    });

    it('changes type status with activate/deactivate and sends rowVersion on PUT and DELETE', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const types = TestBed.inject(LeaveTypeDataService);
      const dto = { id: 2, code: 'AL', name: 'Annual', status: 'Active', rowVersion: 'T1' };

      const list = firstValueFrom(types.getAll());
      http.expectOne('/api/leave-types').flush([dto]);
      await list;

      const off = firstValueFrom(types.update(2, { status: 'Inactive' }));
      const deactivate = http.expectOne('/api/leave-types/2/deactivate');
      expect(deactivate.request.method).toBe('POST');
      expect(deactivate.request.body).toEqual({ rowVersion: 'T1' });
      deactivate.flush({ ...dto, status: 'Inactive', rowVersion: 'T2' });
      await off;

      const on = firstValueFrom(types.update(2, { status: 'Active' }));
      const activate = http.expectOne('/api/leave-types/2/activate');
      expect(activate.request.body).toEqual({ rowVersion: 'T2' });
      activate.flush({ ...dto, rowVersion: 'T3' });
      await on;

      const put = firstValueFrom(types.update(2, { name: 'Annual leave' }));
      const putReq = http.expectOne('/api/leave-types/2');
      expect(putReq.request.method).toBe('PUT');
      expect(putReq.request.body).toEqual({ name: 'Annual leave', rowVersion: 'T3' });
      putReq.flush({ ...dto, name: 'Annual leave', rowVersion: 'T4' });
      await put;

      const del = firstValueFrom(types.delete(2));
      const delReq = http.expectOne('/api/leave-types/2');
      expect(delReq.request.method).toBe('DELETE');
      expect(delReq.request.body).toEqual({ rowVersion: 'T4' });
      delReq.flush(null);
      await del;
    });

    it('changes policy status with activate/deactivate', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = firstValueFrom(
        TestBed.inject(LeavePolicyDataService).update(3, {
          status: 'Inactive',
          rowVersion: 'P1',
        } as never),
      );
      const req = http.expectOne('/api/leave-policies/3/deactivate');
      expect(req.request.body).toEqual({ rowVersion: 'P1' });
      req.flush({ id: 3, status: 'Inactive', rowVersion: 'P2' });
      await promise;
    });

    it('maps a 409 to the reload message', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = rejection(
        firstValueFrom(
          TestBed.inject(LeaveTypeDataService).update(2, {
            status: 'Inactive',
            rowVersion: 'OLD',
          } as never),
        ),
      );
      http
        .expectOne('/api/leave-types/2/deactivate')
        .flush({ code: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      const error = await promise;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('The record changed. Reload and try again.');
    });
  });

  describe('applications', () => {
    it('builds the query with filters and sort, and never calls an unpaged endpoint', async () => {
      const { http, apps } = setup();
      const promise = firstValueFrom(
        apps.query({
          pageIndex: 0,
          pageSize: 25,
          sort: { active: 'startDate', direction: 'desc' },
          search: 'LV',
          filters: {
            employeeId: 5,
            leaveTypeId: 2,
            status: 'Submitted',
            dateFrom: '2026-12-01',
            dateTo: '2026-12-31',
          },
        }),
      );
      const req = http.expectOne((r) => r.url === `${APPS}/query`);
      const p = req.request.params;
      expect(p.get('sort')).toBe('startDate:desc');
      expect(p.get('search')).toBe('LV');
      expect(p.get('employeeId')).toBe('5');
      expect(p.get('leaveTypeId')).toBe('2');
      expect(p.get('status')).toBe('Submitted');
      expect(p.get('dateFrom')).toBe('2026-12-01');
      expect(p.get('dateTo')).toBe('2026-12-31');
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 25 });
      await promise;
      http.expectNone(APPS);
    });

    it('getAll pages /query at 200 per page until done', async () => {
      const { http, apps } = setup();
      const promise = firstValueFrom(apps.getAll());
      const first = http.expectOne((r) => r.url === `${APPS}/query`);
      expect(first.request.params.get('pageIndex')).toBe('0');
      expect(first.request.params.get('pageSize')).toBe('200');
      first.flush({ items: [application], total: 2, pageIndex: 0, pageSize: 200 });
      await tick();
      const second = http.expectOne((r) => r.url === `${APPS}/query`);
      expect(second.request.params.get('pageIndex')).toBe('1');
      second.flush({
        items: [{ ...application, id: 5 }],
        total: 2,
        pageIndex: 1,
        pageSize: 200,
      });
      expect((await promise).map((a) => a.id)).toEqual([4, 5]);
      http.expectNone(APPS);
    });

    it('merges a multi-status list (decision history) from one query per status', async () => {
      const { http, apps } = setup();
      const promise = firstValueFrom(
        apps.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'id', direction: 'asc' },
          filters: { status: ['Approved', 'Rejected'] },
        }),
      );
      const reqs = http.match((r) => r.url === `${APPS}/query`);
      expect(reqs.map((r) => r.request.params.get('status')).sort()).toEqual([
        'Approved',
        'Rejected',
      ]);
      for (const r of reqs) {
        const id = r.request.params.get('status') === 'Approved' ? 9 : 3;
        r.flush({ items: [{ ...application, id }], total: 1, pageIndex: 0, pageSize: 200 });
      }
      const page = await promise;
      expect(page.items.map((a) => a.id)).toEqual([3, 9]);
      expect(page.total).toBe(2);
    });

    it('create posts only the create fields', async () => {
      const { http, apps } = setup();
      const promise = firstValueFrom(
        apps.create({
          ...application,
          documentName: '',
        } as never),
      );
      const req = http.expectOne(APPS);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        employeeId: 5,
        leaveTypeId: 2,
        startDate: '2026-12-01',
        endDate: '2026-12-02',
        halfDay: false,
        reason: 'Family event',
        documentName: null,
      });
      for (const key of ['days', 'applicationNo', 'status', 'rowVersion', 'appliedOn']) {
        expect(key in req.request.body).toBe(false);
      }
      req.flush(application);
      await promise;
    });

    it('PUT and DELETE send the rowVersion', async () => {
      const { http, apps } = setup();
      await seed(http, apps);

      const put = firstValueFrom(apps.update(4, { reason: 'Changed', status: 'Draft' } as never));
      const putReq = http.expectOne(`${APPS}/4`);
      // The partial change is completed from the current record before the PUT.
      expect(putReq.request.method).toBe('GET');
      putReq.flush(application);
      await tick();
      const putBody = http.expectOne(`${APPS}/4`);
      expect(putBody.request.method).toBe('PUT');
      expect(putBody.request.body).toEqual({
        employeeId: 5,
        leaveTypeId: 2,
        startDate: '2026-12-01',
        endDate: '2026-12-02',
        halfDay: false,
        reason: 'Changed',
        documentName: null,
        rowVersion: 'AAAAV1',
      });
      putBody.flush({ ...application, reason: 'Changed', rowVersion: 'AAAAV2' });
      await put;

      const del = firstValueFrom(apps.delete(4));
      const delReq = http.expectOne(`${APPS}/4`);
      expect(delReq.request.method).toBe('DELETE');
      expect(delReq.request.body).toEqual({ rowVersion: 'AAAAV2' });
      delReq.flush(null);
      await del;
    });

    it('each command sends rowVersion and remarks', async () => {
      const { http, apps } = setup();
      await seed(http, apps);

      const submit = firstValueFrom(apps.submit(4));
      const submitReq = http.expectOne(`${APPS}/4/submit`);
      expect(submitReq.request.body).toEqual({ rowVersion: 'AAAAV1' });
      submitReq.flush({ ...application, status: 'Submitted', rowVersion: 'V2' });
      await submit;

      const approve = firstValueFrom(apps.decide(4, 'Approved', ' Enjoy '));
      const approveReq = http.expectOne(`${APPS}/4/approve`);
      expect(approveReq.request.body).toEqual({ rowVersion: 'V2', remarks: 'Enjoy' });
      approveReq.flush({ ...application, status: 'Approved', rowVersion: 'V3' });
      await approve;

      const cancel = firstValueFrom(apps.cancel(4, 'Plans changed'));
      const cancelReq = http.expectOne(`${APPS}/4/cancel`);
      expect(cancelReq.request.body).toEqual({ rowVersion: 'V3', remarks: 'Plans changed' });
      cancelReq.flush({ ...application, status: 'Cancelled', rowVersion: 'V4' });
      await cancel;
    });

    it('reject sends the remark, and a blank remark is refused before any request', async () => {
      const { http, apps } = setup();
      await seed(http, apps);

      const error = await rejection(firstValueFrom(apps.decide(4, 'Rejected', '   ')));
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors).toEqual({ remarks: 'A reason is required.' });
      http.expectNone(`${APPS}/4/reject`);

      const reject = firstValueFrom(apps.decide(4, 'Rejected', 'Peak period'));
      const req = http.expectOne(`${APPS}/4/reject`);
      expect(req.request.body).toEqual({ rowVersion: 'AAAAV1', remarks: 'Peak period' });
      req.flush({ ...application, status: 'Rejected', rowVersion: 'V2' });
      await reject;
    });

    it('maps a 400 on remarks to a field error and a 409 to the reload message', async () => {
      const { http, apps } = setup();
      await seed(http, apps);

      const bad = rejection(firstValueFrom(apps.cancel(4, 'x')));
      http.expectOne(`${APPS}/4/cancel`).flush(
        {
          detail: 'Invalid.',
          code: 'Validation',
          errors: { remarks: ['Remarks are too long.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      const validation = await bad;
      expect(validation.fieldErrors).toEqual({ remarks: 'Remarks are too long.' });

      const stale = rejection(firstValueFrom(apps.submit(4)));
      http.expectOne(`${APPS}/4/submit`).flush({}, { status: 409, statusText: 'Conflict' });
      const conflict = await stale;
      expect(conflict.code).toBe('Conflict');
      expect(conflict.message).toBe('The record changed. Reload and try again.');
    });
  });

  describe('balances', () => {
    it('getForEmployee calls /leave/balances with employee and year', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const rows = [
        {
          leaveTypeId: 2,
          code: 'AL',
          name: 'Annual',
          color: '#123456',
          entitled: 14,
          taken: 2,
          pending: 1,
          available: 11,
        },
      ];
      const promise = firstValueFrom(
        TestBed.inject(LeaveBalanceDataService).getForEmployee(5, 2026),
      );
      const req = http.expectOne((r) => r.url === '/api/leave/balances');
      expect(req.request.params.get('employeeId')).toBe('5');
      expect(req.request.params.get('year')).toBe('2026');
      req.flush(rows);
      expect(await promise).toEqual(rows);
    });

    it('query pages the employee list and loads balances for the visible page only', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const employees = TestBed.inject(EmployeeDataService);
      const item = (id: number) => ({
        id,
        employeeCode: `E${id}`,
        fullName: `Person ${id}`,
        departmentId: 3,
        departmentName: 'Finance',
        branchId: 2,
        branchName: 'Head Office',
      });
      vi.spyOn(employees, 'query').mockReturnValue(
        of({ items: [item(1), item(2)], total: 40, pageIndex: 1, pageSize: 2 }) as never,
      );
      const promise = firstValueFrom(
        TestBed.inject(LeaveBalanceDataService).query({
          year: 2026,
          search: 'per',
          departmentId: 3,
          branchId: null,
          pageIndex: 1,
          pageSize: 2,
          sort: { active: 'employeeName', direction: 'desc' },
        }),
      );
      expect(employees.query).toHaveBeenCalledWith(
        expect.objectContaining({
          sort: { active: 'fullName', direction: 'desc' },
          search: 'per',
          filters: { departmentId: 3, branchId: null },
        }),
      );
      const calls = http.match((r) => r.url === '/api/leave/balances');
      expect(calls.map((c) => c.request.params.get('employeeId'))).toEqual(['1', '2']);
      for (const c of calls) {
        c.flush([]);
      }
      const page = await promise;
      expect(page.total).toBe(40);
      expect(page.items.map((r) => r.employeeName)).toEqual(['Person 1', 'Person 2']);
      expect(page.items[0].departmentName).toBe('Finance');
      expect(page.items[0].balances).toEqual([]);
    });
  });

  describe('calendar', () => {
    it('builds the month grid of whole weeks from the flat range', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const promise = firstValueFrom(
        TestBed.inject(LeaveCalendarDataService).getMonth({
          month: '2026-12',
          departmentId: 3,
          leaveTypeId: null,
          includePending: false,
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/leave/calendar');
      // 1 Dec 2026 is a Tuesday: the grid runs Mon 30 Nov to Sun 3 Jan.
      expect(req.request.params.get('from')).toBe('2026-11-30');
      expect(req.request.params.get('to')).toBe('2027-01-03');
      expect(req.request.params.get('departmentId')).toBe('3');
      expect(req.request.params.get('includePending')).toBe('false');
      expect(req.request.params.has('leaveTypeId')).toBe(false);
      const entry = (name: string) => ({
        applicationId: 1,
        employeeId: 5,
        employeeName: name,
        leaveTypeId: 2,
        leaveTypeName: 'Annual',
        color: '#123456',
        status: 'Approved',
        halfDay: false,
      });
      req.flush({
        from: '2026-11-30',
        to: '2027-01-03',
        days: [
          {
            date: '2026-12-05',
            isWeekend: true,
            holiday: null,
            entries: [entry('Zed'), entry('Amy')],
          },
          { date: '2026-12-25', isWeekend: false, holiday: 'Holiday', entries: [] },
        ],
      });
      const month = await promise;
      expect(month.month).toBe('2026-12');
      expect(month.days).toHaveLength(35);
      expect(month.days[0]).toEqual({
        date: '2026-11-30',
        inMonth: false,
        isWeekend: false,
        holiday: null,
        entries: [],
      });
      const fifth = month.days.find((d) => d.date === '2026-12-05')!;
      expect(fifth.inMonth).toBe(true);
      expect(fifth.isWeekend).toBe(true);
      expect(fifth.entries.map((e) => e.employeeName)).toEqual(['Amy', 'Zed']);
      expect(month.days.find((d) => d.date === '2026-12-25')!.holiday).toBe('Holiday');
      expect(month.days[34].date).toBe('2027-01-03');
    });
  });
});
