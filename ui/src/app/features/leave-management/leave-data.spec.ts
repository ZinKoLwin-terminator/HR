import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { LeaveApplication } from '../../core/models/leave.models';
import { AttendanceRecordDataService } from '../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../../core/services/data/leave-data.services';
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

type NewApplication = Omit<LeaveApplication, 'id'>;

describe('leave data', () => {
  let types: LeaveTypeDataService;
  let policies: LeavePolicyDataService;
  let apps: LeaveApplicationDataService;
  let balances: LeaveBalanceDataService;
  let calendar: LeaveCalendarDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    types = TestBed.inject(LeaveTypeDataService);
    policies = TestBed.inject(LeavePolicyDataService);
    apps = TestBed.inject(LeaveApplicationDataService);
    balances = TestBed.inject(LeaveBalanceDataService);
    calendar = TestBed.inject(LeaveCalendarDataService);
  });

  /** A far-future Monday with no holidays: 2027-03-01 … Friday 2027-03-05 are all working days. */
  const MONDAY = '2027-03-01';

  const draft = (overrides: Partial<NewApplication> = {}): Omit<LeaveApplication, 'id'> => ({
    applicationNo: '',
    employeeId: 15,
    leaveTypeId: 1,
    startDate: MONDAY,
    endDate: addDays(MONDAY, 1),
    days: 0,
    halfDay: false,
    reason: 'Family trip',
    documentName: null,
    status: 'Draft',
    appliedOn: '',
    approverId: null,
    actionedOn: null,
    approverRemarks: null,
    ...overrides,
  });

  describe('leave types and policies', () => {
    it('validates leave types and protects those in use', async () => {
      const all = await firstValueFrom(types.getAll());
      const { id: _id, ...model } = all[0];
      void _id;
      await expectError(firstValueFrom(types.create({ ...model, code: 'AL' })), 'Validation');
      const carry = await expectError(
        firstValueFrom(
          types.create({ ...model, code: 'X1', carryForward: true, maxCarryForwardDays: 99 }),
        ),
        'Validation',
      );
      expect(carry.fieldErrors?.['maxCarryForwardDays']).toBeTruthy();

      const inUse = await expectError(firstValueFrom(types.delete(1)), 'Conflict');
      expect(inUse.message).toMatch(/application\(s\).*policy\(ies\)/);

      const created = await firstValueFrom(
        types.create({ ...model, code: 'SAB', name: 'Sabbatical' }),
      );
      await firstValueFrom(types.delete(created.id));
    });

    it('rejects clashing active policies but allows an inactive duplicate', async () => {
      const all = await firstValueFrom(policies.getAll());
      const executives = all.find((p) => p.jobGradeId === 9)!;
      const { id: _id, ...model } = executives;
      void _id;
      await expectError(
        firstValueFrom(policies.create({ ...model, name: 'Another executives policy' })),
        'Validation',
      );
      await expectError(
        firstValueFrom(policies.create({ ...model, name: executives.name, jobGradeId: 1 })),
        'Validation',
      );
      const inactive = await firstValueFrom(
        policies.create({ ...model, name: 'Draft executives policy', status: 'Inactive' }),
      );
      expect(inactive.status).toBe('Inactive');
    });
  });

  describe('applications', () => {
    it('creates a draft with a number, days that skip weekends, and today as applied date', async () => {
      const created = await firstValueFrom(
        apps.create(draft({ startDate: '2027-03-04', endDate: '2027-03-09' })),
      );
      // Thu 4, Fri 5, (weekend), Mon 8, Tue 9 → 4 working days
      expect(created).toMatchObject({ status: 'Draft', days: 4, appliedOn: todayIso() });
      expect(created.applicationNo).toMatch(/^LV-\d{4}-\d{4}$/);

      const half = await firstValueFrom(
        apps.create(
          draft({ startDate: '2027-03-15', endDate: '2027-03-15', halfDay: true, leaveTypeId: 2 }),
        ),
      );
      expect(half.days).toBe(0.5);
    });

    it('validates dates, eligibility and limits', async () => {
      const reversed = await expectError(
        firstValueFrom(apps.create(draft({ startDate: '2027-03-05', endDate: '2027-03-01' }))),
        'Validation',
      );
      expect(reversed.fieldErrors?.['endDate']).toBeTruthy();
      await expectError(
        firstValueFrom(
          apps.create(draft({ halfDay: true, startDate: MONDAY, endDate: addDays(MONDAY, 1) })),
        ),
        'Validation',
      );
      // Saturday–Sunday only: no working days
      const weekend = await expectError(
        firstValueFrom(apps.create(draft({ startDate: '2027-03-06', endDate: '2027-03-07' }))),
        'Validation',
      );
      expect(weekend.message).toMatch(/no working days/);
      // Employee 15 is a woman: paternity leave is for men only
      const gender = await expectError(
        firstValueFrom(apps.create(draft({ leaveTypeId: 5 }))),
        'Validation',
      );
      expect(gender.fieldErrors?.['leaveTypeId']).toBeTruthy();
      // Casual leave allows at most 3 consecutive days
      await expectError(
        firstValueFrom(
          apps.create(draft({ leaveTypeId: 2, startDate: MONDAY, endDate: addDays(MONDAY, 4) })),
        ),
        'Validation',
      );
      // Cannot start before joining
      await expectError(
        firstValueFrom(apps.create(draft({ startDate: '2019-01-07', endDate: '2019-01-08' }))),
        'Validation',
      );
      await expectError(firstValueFrom(apps.create(draft({ employeeId: 999 }))), 'Validation');
    });

    it('lets only drafts be edited or deleted', async () => {
      const created = await firstValueFrom(apps.create(draft()));
      const edited = await firstValueFrom(
        apps.update(created.id, { reason: 'Changed', endDate: addDays(MONDAY, 2) }),
      );
      expect(edited).toMatchObject({
        reason: 'Changed',
        days: 3,
        applicationNo: created.applicationNo,
      });
      await firstValueFrom(apps.submit(created.id));
      await expectError(firstValueFrom(apps.update(created.id, { reason: 'Again' })), 'Conflict');
      await expectError(firstValueFrom(apps.delete(created.id)), 'Conflict');

      const another = await firstValueFrom(
        apps.create(draft({ startDate: '2027-04-05', endDate: '2027-04-05' })),
      );
      await firstValueFrom(apps.delete(another.id));
      await expectError(firstValueFrom(apps.getById(another.id)), 'NotFound');
    });

    it('runs the Draft → Submitted → Approved workflow', async () => {
      const created = await firstValueFrom(apps.create(draft()));
      const submitted = await firstValueFrom(apps.submit(created.id));
      expect(submitted.status).toBe('Submitted');
      await expectError(firstValueFrom(apps.submit(created.id)), 'Conflict');

      const approved = await firstValueFrom(apps.decide(created.id, 'Approved', 'Enjoy'));
      expect(approved).toMatchObject({
        status: 'Approved',
        approverId: 2,
        approverRemarks: 'Enjoy',
        actionedOn: todayIso(),
      });
      await expectError(firstValueFrom(apps.decide(created.id, 'Rejected', 'late')), 'Conflict');

      // Approved leave in the future can still be cancelled.
      const cancelled = await firstValueFrom(apps.cancel(created.id, 'Plans changed'));
      expect(cancelled).toMatchObject({ status: 'Cancelled', approverRemarks: 'Plans changed' });
      await expectError(firstValueFrom(apps.cancel(created.id, '')), 'Conflict');
    });

    it('requires a reason to reject and refuses self-approval', async () => {
      const created = await firstValueFrom(apps.create(draft()));
      await firstValueFrom(apps.submit(created.id));
      const noReason = await expectError(
        firstValueFrom(apps.decide(created.id, 'Rejected', '  ')),
        'Validation',
      );
      expect(noReason.fieldErrors?.['approverRemarks']).toBeTruthy();
      const rejected = await firstValueFrom(apps.decide(created.id, 'Rejected', 'Team coverage'));
      expect(rejected).toMatchObject({ status: 'Rejected', approverRemarks: 'Team coverage' });

      // The signed-in demo user (employee 2) cannot decide their own request.
      const own = await firstValueFrom(
        apps.create(draft({ employeeId: 2, startDate: '2027-05-03', endDate: '2027-05-03' })),
      );
      await firstValueFrom(apps.submit(own.id));
      const self = await expectError(
        firstValueFrom(apps.decide(own.id, 'Approved', '')),
        'Conflict',
      );
      expect(self.message).toContain('your own');
    });

    it('checks documents, balance and overlaps when submitting', async () => {
      const medical = await firstValueFrom(apps.create(draft({ leaveTypeId: 3 })));
      const doc = await expectError(firstValueFrom(apps.submit(medical.id)), 'Validation');
      expect(doc.fieldErrors?.['documentName']).toBeTruthy();
      await firstValueFrom(apps.update(medical.id, { documentName: 'certificate.pdf' }));
      await firstValueFrom(apps.submit(medical.id));

      // The same dates are now taken for this employee.
      const clash = await expectError(
        firstValueFrom(
          apps.create(draft({ startDate: addDays(MONDAY, 1), endDate: addDays(MONDAY, 2) })),
        ),
        'Validation',
      );
      expect(clash.message).toMatch(/overlap LV-/);

      // 5 days of Casual leave needs a balance of 5; casual allows max 3 at a time so use annual leave
      // for an employee with a small balance: a new joiner has no annual leave yet.
      const employees = TestBed.inject(EmployeeDataService);
      const all = await firstValueFrom(employees.getAll());
      const newcomer = all.find(
        (e) =>
          e.employmentType === 'Full-time' &&
          e.employmentStatus === 'Probation' &&
          e.joiningDate > addDays(todayIso(), -80),
      )!;
      const tooMuch = await firstValueFrom(
        apps.create(
          draft({ employeeId: newcomer.id, startDate: '2027-06-07', endDate: '2027-06-08' }),
        ),
      );
      const balance = await expectError(firstValueFrom(apps.submit(tooMuch.id)), 'Conflict');
      expect(balance.message).toMatch(/Insufficient Annual Leave balance/);
    });

    it('marks past attendance as On Leave when leave is approved, and filters by date overlap', async () => {
      const attendance = TestBed.inject(AttendanceRecordDataService);
      const records = (
        await firstValueFrom(
          attendance.query({
            pageIndex: 0,
            pageSize: 100,
            filters: { employeeId: 17, status: 'Present' },
            sort: { active: 'date', direction: 'desc' },
          }),
        )
      ).items;
      const mine = (await firstValueFrom(apps.getAll())).filter((a) => a.employeeId === 17);
      const free = records.find(
        (r) =>
          !mine.some(
            (a) =>
              ['Submitted', 'Approved'].includes(a.status) &&
              a.startDate <= r.date &&
              a.endDate >= r.date,
          ),
      )!;

      const created = await firstValueFrom(
        apps.create(
          draft({
            employeeId: 17,
            leaveTypeId: 2,
            startDate: free.date,
            endDate: free.date,
            halfDay: false,
          }),
        ),
      );
      await firstValueFrom(apps.submit(created.id));
      await firstValueFrom(apps.decide(created.id, 'Approved', ''));

      const updated = (
        await firstValueFrom(
          attendance.query({
            pageIndex: 0,
            pageSize: 5,
            filters: { employeeId: 17, dateFrom: free.date, dateTo: free.date },
          }),
        )
      ).items[0];
      expect(updated).toMatchObject({
        status: 'On Leave',
        checkIn: null,
        remarks: created.applicationNo,
      });

      const overlapping = await firstValueFrom(
        apps.query({
          pageIndex: 0,
          pageSize: 50,
          filters: { employeeId: 17, dateFrom: free.date, dateTo: free.date },
        }),
      );
      expect(overlapping.items.map((a) => a.id)).toContain(created.id);
      const before = await firstValueFrom(
        apps.query({
          pageIndex: 0,
          pageSize: 50,
          filters: { employeeId: 17, dateTo: addDays(free.date, -1), status: 'Approved' },
        }),
      );
      expect(before.items.map((a) => a.id)).not.toContain(created.id);
    });

    it('blocks cancelling leave that has already started', async () => {
      const started = (await firstValueFrom(apps.getAll())).find(
        (a) => a.status === 'Approved' && a.startDate <= todayIso(),
      )!;
      const error = await expectError(firstValueFrom(apps.cancel(started.id, '')), 'Conflict');
      expect(error.message).toMatch(/already started/);
    });
  });

  describe('balances', () => {
    it('applies policies by grade, employment type, service and accrual', async () => {
      const find = async (employeeId: number, code: string) =>
        (await firstValueFrom(balances.getForEmployee(employeeId, new Date().getFullYear()))).find(
          (b) => b.code === code,
        )!;

      expect((await find(1, 'AL')).entitled).toBe(25); // CEO — grade 9
      expect((await find(3, 'AL')).entitled).toBe(22); // Head of Engineering — grade 8
      expect((await find(5, 'AL')).entitled).toBe(18); // Engineering Manager — grade 6
      expect((await find(15, 'AL')).entitled).toBe(15); // standard policy
      expect((await find(3, 'ML')).entitled).toBe(30); // no policy → leave type default

      const month = new Date().getMonth() + 1;
      expect((await find(1, 'CL')).entitled).toBe(Math.round(((6 * month) / 12) * 2) / 2);

      const intern = (await firstValueFrom(TestBed.inject(EmployeeDataService).getAll())).find(
        (e) => e.employmentType === 'Intern',
      )!;
      expect((await find(intern.id, 'ML')).entitled).toBe(10);
      expect((await find(intern.id, 'AL')).entitled).toBeLessThanOrEqual(6);

      const newcomer = (await firstValueFrom(TestBed.inject(EmployeeDataService).getAll())).find(
        (e) =>
          e.employmentType === 'Full-time' &&
          e.employmentStatus === 'Probation' &&
          e.joiningDate > addDays(todayIso(), -80),
      )!;
      expect((await find(newcomer.id, 'AL')).entitled).toBe(0);
    });

    it('subtracts approved and pending leave', async () => {
      const mat = (
        await firstValueFrom(balances.getForEmployee(35, new Date().getFullYear()))
      ).find((b) => b.code === 'MAT')!;
      expect(mat.taken).toBeGreaterThan(0);
      expect(mat.available).toBe(Math.max(0, mat.entitled - mat.taken - mat.pending));

      const al = (await firstValueFrom(balances.getForEmployee(26, new Date().getFullYear()))).find(
        (b) => b.code === 'AL',
      )!;
      expect(al.pending).toBeGreaterThan(0); // submitted annual leave is held as pending
    });

    it('lists employees with search, filters, sorting and paging, and a previous year without leave', async () => {
      const year = new Date().getFullYear();
      const page = await firstValueFrom(
        balances.query({
          year,
          search: '',
          departmentId: null,
          branchId: null,
          pageIndex: 0,
          pageSize: 10,
        }),
      );
      expect(page.total).toBe(58);
      expect(page.items).toHaveLength(10);
      expect(page.items[0]).toMatchObject({ employeeCode: 'EMP001' });

      const hr = await firstValueFrom(
        balances.query({
          year,
          search: '',
          departmentId: 2,
          branchId: null,
          pageIndex: 0,
          pageSize: 50,
        }),
      );
      expect(hr.items.every((r) => r.departmentName === 'Human Resources')).toBe(true);

      const found = await firstValueFrom(
        balances.query({
          year,
          search: 'carter',
          departmentId: null,
          branchId: null,
          pageIndex: 0,
          pageSize: 10,
        }),
      );
      expect(found.items.map((r) => r.employeeCode)).toEqual(['EMP005']);

      const desc = await firstValueFrom(
        balances.query({
          year,
          search: '',
          departmentId: null,
          branchId: null,
          pageIndex: 0,
          pageSize: 3,
          sort: { active: 'employeeName', direction: 'desc' },
        }),
      );
      expect(desc.items[0].employeeName.localeCompare(desc.items[1].employeeName)).toBeGreaterThan(
        0,
      );

      const last = await firstValueFrom(
        balances.query({
          year: year - 1,
          search: '',
          departmentId: null,
          branchId: null,
          pageIndex: 0,
          pageSize: 5,
        }),
      );
      expect(
        last.items.every((r) => r.balances.every((b) => b.taken === 0 && b.pending === 0)),
      ).toBe(true);
      await expectError(firstValueFrom(balances.getForEmployee(999, year)), 'NotFound');
    });
  });

  describe('calendar', () => {
    const month = todayIso().slice(0, 7);

    it('returns whole Monday–Sunday weeks with weekends and holidays flagged', async () => {
      const m = await firstValueFrom(
        calendar.getMonth({
          month: '2026-11',
          departmentId: null,
          leaveTypeId: null,
          includePending: true,
        }),
      );
      expect(m.days.length % 7).toBe(0);
      expect(dayOfWeek(m.days[0].date)).toBe(1);
      expect(m.days[0].date).toBe('2026-10-26');
      // 30 Nov 2026 is a Monday, so the last week runs to Sunday 6 Dec (6 weeks in total).
      expect(m.days.at(-1)?.date).toBe('2026-12-06');
      expect(m.days).toHaveLength(42);
      expect(m.days.filter((d) => d.inMonth)).toHaveLength(30);
      expect(m.days.find((d) => d.date === '2026-11-20')?.holiday).toBe('Company Foundation Day');
      expect(m.days.find((d) => d.date === '2026-11-21')?.isWeekend).toBe(true);
    });

    it('lists approved and pending leave and honours the filters', async () => {
      const full = await firstValueFrom(
        calendar.getMonth({ month, departmentId: null, leaveTypeId: null, includePending: true }),
      );
      const approvedOnly = await firstValueFrom(
        calendar.getMonth({ month, departmentId: null, leaveTypeId: null, includePending: false }),
      );
      const entries = (m: typeof full) => m.days.flatMap((d) => d.entries);
      expect(entries(full).some((e) => e.status === 'Submitted')).toBe(true);
      expect(entries(approvedOnly).every((e) => e.status === 'Approved')).toBe(true);
      expect(entries(full).length).toBeGreaterThan(entries(approvedOnly).length);

      const casual = await firstValueFrom(
        calendar.getMonth({ month, departmentId: null, leaveTypeId: 2, includePending: true }),
      );
      expect(entries(casual).every((e) => e.leaveTypeName === 'Casual Leave')).toBe(true);

      const engineering = await firstValueFrom(
        calendar.getMonth({ month, departmentId: 4, leaveTypeId: null, includePending: true }),
      );
      expect(entries(engineering).length).toBeLessThanOrEqual(entries(full).length);
    });
  });
});
