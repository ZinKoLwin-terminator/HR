import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttendanceFilter,
  AttendanceListItem,
  AttendanceRecord,
  Holiday,
  PunchRequest,
  Shift,
} from '../../core/models/attendance.models';
import { fullName } from '../../core/models/employee.models';
import {
  AttendanceRecordDataService,
  HolidayDataService,
  ShiftDataService,
} from '../../core/services/data/attendance-data.services';
import { deriveAttendance } from '../../core/utilities/attendance-calc';
import { MOCK_ATTENDANCE_RECORDS } from '../../mock-data/attendance/attendance-records.mock';
import { MOCK_HOLIDAYS } from '../../mock-data/attendance/holidays.mock';
import { MOCK_SHIFTS } from '../../mock-data/attendance/shifts.mock';
import { isDateInRange } from '../../shared/utilities/date.util';
import { matchesEqualityFilters } from '../../shared/utilities/query.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { mockError } from '../mock-response';
import {
  BranchMockService,
  DepartmentMockService,
} from '../organization/organization-mock.services';

@Injectable({ providedIn: 'root' })
export class ShiftMockService extends InMemoryCrudService<Shift> implements ShiftDataService {
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Shift';
  protected readonly activityCategory = 'attendance';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_SHIFTS, 'shifts');
  }

  protected override describe(entity: Shift): string {
    return entity.name;
  }

  protected override validate(candidate: Shift): void {
    this.ensureUnique(candidate, 'code', 'Shift code');
    if (!candidate.isNightShift && candidate.endTime <= candidate.startTime) {
      this.validationError('The end time must be after the start time for a day shift.', {
        endTime: 'Must be after the start time (or mark the shift as a night shift).',
      });
    }
    if (!candidate.workDays?.length) {
      this.validationError('Choose at least one working day.', {
        workDays: 'Choose at least one day.',
      });
    }
  }

  protected override beforeDelete(entity: Shift): void {
    const assigned = this.injector
      .get(EmployeeMockService)
      .records()
      .filter((e) => e.shiftId === entity.id).length;
    this.ensureNotReferenced(entity.name, [['assigned employee(s)', assigned]]);
  }
}

@Injectable({ providedIn: 'root' })
export class HolidayMockService extends InMemoryCrudService<Holiday> implements HolidayDataService {
  protected readonly entityLabel = 'Holiday';
  protected readonly activityCategory = 'attendance';
  protected override readonly searchFields = ['name'] as const;

  constructor() {
    super(MOCK_HOLIDAYS, 'holidays');
  }

  protected override describe(entity: Holiday): string {
    return `${entity.name} (${entity.date})`;
  }
}

@Injectable({ providedIn: 'root' })
export class AttendanceRecordMockService
  extends InMemoryCrudService<AttendanceRecord, AttendanceListItem, AttendanceFilter>
  implements AttendanceRecordDataService
{
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly branches = inject(BranchMockService);
  private readonly shifts = inject(ShiftMockService);

  protected readonly entityLabel = 'Attendance record';
  protected readonly activityCategory = 'attendance';
  protected override readonly searchFields = ['employeeName', 'employeeCode'] as const;

  constructor() {
    super(MOCK_ATTENDANCE_RECORDS, 'attendance_records');
  }

  protected override describe(entity: AttendanceRecord): string {
    const employee = this.employees.findById(entity.employeeId);
    return `${employee ? fullName(employee) : `employee #${entity.employeeId}`} · ${entity.date}`;
  }

  protected override toListItem(r: AttendanceRecord): AttendanceListItem {
    const employee = this.employees.findById(r.employeeId);
    const department = employee ? this.departments.findById(employee.departmentId) : undefined;
    const branch = employee ? this.branches.findById(employee.branchId) : undefined;
    return {
      ...r,
      employeeCode: employee?.employeeCode ?? '—',
      employeeName: employee ? fullName(employee) : `Employee #${r.employeeId}`,
      departmentId: employee?.departmentId ?? 0,
      departmentName: department?.name ?? '—',
      branchId: employee?.branchId ?? 0,
      branchName: branch?.name ?? '—',
      shiftName: (r.shiftId ? this.shifts.findById(r.shiftId)?.name : undefined) ?? '—',
    };
  }

  protected override matchesFilters(
    item: AttendanceListItem,
    filters: Partial<AttendanceFilter>,
  ): boolean {
    const { dateFrom, dateTo, lateOnly, earlyOnly, overtimeOnly, ...equality } = filters;
    return (
      matchesEqualityFilters(item, equality) &&
      isDateInRange(item.date, dateFrom, dateTo) &&
      (!lateOnly || item.lateMinutes > 0) &&
      (!earlyOnly || item.earlyLeaveMinutes > 0) &&
      (!overtimeOnly || item.overtimeMinutes > 0)
    );
  }

  /** Synchronous in-place change used by other mock services (e.g. applying an adjustment). */
  patchRecord(id: number, changes: Partial<AttendanceRecord>): void {
    this.store.update((items) => items.map((r) => (r.id === id ? { ...r, ...changes } : r)));
  }

  punch(request: PunchRequest): Observable<AttendanceRecord> {
    return this.respond(() => {
      const employee = this.employees.findById(request.employeeId);
      if (!employee) {
        throw mockError('NotFound', `Employee #${request.employeeId} was not found.`);
      }
      const shift = this.shifts.findById(employee.shiftId ?? -1) ?? this.shifts.records()[0];
      let record = this.store().find(
        (r) => r.employeeId === request.employeeId && r.date === request.date,
      );
      if (!record) {
        record = {
          id: this.nextId(),
          employeeId: request.employeeId,
          date: request.date,
          shiftId: shift.id,
          checkIn: null,
          checkOut: null,
          workedMinutes: 0,
          lateMinutes: 0,
          earlyLeaveMinutes: 0,
          overtimeMinutes: 0,
          status: 'Absent',
          source: 'Web',
          remarks: '',
        };
        this.store.update((items) => [...items, record!]);
      }
      if (record.status === 'On Leave') {
        throw mockError('Conflict', 'You are on approved leave on this day.');
      }

      const nonWorking = record.status === 'Weekend' || record.status === 'Holiday';
      let changes: Partial<AttendanceRecord>;
      if (request.kind === 'in') {
        if (record.checkIn) {
          throw mockError('Conflict', `Already checked in at ${record.checkIn}.`);
        }
        const derived = deriveAttendance(shift, request.time, null);
        changes = {
          ...derived,
          checkIn: request.time,
          source: 'Web',
          remarks: nonWorking
            ? `Worked on a ${record.status.toLowerCase()}`
            : derived.status === 'Late'
              ? 'Late arrival'
              : '',
        };
      } else {
        if (!record.checkIn) {
          throw mockError('Conflict', 'Check in before checking out.');
        }
        if (record.checkOut) {
          throw mockError('Conflict', `Already checked out at ${record.checkOut}.`);
        }
        if (!shift.isNightShift && request.time <= record.checkIn) {
          throw mockError('Validation', 'The check-out time must be after the check-in time.');
        }
        const derived = deriveAttendance(shift, record.checkIn, request.time);
        changes = { ...derived, checkOut: request.time };
      }
      if (nonWorking) {
        changes = { ...changes, status: 'Present', lateMinutes: 0, earlyLeaveMinutes: 0 };
      }
      const updated = { ...record, ...changes };
      this.store.update((items) => items.map((r) => (r.id === updated.id ? updated : r)));
      this.activity.record(
        'attendance',
        `${fullName(employee)} checked ${request.kind === 'in' ? 'in' : 'out'} at ${request.time}`,
      );
      return updated;
    });
  }
}
