import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  AttendanceAdjustmentDataService,
  AttendanceOverviewDataService,
  AttendanceRecordDataService,
  HolidayDataService,
  OvertimeDataService,
  ShiftDataService,
  WorkScheduleDataService,
} from '../../core/services/data/attendance-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import {
  AttendanceAdjustmentHttpService,
  OvertimeHttpService,
} from './attendance-approval-http.services';
import { HolidayHttpService, ShiftHttpService } from './attendance-http.services';
import { AttendanceRecordHttpService } from './attendance-record-http.service';
import {
  AttendanceOverviewHttpService,
  WorkScheduleHttpService,
} from './attendance-views-http.services';

const shift = {
  id: 3,
  code: 'DAY',
  name: 'Day',
  startTime: '09:00',
  endTime: '18:00',
  breakMinutes: 60,
  graceMinutes: 10,
  workDays: [1, 2, 3, 4, 5],
  isNightShift: false,
  color: '#1976d2',
  status: 'Active',
  rowVersion: 'SHIFTV1',
};
const holiday = {
  id: 8,
  date: '2026-12-25',
  name: 'Year End',
  type: 'Public',
  rowVersion: 'HOLV1',
};
const adjustment = {
  id: 5,
  employeeId: 2,
  date: '2026-11-03',
  currentCheckIn: '09:40',
  currentCheckOut: null,
  requestedCheckIn: '09:00',
  requestedCheckOut: null,
  requestedStatus: null,
  reason: 'Forgot to punch',
  status: 'Pending',
  requestedOn: '2026-11-04',
  decidedBy: null,
  decidedOn: null,
  remarks: '',
  rowVersion: 'ADJV1',
};
const overtime = {
  id: 6,
  employeeId: 2,
  date: '2026-11-03',
  startTime: '18:00',
  endTime: '20:00',
  minutes: 120,
  reason: 'Release',
  status: 'Pending',
  requestedOn: '2026-11-04',
  decidedBy: null,
  decidedOn: null,
  remarks: '',
  rowVersion: 'OTV1',
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
    shifts: TestBed.inject(ShiftDataService),
    holidays: TestBed.inject(HolidayDataService),
    records: TestBed.inject(AttendanceRecordDataService),
    adjustments: TestBed.inject(AttendanceAdjustmentDataService),
    overtime: TestBed.inject(OvertimeDataService),
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
const page = (items: unknown[]) => ({ items, total: items.length, pageIndex: 0, pageSize: 10 });

describe('Attendance HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('binds every attendance contract to HTTP in API mode', () => {
    setup();
    expect(TestBed.inject(ShiftDataService)).toBeInstanceOf(ShiftHttpService);
    expect(TestBed.inject(HolidayDataService)).toBeInstanceOf(HolidayHttpService);
    expect(TestBed.inject(AttendanceRecordDataService)).toBeInstanceOf(AttendanceRecordHttpService);
    expect(TestBed.inject(AttendanceAdjustmentDataService)).toBeInstanceOf(
      AttendanceAdjustmentHttpService,
    );
    expect(TestBed.inject(OvertimeDataService)).toBeInstanceOf(OvertimeHttpService);
    expect(TestBed.inject(AttendanceOverviewDataService)).toBeInstanceOf(
      AttendanceOverviewHttpService,
    );
    expect(TestBed.inject(WorkScheduleDataService)).toBeInstanceOf(WorkScheduleHttpService);
  });

  describe('shifts and holidays', () => {
    it('queries shifts with filter and a supported sort, and drops unsupported sort keys', async () => {
      const { http, shifts } = setup();
      const first = firstValueFrom(
        shifts.query({
          pageIndex: 1,
          pageSize: 20,
          search: ' day ',
          filters: { status: 'Active' },
          sort: { active: 'name', direction: 'asc' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/shifts/query');
      expect(req.request.params.get('status')).toBe('Active');
      expect(req.request.params.get('search')).toBe('day');
      expect(req.request.params.get('sort')).toBe('name:asc');
      req.flush(page([shift]));
      await first;

      const second = firstValueFrom(
        shifts.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'workDays', direction: 'asc' },
        }),
      );
      const req2 = http.expectOne((r) => r.url === '/api/shifts/query');
      expect(req2.request.params.has('sort')).toBe(false);
      req2.flush(page([]));
      await second;
    });

    it('changes shift status with activate and deactivate, never a partial PUT', async () => {
      const { http, shifts } = setup();
      const seed = firstValueFrom(shifts.query({ pageIndex: 0, pageSize: 10 }));
      http.expectOne((r) => r.url === '/api/shifts/query').flush(page([shift]));
      await seed;

      const done = firstValueFrom(shifts.update(3, { status: 'Inactive' }));
      await tick();
      const req = http.expectOne('/api/shifts/3/deactivate');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'SHIFTV1' });
      req.flush({ ...shift, status: 'Inactive', rowVersion: 'SHIFTV2' });
      await done;

      const back = firstValueFrom(shifts.update(3, { status: 'Active' }));
      await tick();
      const activate = http.expectOne('/api/shifts/3/activate');
      expect(activate.request.body).toEqual({ rowVersion: 'SHIFTV2' });
      activate.flush(shift);
      await back;
    });

    it('sends rowVersion on PUT and DELETE, without status in the PUT', async () => {
      const { http, shifts, holidays } = setup();
      const seed = firstValueFrom(shifts.query({ pageIndex: 0, pageSize: 10 }));
      http.expectOne((r) => r.url === '/api/shifts/query').flush(page([shift]));
      await seed;

      const put = firstValueFrom(shifts.update(3, { name: 'Early', status: 'Active' }));
      await tick();
      const putReq = http.expectOne('/api/shifts/3');
      expect(putReq.request.method).toBe('PUT');
      expect(putReq.request.body).toEqual({ name: 'Early', rowVersion: 'SHIFTV1' });
      putReq.flush({ ...shift, name: 'Early', rowVersion: 'SHIFTV2' });
      await put;

      const del = firstValueFrom(shifts.delete(3));
      await tick();
      const delReq = http.expectOne('/api/shifts/3');
      expect(delReq.request.method).toBe('DELETE');
      expect(delReq.request.body).toEqual({ rowVersion: 'SHIFTV2' });
      delReq.flush(null, { status: 204, statusText: 'No Content' });
      await del;

      const hSeed = firstValueFrom(holidays.query({ pageIndex: 0, pageSize: 10 }));
      http.expectOne((r) => r.url === '/api/holidays/query').flush(page([holiday]));
      await hSeed;
      const hPut = firstValueFrom(holidays.update(8, { name: 'New Year' }));
      await tick();
      const hReq = http.expectOne('/api/holidays/8');
      expect(hReq.request.body).toEqual({ name: 'New Year', rowVersion: 'HOLV1' });
      hReq.flush(holiday);
      await hPut;
    });

    it('queries holidays by date range and type', async () => {
      const { http, holidays } = setup();
      const first = firstValueFrom(
        holidays.query({
          pageIndex: 0,
          pageSize: 50,
          filters: { dateFrom: '2026-12-01', dateTo: '2026-12-31', type: 'Public' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/holidays/query');
      expect(req.request.params.get('dateFrom')).toBe('2026-12-01');
      expect(req.request.params.get('dateTo')).toBe('2026-12-31');
      expect(req.request.params.get('type')).toBe('Public');
      req.flush(page([holiday]));
      await first;
    });

    it('maps a 409 to the reload message', async () => {
      const { http, shifts } = setup();
      const seed = firstValueFrom(shifts.query({ pageIndex: 0, pageSize: 10 }));
      http.expectOne((r) => r.url === '/api/shifts/query').flush(page([shift]));
      await seed;
      const failed = rejection(firstValueFrom(shifts.delete(3)));
      await tick();
      http
        .expectOne('/api/shifts/3')
        .flush({ code: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      const error = await failed;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('The record changed. Reload and try again.');
    });
  });

  describe('records and punches', () => {
    it('builds the records query with filters and drops unsupported sort keys', async () => {
      const { http, records } = setup();
      const first = firstValueFrom(
        records.query({
          pageIndex: 0,
          pageSize: 25,
          search: 'ann',
          filters: {
            employeeId: 4,
            departmentId: 2,
            branchId: null,
            status: 'Late',
            dateFrom: '2026-11-01',
            dateTo: '2026-11-07',
            lateOnly: true,
            earlyOnly: null,
            overtimeOnly: false,
          },
          sort: { active: 'date', direction: 'desc' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/attendance-records/query');
      const p = req.request.params;
      expect(p.get('employeeId')).toBe('4');
      expect(p.get('departmentId')).toBe('2');
      expect(p.get('status')).toBe('Late');
      expect(p.get('dateFrom')).toBe('2026-11-01');
      expect(p.get('dateTo')).toBe('2026-11-07');
      expect(p.get('lateOnly')).toBe('true');
      expect(p.has('branchId')).toBe(false);
      expect(p.has('earlyOnly')).toBe(false);
      expect(p.has('overtimeOnly')).toBe(false);
      expect(p.get('sort')).toBe('date:desc');
      req.flush(page([]));
      await first;

      const second = firstValueFrom(
        records.query({
          pageIndex: 0,
          pageSize: 25,
          sort: { active: 'employeeName', direction: 'asc' },
        }),
      );
      const req2 = http.expectOne((r) => r.url === '/api/attendance-records/query');
      expect(req2.request.params.has('sort')).toBe(false);
      req2.flush(page([]));
      await second;
    });

    it('punch sends only employeeId and kind, with no date or time', async () => {
      const { http, records } = setup();
      const done = firstValueFrom(
        records.punch({ employeeId: 2, date: '2026-11-11', time: '09:15', kind: 'in' }),
      );
      const req = http.expectOne('/api/attendance/punches');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ employeeId: 2, kind: 'CheckIn' });
      req.flush({ id: 1, employeeId: 2 });
      await done;

      const out = firstValueFrom(
        records.punch({ employeeId: 2, date: '2026-11-11', time: '18:01', kind: 'out' }),
      );
      const outReq = http.expectOne('/api/attendance/punches');
      expect(outReq.request.body).toEqual({ employeeId: 2, kind: 'CheckOut' });
      outReq.flush({ id: 1, employeeId: 2 });
      await out;
    });

    it('refuses create, update and delete of a record without sending a request', async () => {
      const { http, records } = setup();
      const created = await rejection(
        firstValueFrom(
          records.create({
            employeeId: 1,
            date: '2026-11-01',
            shiftId: null,
            checkIn: null,
            checkOut: null,
            workedMinutes: 0,
            lateMinutes: 0,
            earlyLeaveMinutes: 0,
            overtimeMinutes: 0,
            status: 'Absent',
            source: 'Manual',
            remarks: '',
          }),
        ),
      );
      expect(created.code).toBe('Forbidden');
      expect((await rejection(firstValueFrom(records.update(1, { remarks: 'x' })))).code).toBe(
        'Forbidden',
      );
      expect((await rejection(firstValueFrom(records.delete(1)))).code).toBe('Forbidden');
      http.expectNone(() => true);
    });
  });

  describe('adjustments', () => {
    it('create posts only the create fields, as HH:mm', async () => {
      const { http, adjustments } = setup();
      const done = firstValueFrom(
        adjustments.create({
          employeeId: 2,
          date: '2026-11-03',
          recordId: 99,
          currentCheckIn: '09:40',
          currentCheckOut: null,
          requestedCheckIn: '09:00:30',
          requestedCheckOut: '',
          requestedStatus: null,
          reason: 'Forgot to punch',
          status: 'Pending',
          requestedOn: '',
          decidedBy: null,
          decidedOn: null,
          remarks: '',
        }),
      );
      const req = http.expectOne('/api/attendance-adjustments');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        employeeId: 2,
        date: '2026-11-03',
        requestedCheckIn: '09:00',
        requestedCheckOut: null,
        requestedStatus: null,
        reason: 'Forgot to punch',
      });
      req.flush(adjustment, { status: 201, statusText: 'Created' });
      await done;
    });

    it('queries with filters and sort, and refuses update without a request', async () => {
      const { http, adjustments } = setup();
      const first = firstValueFrom(
        adjustments.query({
          pageIndex: 0,
          pageSize: 10,
          filters: {
            employeeId: 2,
            status: 'Pending',
            dateFrom: '2026-11-03',
            dateTo: '2026-11-03',
          },
          sort: { active: 'date', direction: 'desc' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/attendance-adjustments/query');
      expect(req.request.params.get('employeeId')).toBe('2');
      expect(req.request.params.get('status')).toBe('Pending');
      expect(req.request.params.get('dateFrom')).toBe('2026-11-03');
      expect(req.request.params.get('dateTo')).toBe('2026-11-03');
      expect(req.request.params.get('sort')).toBe('date:desc');
      req.flush(page([adjustment]));
      await first;

      expect((await rejection(firstValueFrom(adjustments.update(5, { reason: 'x' })))).code).toBe(
        'Forbidden',
      );
      http.expectNone(() => true);
    });

    it('approve and reject send rowVersion and remarks', async () => {
      const { http, adjustments } = setup();
      const seed = firstValueFrom(adjustments.query({ pageIndex: 0, pageSize: 10 }));
      http
        .expectOne((r) => r.url === '/api/attendance-adjustments/query')
        .flush(page([adjustment]));
      await seed;

      const approved = firstValueFrom(adjustments.decide(5, 'Approved', ' ok '));
      await tick();
      const approve = http.expectOne('/api/attendance-adjustments/5/approve');
      expect(approve.request.body).toEqual({ rowVersion: 'ADJV1', remarks: 'ok' });
      approve.flush({ ...adjustment, status: 'Approved', rowVersion: 'ADJV2' });
      await approved;

      const rejected = firstValueFrom(adjustments.decide(5, 'Rejected', 'Wrong day'));
      await tick();
      const reject = http.expectOne('/api/attendance-adjustments/5/reject');
      expect(reject.request.body).toEqual({ rowVersion: 'ADJV2', remarks: 'Wrong day' });
      reject.flush({ ...adjustment, status: 'Rejected' });
      await rejected;
    });

    it('refuses a reject with blank remarks before any request', async () => {
      const { http, adjustments } = setup();
      const error = await rejection(firstValueFrom(adjustments.decide(5, 'Rejected', '   ')));
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['remarks']).toBeTruthy();
      http.expectNone(() => true);
    });

    it('maps a 400 on remarks to a field error', async () => {
      const { http, adjustments } = setup();
      const seed = firstValueFrom(adjustments.query({ pageIndex: 0, pageSize: 10 }));
      http
        .expectOne((r) => r.url === '/api/attendance-adjustments/query')
        .flush(page([adjustment]));
      await seed;
      const failed = rejection(firstValueFrom(adjustments.decide(5, 'Rejected', 'x')));
      await tick();
      http.expectOne('/api/attendance-adjustments/5/reject').flush(
        {
          detail: 'Invalid.',
          code: 'Validation',
          errors: { remarks: ['Remarks are required.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      const error = await failed;
      expect(error.code).toBe('Validation');
      expect(error.fieldErrors?.['remarks']).toBe('Remarks are required.');
    });

    it('maps a 409 on approve to the reload message', async () => {
      const { http, adjustments } = setup();
      const seed = firstValueFrom(adjustments.query({ pageIndex: 0, pageSize: 10 }));
      http
        .expectOne((r) => r.url === '/api/attendance-adjustments/query')
        .flush(page([adjustment]));
      await seed;
      const failed = rejection(firstValueFrom(adjustments.decide(5, 'Approved', '')));
      await tick();
      http
        .expectOne('/api/attendance-adjustments/5/approve')
        .flush({}, { status: 409, statusText: 'Conflict' });
      const error = await failed;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('The record changed. Reload and try again.');
    });

    it('delete sends the rowVersion in the body', async () => {
      const { http, adjustments } = setup();
      const seed = firstValueFrom(adjustments.query({ pageIndex: 0, pageSize: 10 }));
      http
        .expectOne((r) => r.url === '/api/attendance-adjustments/query')
        .flush(page([adjustment]));
      await seed;
      const done = firstValueFrom(adjustments.delete(5));
      await tick();
      const req = http.expectOne('/api/attendance-adjustments/5');
      expect(req.request.method).toBe('DELETE');
      expect(req.request.body).toEqual({ rowVersion: 'ADJV1' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      await done;
    });

    it('getAll pages the query because there is no unpaged GET', async () => {
      const { http, adjustments } = setup();
      const done = firstValueFrom(adjustments.getAll());
      const req = http.expectOne((r) => r.url === '/api/attendance-adjustments/query');
      expect(req.request.params.get('pageSize')).toBe('200');
      req.flush({ items: [adjustment], total: 1, pageIndex: 0, pageSize: 200 });
      expect((await done).length).toBe(1);
    });
  });

  describe('overtime', () => {
    it('create posts startTime and endTime as HH:mm and no derived fields', async () => {
      const { http, overtime: ot } = setup();
      const done = firstValueFrom(
        ot.create({
          employeeId: 2,
          date: '2026-11-03',
          startTime: '18:00:00',
          endTime: '20:00',
          minutes: 0,
          reason: 'Release',
          status: 'Pending',
          requestedOn: '',
          decidedBy: null,
          decidedOn: null,
          remarks: '',
        }),
      );
      const req = http.expectOne('/api/overtime-records');
      expect(req.request.body).toEqual({
        employeeId: 2,
        date: '2026-11-03',
        startTime: '18:00',
        endTime: '20:00',
        reason: 'Release',
      });
      req.flush(overtime, { status: 201, statusText: 'Created' });
      await done;
    });

    it('approve, reject and delete carry rowVersion; blank reject remarks are refused', async () => {
      const { http, overtime: ot } = setup();
      const seed = firstValueFrom(ot.query({ pageIndex: 0, pageSize: 10 }));
      http.expectOne((r) => r.url === '/api/overtime-records/query').flush(page([overtime]));
      await seed;

      expect(
        (await rejection(firstValueFrom(ot.decide(6, 'Rejected', '')))).fieldErrors?.['remarks'],
      ).toBeTruthy();
      http.expectNone(() => true);

      const approved = firstValueFrom(ot.decide(6, 'Approved', ''));
      await tick();
      const approve = http.expectOne('/api/overtime-records/6/approve');
      expect(approve.request.body).toEqual({ rowVersion: 'OTV1' });
      approve.flush({ ...overtime, status: 'Approved', rowVersion: 'OTV2' });
      await approved;

      const rejected = firstValueFrom(ot.decide(6, 'Rejected', 'Not needed'));
      await tick();
      const reject = http.expectOne('/api/overtime-records/6/reject');
      expect(reject.request.body).toEqual({ rowVersion: 'OTV2', remarks: 'Not needed' });
      reject.flush(overtime);
      await rejected;

      const del = firstValueFrom(ot.delete(6));
      await tick();
      const delReq = http.expectOne('/api/overtime-records/6');
      expect(delReq.request.method).toBe('DELETE');
      expect(delReq.request.body).toEqual({ rowVersion: 'OTV1' });
      delReq.flush(null, { status: 204, statusText: 'No Content' });
      await del;
    });
  });

  describe('overview and schedule', () => {
    it('maps the overview to the screen model', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const service = TestBed.inject(AttendanceOverviewDataService);
      const done = firstValueFrom(service.getOverview('2026-11-10', 3));
      const req = http.expectOne((r) => r.url === '/api/attendance/overview');
      expect(req.request.params.get('date')).toBe('2026-11-10');
      expect(req.request.params.get('branchId')).toBe('3');
      req.flush({
        summary: {
          date: '2026-11-10',
          scheduled: 10,
          present: 6,
          late: 2,
          halfDay: 0,
          absent: 1,
          onLeave: 1,
          attendanceRate: 80,
          extra: 'ignored',
        },
        byDepartment: [
          {
            departmentId: 1,
            department: 'Ops',
            present: 6,
            late: 2,
            halfDay: 0,
            absent: 1,
            onLeave: 1,
            total: 10,
          },
        ],
        late: [
          {
            employeeId: 2,
            employeeCode: 'E002',
            name: 'Ann',
            department: 'Ops',
            detail: 'Checked in 09:42',
          },
        ],
        absent: [],
        trend: [{ date: '2026-11-10', present: 6, late: 2, absent: 1, onLeave: 1 }],
      });
      const overview = await done;
      expect(overview.summary).toEqual({
        date: '2026-11-10',
        scheduled: 10,
        present: 6,
        late: 2,
        halfDay: 0,
        absent: 1,
        onLeave: 1,
        attendanceRate: 80,
      });
      expect(overview.byDepartment[0].department).toBe('Ops');
      expect(overview.late[0].name).toBe('Ann');
      expect(overview.trend.length).toBe(1);
    });

    it('omits branchId when none is chosen', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const done = firstValueFrom(
        TestBed.inject(AttendanceOverviewDataService).getOverview('2026-11-10', null),
      );
      const req = http.expectOne((r) => r.url === '/api/attendance/overview');
      expect(req.request.params.has('branchId')).toBe(false);
      req.flush({
        summary: {
          date: '2026-11-10',
          scheduled: 0,
          present: 0,
          late: 0,
          halfDay: 0,
          absent: 0,
          onLeave: 0,
          attendanceRate: 0,
        },
        byDepartment: [],
        late: [],
        absent: [],
        trend: [],
      });
      await done;
    });

    it('maps the schedule to the screen model', async () => {
      setup();
      const http = TestBed.inject(HttpTestingController);
      const done = firstValueFrom(
        TestBed.inject(WorkScheduleDataService).getWeek({
          weekStart: '2026-11-09',
          departmentId: 2,
          branchId: null,
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/attendance/schedule');
      expect(req.request.params.get('weekStart')).toBe('2026-11-09');
      expect(req.request.params.get('departmentId')).toBe('2');
      expect(req.request.params.has('branchId')).toBe(false);
      req.flush({
        weekStart: '2026-11-09',
        days: ['2026-11-09', '2026-11-10'],
        rows: [
          {
            employeeId: 2,
            employeeCode: 'E002',
            employeeName: 'Ann',
            department: 'Ops',
            shiftName: 'Day',
            shiftColor: '#1976d2',
            cells: [
              { date: '2026-11-09', kind: 'Shift', label: '09:00–18:00' },
              { date: '2026-11-10', kind: 'Off', label: 'Off' },
            ],
          },
        ],
      });
      const week = await done;
      expect(week.days.length).toBe(2);
      expect(week.rows[0].cells[1]).toEqual({ date: '2026-11-10', kind: 'Off', label: 'Off' });
      expect(week.rows[0].shiftColor).toBe('#1976d2');
    });
  });
});
