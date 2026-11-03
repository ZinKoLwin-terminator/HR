import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import {
  AttendanceAdjustment,
  AttendanceListItem,
  OvertimeRecord,
} from '../../core/models/attendance.models';
import {
  AttendanceAdjustmentDataService,
  AttendanceOverviewDataService,
  AttendanceRecordDataService,
  OvertimeDataService,
  ShiftDataService,
  WorkScheduleDataService,
} from '../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { HrmsValidators } from '../../shared/validators/hrms-validators';
import { FormControl, FormGroup } from '@angular/forms';
import { mondayOf } from './work-schedules/work-schedules.component';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('attendance data', () => {
  let records: AttendanceRecordDataService;
  let adjustments: AttendanceAdjustmentDataService;
  let overtime: OvertimeDataService;
  let shifts: ShiftDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    records = TestBed.inject(AttendanceRecordDataService);
    adjustments = TestBed.inject(AttendanceAdjustmentDataService);
    overtime = TestBed.inject(OvertimeDataService);
    shifts = TestBed.inject(ShiftDataService);
  });

  const query = async (filters: object, pageSize = 500, search = '') =>
    (
      await firstValueFrom(
        records.query({
          pageIndex: 0,
          pageSize,
          search,
          filters,
          sort: { active: 'date', direction: 'desc' },
        }),
      )
    ).items as AttendanceListItem[];

  describe('records list', () => {
    it('joins employee, department, branch and shift names', async () => {
      const [first] = await query({ employeeId: 3 }, 5);
      expect(first).toMatchObject({
        employeeCode: 'EMP003',
        employeeName: 'Kyaw Zin Htet',
        departmentName: 'Engineering',
        branchName: 'Yangon Head Office',
        shiftName: 'General Shift',
      });
    });

    it('filters by status, department, branch and date range', async () => {
      const late = await query({ status: 'Late' });
      expect(late.length).toBeGreaterThan(0);
      expect(late.every((r) => r.status === 'Late')).toBe(true);

      const engineering = await query({ departmentId: 4 }, 100);
      expect(engineering.every((r) => r.departmentName === 'Engineering')).toBe(true);

      const warehouse = await query({ branchId: 4 }, 100);
      expect(warehouse.every((r) => r.branchId === 4)).toBe(true);

      const from = addDays(todayIso(), -6);
      const week = await query({ dateFrom: from, dateTo: addDays(todayIso(), -3) });
      expect(week.every((r) => r.date >= from && r.date <= addDays(todayIso(), -3))).toBe(true);
      expect(new Set(week.map((r) => r.date)).size).toBe(4);
    });

    it('supports exception filters and search by name or code', async () => {
      expect((await query({ lateOnly: true })).every((r) => r.lateMinutes > 0)).toBe(true);
      expect((await query({ earlyOnly: true })).every((r) => r.earlyLeaveMinutes > 0)).toBe(true);
      expect((await query({ overtimeOnly: true })).every((r) => r.overtimeMinutes > 0)).toBe(true);

      const byName = await query({}, 100, 'daniel carter');
      expect(byName.length).toBeGreaterThan(0);
      expect(byName.every((r) => r.employeeCode === 'EMP005')).toBe(true);
      expect((await query({}, 100, 'emp005')).every((r) => r.employeeCode === 'EMP005')).toBe(true);
    });
  });

  describe('check-in / check-out', () => {
    // A fixed historic Monday where no generated record exists.
    const DATE = '2026-01-05';

    it('checks in (late), then out, and applies the shift rules', async () => {
      const inRecord = await firstValueFrom(
        records.punch({ employeeId: 2, date: DATE, time: '09:30', kind: 'in' }),
      );
      expect(inRecord).toMatchObject({
        checkIn: '09:30',
        status: 'Late',
        lateMinutes: 30,
        source: 'Web',
      });

      const outRecord = await firstValueFrom(
        records.punch({ employeeId: 2, date: DATE, time: '18:10', kind: 'out' }),
      );
      expect(outRecord).toMatchObject({
        checkOut: '18:10',
        workedMinutes: 8 * 60 + 40 - 60, // 09:30–18:10 minus the 60 minute break
        status: 'Late',
      });
      expect(outRecord.overtimeMinutes).toBe(0);
    });

    it('rejects duplicate, out-of-order and invalid punches', async () => {
      await expectError(
        firstValueFrom(records.punch({ employeeId: 2, date: DATE, time: '17:00', kind: 'out' })),
        'Conflict',
      );
      await firstValueFrom(records.punch({ employeeId: 2, date: DATE, time: '09:00', kind: 'in' }));
      await expectError(
        firstValueFrom(records.punch({ employeeId: 2, date: DATE, time: '09:05', kind: 'in' })),
        'Conflict',
      );
      await expectError(
        firstValueFrom(records.punch({ employeeId: 2, date: DATE, time: '08:00', kind: 'out' })),
        'Validation',
      );
      await firstValueFrom(
        records.punch({ employeeId: 2, date: DATE, time: '18:00', kind: 'out' }),
      );
      await expectError(
        firstValueFrom(records.punch({ employeeId: 2, date: DATE, time: '18:30', kind: 'out' })),
        'Conflict',
      );
      await expectError(
        firstValueFrom(records.punch({ employeeId: 999, date: DATE, time: '09:00', kind: 'in' })),
        'NotFound',
      );
    });

    it('blocks punching on approved leave and keeps weekend work as Present', async () => {
      const onLeave = (await query({ status: 'On Leave' }, 5))[0];
      await expectError(
        firstValueFrom(
          records.punch({
            employeeId: onLeave.employeeId,
            date: onLeave.date,
            time: '09:00',
            kind: 'in',
          }),
        ),
        'Conflict',
      );

      const weekend = (await query({ status: 'Weekend', employeeId: 2 }, 1))[0];
      const worked = await firstValueFrom(
        records.punch({ employeeId: 2, date: weekend.date, time: '10:00', kind: 'in' }),
      );
      expect(worked.status).toBe('Present');
      expect(worked.remarks).toContain('weekend');
    });
  });

  describe('adjustments', () => {
    const request = async (overrides: Partial<AttendanceAdjustment> = {}) => {
      const target = (await query({ employeeId: 2 }, 5)).find(
        (r) => r.status === 'Present' || r.status === 'Late',
      )!;
      return {
        target,
        model: {
          employeeId: 2,
          date: target.date,
          recordId: null,
          currentCheckIn: null,
          currentCheckOut: null,
          requestedCheckIn: '08:45',
          requestedCheckOut: '18:30',
          requestedStatus: null,
          reason: 'Biometric error',
          status: 'Pending' as const,
          requestedOn: '',
          decidedBy: null,
          decidedOn: null,
          remarks: '',
          ...overrides,
        },
      };
    };

    it('seeds pending, approved and rejected requests', async () => {
      const all = await firstValueFrom(adjustments.getAll());
      expect(all.length).toBeGreaterThanOrEqual(8);
      for (const status of ['Pending', 'Approved', 'Rejected']) {
        expect(all.some((a) => a.status === status)).toBe(true);
      }
      // Seeded approvals are already reflected in the attendance records.
      const approved = all.find((a) => a.status === 'Approved' && a.requestedCheckIn)!;
      const record = (await query({ employeeId: approved.employeeId }, 200)).find(
        (r) => r.id === approved.recordId,
      )!;
      expect(record.source).toBe('Manual');
      expect(record.checkIn).toBe(approved.requestedCheckIn);
    });

    it('creates a pending request that snapshots the current record and applies it on approval', async () => {
      const { target, model } = await request();
      const created = await firstValueFrom(adjustments.create(model));
      expect(created).toMatchObject({
        status: 'Pending',
        recordId: target.id,
        currentCheckIn: target.checkIn,
        currentCheckOut: target.checkOut,
      });

      const decided = await firstValueFrom(adjustments.decide(created.id, 'Approved', 'OK'));
      expect(decided).toMatchObject({
        status: 'Approved',
        decidedBy: 'Su Myat Noe',
        remarks: 'OK',
      });

      const updated = (await query({ employeeId: 2 }, 10)).find((r) => r.id === target.id)!;
      expect(updated).toMatchObject({
        checkIn: '08:45',
        checkOut: '18:30',
        source: 'Manual',
        status: 'Present',
      });
      expect(updated.remarks).toContain('Adjusted');
      expect(updated.overtimeMinutes).toBe(0);

      await expectError(firstValueFrom(adjustments.decide(created.id, 'Rejected', '')), 'Conflict');
      await expectError(firstValueFrom(adjustments.delete(created.id)), 'Conflict');
      await expectError(
        firstValueFrom(adjustments.update(created.id, { reason: 'changed' })),
        'Conflict',
      );
    });

    it('leaves the record unchanged when rejected', async () => {
      const { target, model } = await request({ requestedCheckIn: '07:00' });
      const created = await firstValueFrom(adjustments.create(model));
      await firstValueFrom(adjustments.decide(created.id, 'Rejected', 'No evidence'));
      const same = (await query({ employeeId: 2 }, 10)).find((r) => r.id === target.id)!;
      expect(same.checkIn).toBe(target.checkIn);
    });

    it('validates the request', async () => {
      const { model } = await request();
      const missingRecord = await expectError(
        firstValueFrom(adjustments.create({ ...model, date: '2020-01-01' })),
        'Validation',
      );
      expect(missingRecord.fieldErrors?.['date']).toBeTruthy();
      await expectError(
        firstValueFrom(
          adjustments.create({ ...model, requestedCheckIn: null, requestedCheckOut: null }),
        ),
        'Validation',
      );
      await expectError(
        firstValueFrom(
          adjustments.create({ ...model, requestedCheckIn: '18:00', requestedCheckOut: '09:00' }),
        ),
        'Validation',
      );
      await expectError(
        firstValueFrom(adjustments.create({ ...model, date: addDays(todayIso(), 2) })),
        'Validation',
      );

      await firstValueFrom(adjustments.create(model));
      const duplicate = await expectError(firstValueFrom(adjustments.create(model)), 'Validation');
      expect(duplicate.message).toContain('pending request already exists');
    });

    it('lets a pending request be edited and deleted', async () => {
      const { model } = await request();
      const created = await firstValueFrom(adjustments.create(model));
      const edited = await firstValueFrom(
        adjustments.update(created.id, { reason: 'Updated reason' }),
      );
      expect(edited.reason).toBe('Updated reason');
      await firstValueFrom(adjustments.delete(created.id));
      await expectError(firstValueFrom(adjustments.getById(created.id)), 'NotFound');
    });
  });

  describe('overtime', () => {
    const entry = (overrides: Partial<OvertimeRecord> = {}): Omit<OvertimeRecord, 'id'> => ({
      employeeId: 2,
      date: '2026-01-05',
      startTime: '18:00',
      endTime: '20:30',
      minutes: 0,
      reason: 'Month-end',
      status: 'Pending',
      requestedOn: '',
      decidedBy: null,
      decidedOn: null,
      remarks: '',
      ...overrides,
    });

    it('derives minutes, starts pending and can be approved once', async () => {
      const created = await firstValueFrom(overtime.create(entry()));
      expect(created).toMatchObject({ minutes: 150, status: 'Pending' });
      const approved = await firstValueFrom(overtime.decide(created.id, 'Approved', ''));
      expect(approved).toMatchObject({
        status: 'Approved',
        decidedBy: 'Su Myat Noe',
        remarks: 'Approved.',
      });
      await expectError(firstValueFrom(overtime.decide(created.id, 'Rejected', '')), 'Conflict');
      await expectError(firstValueFrom(overtime.delete(created.id)), 'Conflict');
    });

    it('validates duration, date and duplicates', async () => {
      await expectError(firstValueFrom(overtime.create(entry({ endTime: '18:05' }))), 'Validation');
      await expectError(
        firstValueFrom(overtime.create(entry({ startTime: '06:00', endTime: '20:00' }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(overtime.create(entry({ date: addDays(todayIso(), 1) }))),
        'Validation',
      );
      await firstValueFrom(overtime.create(entry()));
      await expectError(firstValueFrom(overtime.create(entry())), 'Validation');
    });

    it('seeds a mix of statuses from real overtime records', async () => {
      const all = await firstValueFrom(overtime.getAll());
      expect(all.length).toBeGreaterThan(20);
      expect(all.every((o) => o.minutes >= 60)).toBe(true);
      for (const status of ['Pending', 'Approved', 'Rejected']) {
        expect(all.some((o) => o.status === status)).toBe(true);
      }
    });
  });

  describe('shifts', () => {
    const newShift = {
      code: 'EVE',
      name: 'Evening',
      startTime: '12:00',
      endTime: '20:00',
      breakMinutes: 30,
      graceMinutes: 10,
      workDays: [1, 2, 3],
      isNightShift: false,
      color: '#123456',
      status: 'Active' as const,
    };

    it('validates times, working days and unique codes', async () => {
      const times = await expectError(
        firstValueFrom(shifts.create({ ...newShift, endTime: '11:00' })),
        'Validation',
      );
      expect(times.fieldErrors?.['endTime']).toBeTruthy();
      await firstValueFrom(
        shifts.create({
          ...newShift,
          code: 'NIGHT2',
          startTime: '22:00',
          endTime: '06:00',
          isNightShift: true,
        }),
      );
      await expectError(firstValueFrom(shifts.create({ ...newShift, workDays: [] })), 'Validation');
      await expectError(firstValueFrom(shifts.create({ ...newShift, code: 'GEN' })), 'Validation');
    });

    it('protects shifts that are assigned to employees', async () => {
      const error = await expectError(firstValueFrom(shifts.delete(1)), 'Conflict');
      expect(error.message).toMatch(/assigned employee\(s\)/);
      const created = await firstValueFrom(shifts.create(newShift));
      await firstValueFrom(shifts.delete(created.id));
    });

    it('reflects shift assignment on employees', async () => {
      const employees = TestBed.inject(EmployeeDataService);
      await firstValueFrom(employees.update(2, { shiftId: 2 }));
      const list = await firstValueFrom(
        employees.query({ pageIndex: 0, pageSize: 5, filters: { shiftId: 2, departmentId: 2 } }),
      );
      expect(list.items.map((e) => e.id)).toContain(2);
      expect(list.items.find((e) => e.id === 2)?.shiftName).toBe('Warehouse Morning');
    });
  });

  describe('dashboard and schedule views', () => {
    it('summarises a working day consistently', async () => {
      const day = (await query({ status: 'Present' }, 1))[0].date;
      const overview = await firstValueFrom(
        TestBed.inject(AttendanceOverviewDataService).getOverview(day, null),
      );
      const s = overview.summary;
      expect(s.present + s.late + s.halfDay + s.absent + s.onLeave).toBe(s.scheduled);
      expect(overview.byDepartment.reduce((sum, d) => sum + d.total, 0)).toBe(s.scheduled);
      expect(overview.trend.length).toBeGreaterThan(5);
      expect(overview.trend.length).toBeLessThanOrEqual(14);
      expect(overview.trend.at(-1)?.date).toBe(day);
      expect(s.attendanceRate).toBeGreaterThan(70);

      const branch = await firstValueFrom(
        TestBed.inject(AttendanceOverviewDataService).getOverview(day, 4),
      );
      expect(branch.summary.scheduled).toBeLessThan(s.scheduled);

      const weekend = await firstValueFrom(
        TestBed.inject(AttendanceOverviewDataService).getOverview(
          (await query({ status: 'Weekend' }, 1))[0].date,
          null,
        ),
      );
      expect(weekend.summary.scheduled).toBe(0);
    });

    it('builds a weekly roster with holidays, days off and leave', async () => {
      const schedule = TestBed.inject(WorkScheduleDataService);
      // 16–22 Nov 2026 contains the company holiday on Friday 20 Nov.
      const week = await firstValueFrom(
        schedule.getWeek({ weekStart: '2026-11-16', departmentId: null, branchId: null }),
      );
      expect(week.days).toHaveLength(7);
      expect(week.rows.length).toBe(58);
      expect(
        week.rows.every(
          (r) => r.cells[4].kind === 'Holiday' && r.cells[4].label === 'Company Foundation Day',
        ),
      ).toBe(true);
      expect(
        week.rows
          .find((r) => r.shiftName === 'General Shift')
          ?.cells.slice(5)
          .map((c) => c.kind),
      ).toEqual(['Off', 'Off']);
      expect(week.rows.find((r) => r.shiftName === 'Warehouse Morning')?.cells[5].kind).toBe(
        'Shift',
      );

      const thisWeek = await firstValueFrom(
        schedule.getWeek({ weekStart: mondayOf(todayIso()), departmentId: null, branchId: null }),
      );
      expect(thisWeek.rows.some((r) => r.cells.some((c) => c.kind === 'Leave'))).toBe(true);

      const hr = await firstValueFrom(
        schedule.getWeek({ weekStart: '2026-11-16', departmentId: 2, branchId: null }),
      );
      expect(hr.rows.every((r) => r.department === 'Human Resources')).toBe(true);
    });
  });
});

describe('shift time validator', () => {
  const group = (night: boolean, start: string, end: string) =>
    new FormGroup(
      {
        startTime: new FormControl(start),
        endTime: new FormControl(end),
        isNightShift: new FormControl(night),
      },
      { validators: HrmsValidators.timeOrder('startTime', 'endTime', 'isNightShift') },
    );

  it('requires end after start for day shifts only', () => {
    const day = group(false, '18:00', '09:00');
    expect(day.controls.endTime.errors).toHaveProperty('timeOrder');
    day.controls.endTime.setValue('19:00');
    expect(day.controls.endTime.errors).toBeNull();
    expect(group(true, '22:00', '06:00').controls.endTime.errors).toBeNull();
  });
});
