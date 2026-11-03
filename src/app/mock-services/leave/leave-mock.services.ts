import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PagedResult } from '../../core/models/common.models';
import { CURRENT_EMPLOYMENT_STATUSES, Employee, fullName } from '../../core/models/employee.models';
import {
  EmployeeLeaveBalances,
  LeaveApplication,
  LeaveBalance,
  LeaveBalanceQuery,
  LeaveCalendarDay,
  LeaveCalendarMonth,
  LeaveCalendarQuery,
  LeavePolicy,
  LeaveType,
} from '../../core/models/leave.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeaveDecision,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../../core/services/data/leave-data.services';
import { computeLeaveBalances, resolveEntitlement } from '../../core/utilities/leave-balance';
import { MOCK_LEAVE_APPLICATIONS } from '../../mock-data/leave/leave-applications.mock';
import { MOCK_LEAVE_POLICIES } from '../../mock-data/leave/leave-policies.mock';
import { MOCK_LEAVE_TYPES } from '../../mock-data/leave/leave-types.mock';
import {
  addDays,
  countWorkingDays,
  dayOfWeek,
  endOfMonth,
  todayIso,
} from '../../shared/utilities/date.util';
import { matchesEqualityFilters, paginate, sortItems } from '../../shared/utilities/query.util';
import {
  AttendanceRecordMockService,
  HolidayMockService,
} from '../attendance/attendance-mock.services';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { mockError, mockResponse } from '../mock-response';
import {
  BranchMockService,
  DepartmentMockService,
} from '../organization/organization-mock.services';

const DAY_STATUSES_TO_REPLACE = new Set(['Present', 'Late', 'Absent', 'Half Day']);

@Injectable({ providedIn: 'root' })
export class LeaveTypeMockService
  extends InMemoryCrudService<LeaveType>
  implements LeaveTypeDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Leave type';
  protected readonly activityCategory = 'leave';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_LEAVE_TYPES);
  }

  protected override describe(entity: LeaveType): string {
    return entity.name;
  }

  protected override validate(candidate: LeaveType): void {
    this.ensureUnique(candidate, 'code', 'Leave type code');
    if (candidate.carryForward && candidate.maxCarryForwardDays > candidate.defaultDaysPerYear) {
      this.validationError('Carry-forward days cannot exceed the yearly entitlement.', {
        maxCarryForwardDays: 'Must not exceed the yearly entitlement.',
      });
    }
    if (candidate.maxConsecutiveDays < 1) {
      this.validationError('Maximum consecutive days must be at least 1.', {
        maxConsecutiveDays: 'Must be at least 1.',
      });
    }
  }

  protected override beforeDelete(entity: LeaveType): void {
    const applications = this.injector
      .get(LeaveApplicationMockService)
      .records()
      .filter((a) => a.leaveTypeId === entity.id).length;
    const policies = this.injector
      .get(LeavePolicyMockService)
      .records()
      .filter((p) => p.leaveTypeId === entity.id).length;
    this.ensureNotReferenced(entity.name, [
      ['application(s)', applications],
      ['policy(ies)', policies],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class LeavePolicyMockService
  extends InMemoryCrudService<LeavePolicy>
  implements LeavePolicyDataService
{
  protected readonly entityLabel = 'Leave policy';
  protected readonly activityCategory = 'leave';
  protected override readonly searchFields = ['name'] as const;

  constructor() {
    super(MOCK_LEAVE_POLICIES);
  }

  protected override describe(entity: LeavePolicy): string {
    return entity.name;
  }

  protected override validate(candidate: LeavePolicy): void {
    this.ensureUnique(candidate, 'name', 'Policy name');
    const clash = this.store().some(
      (p) =>
        p.id !== candidate.id &&
        p.status === 'Active' &&
        candidate.status === 'Active' &&
        p.leaveTypeId === candidate.leaveTypeId &&
        p.jobGradeId === candidate.jobGradeId &&
        p.employmentType === candidate.employmentType,
    );
    if (clash) {
      this.validationError(
        'An active policy already exists for this leave type, grade and employment type.',
        {
          leaveTypeId: 'Another active policy covers the same group.',
        },
      );
    }
  }
}

@Injectable({ providedIn: 'root' })
export class LeaveApplicationMockService
  extends InMemoryCrudService<LeaveApplication>
  implements LeaveApplicationDataService
{
  private readonly employees = inject(EmployeeMockService);
  private readonly types = inject(LeaveTypeMockService);
  private readonly policies = inject(LeavePolicyMockService);
  private readonly holidays = inject(HolidayMockService);
  private readonly attendance = inject(AttendanceRecordMockService);
  private readonly user = inject(CurrentUserService);
  private readonly weekendDays = inject(APP_CONFIG).weekendDays;

  protected readonly entityLabel = 'Leave application';
  protected readonly activityCategory = 'leave';
  protected override readonly searchFields = ['applicationNo', 'reason'] as const;

  constructor() {
    super(MOCK_LEAVE_APPLICATIONS);
  }

  protected override describe(entity: LeaveApplication): string {
    return entity.applicationNo;
  }

  /** Filters: equality fields plus `dateFrom` / `dateTo`, matched as an overlap with the leave dates. */
  protected override matchesFilters(
    item: LeaveApplication,
    filters: Partial<Record<string, unknown>>,
  ): boolean {
    const { dateFrom, dateTo, ...equality } = filters;
    return (
      matchesEqualityFilters(item, equality) &&
      (!dateFrom || item.endDate >= String(dateFrom)) &&
      (!dateTo || item.startDate <= String(dateTo))
    );
  }

  /** Working days between two dates, excluding weekends and holidays. */
  workingDays(startDate: string, endDate: string): number {
    const holidaySet = new Set(this.holidays.records().map((h) => h.date));
    return countWorkingDays(startDate, endDate, this.weekendDays, holidaySet);
  }

  protected override prepare(candidate: LeaveApplication, isNew: boolean): void {
    if (candidate.endDate && candidate.startDate) {
      candidate.days = candidate.halfDay
        ? 0.5
        : this.workingDays(candidate.startDate, candidate.endDate);
    }
    candidate.documentName ||= null;
    if (isNew) {
      const next = this.store().reduce((m, a) => Math.max(m, a.id), 0) + 1;
      candidate.applicationNo = `LV-${todayIso().slice(0, 4)}-${String(next).padStart(4, '0')}`;
      candidate.status = 'Draft';
      candidate.appliedOn = todayIso();
      candidate.approverId = null;
      candidate.actionedOn = null;
      candidate.approverRemarks = null;
    }
  }

  protected override validate(candidate: LeaveApplication, isNew: boolean): void {
    if (!isNew && this.findById(candidate.id)?.status !== 'Draft') {
      this.conflictError('Only draft applications can be changed. Cancel a submitted one instead.');
    }
    const employee = this.employees.findById(candidate.employeeId);
    if (!employee) {
      this.validationError('The selected employee does not exist.', {
        employeeId: 'Choose an employee.',
      });
    }
    const type = this.types.findById(candidate.leaveTypeId);
    if (!type || type.status !== 'Active') {
      this.validationError('Choose an active leave type.', {
        leaveTypeId: 'Choose an active leave type.',
      });
    }
    if (type.eligibleGender && type.eligibleGender !== employee.gender) {
      this.validationError(`${type.name} is not available to this employee.`, {
        leaveTypeId: 'Not available for this employee.',
      });
    }
    if (candidate.endDate < candidate.startDate) {
      this.validationError('End date must be on or after the start date.', {
        endDate: 'Must be on or after the start date.',
      });
    }
    if (candidate.halfDay && candidate.startDate !== candidate.endDate) {
      this.validationError('A half day must start and end on the same date.', {
        endDate: 'A half day covers a single date.',
      });
    }
    if (candidate.days <= 0) {
      this.validationError('The selected dates contain no working days.', {
        endDate: 'Choose dates that include a working day.',
      });
    }
    if (candidate.startDate < employee.joiningDate) {
      this.validationError('Leave cannot start before the joining date.', {
        startDate: `Must be on or after ${employee.joiningDate}.`,
      });
    }
    if (candidate.days > type.maxConsecutiveDays) {
      this.validationError(
        `${type.name} is limited to ${type.maxConsecutiveDays} consecutive days.`,
        {
          endDate: `At most ${type.maxConsecutiveDays} days at a time.`,
        },
      );
    }
    this.ensureNoOverlap(candidate);
  }

  /** Submitted and approved leave of the same employee must not overlap. */
  private ensureNoOverlap(candidate: LeaveApplication): void {
    const clash = this.store().find(
      (a) =>
        a.id !== candidate.id &&
        a.employeeId === candidate.employeeId &&
        (a.status === 'Submitted' || a.status === 'Approved') &&
        a.startDate <= candidate.endDate &&
        a.endDate >= candidate.startDate,
    );
    if (clash) {
      this.validationError(
        `These dates overlap ${clash.applicationNo} (${clash.startDate} – ${clash.endDate}).`,
        {
          startDate: `Overlaps ${clash.applicationNo}.`,
        },
      );
    }
  }

  protected override beforeDelete(entity: LeaveApplication): void {
    if (entity.status !== 'Draft') {
      this.conflictError('Only draft applications can be deleted. Cancel a submitted one instead.');
    }
  }

  /** Days still available for a type (entitlement − approved − pending), ignoring one application. */
  availableDays(employee: Employee, type: LeaveType, excludeId: number): number {
    const mine = this.store().filter((a) => a.employeeId === employee.id && a.id !== excludeId);
    const balance = computeLeaveBalances(
      [type],
      mine,
      employee.gender,
      new Date().getFullYear(),
      (t) => resolveEntitlement(t, this.policies.records(), employee, todayIso()),
    )[0];
    return balance?.available ?? 0;
  }

  private change(id: number, changes: Partial<LeaveApplication>): LeaveApplication {
    const updated = { ...this.require(id), ...changes };
    this.store.update((items) => items.map((a) => (a.id === id ? updated : a)));
    return updated;
  }

  submit(id: number): Observable<LeaveApplication> {
    return this.respond(() => {
      const application = this.require(id);
      if (application.status !== 'Draft') {
        this.conflictError(`Only a draft can be submitted (this one is ${application.status}).`);
      }
      const employee = this.employees.findById(application.employeeId)!;
      const type = this.types.findById(application.leaveTypeId)!;
      this.ensureNoOverlap(application);
      if (type.requiresDocument && !application.documentName) {
        this.validationError(
          `${type.name} needs a supporting document before it can be submitted.`,
          {
            documentName: 'A supporting document is required.',
          },
        );
      }
      const available = this.availableDays(employee, type, id);
      if (application.days > available) {
        this.conflictError(
          `Insufficient ${type.name} balance: ${application.days} day(s) requested, ${available} available.`,
        );
      }
      const submitted = this.change(id, { status: 'Submitted', appliedOn: todayIso() });
      this.activity.record('leave', `${application.applicationNo} submitted for approval`);
      return submitted;
    });
  }

  decide(id: number, decision: LeaveDecision, remarks: string): Observable<LeaveApplication> {
    return this.respond(() => {
      const application = this.require(id);
      if (application.status !== 'Submitted') {
        this.conflictError(
          `Only submitted applications can be decided (this one is ${application.status}).`,
        );
      }
      const approver = this.user.current();
      if (approver.employeeId === application.employeeId) {
        this.conflictError('You cannot decide your own leave request.');
      }
      if (decision === 'Rejected' && !remarks.trim()) {
        this.validationError('Give a reason for rejecting the request.', {
          approverRemarks: 'A reason is required.',
        });
      }
      const decided = this.change(id, {
        status: decision,
        approverId: approver.employeeId,
        actionedOn: todayIso(),
        approverRemarks: remarks.trim() || 'Approved.',
      });
      if (decision === 'Approved') {
        this.markAttendanceOnLeave(decided);
      }
      this.activity.record('leave', `${application.applicationNo} ${decision.toLowerCase()}`);
      return decided;
    });
  }

  cancel(id: number, remarks: string): Observable<LeaveApplication> {
    return this.respond(() => {
      const application = this.require(id);
      if (application.status !== 'Submitted' && application.status !== 'Approved') {
        this.conflictError(
          `Only submitted or approved leave can be cancelled (this one is ${application.status}).`,
        );
      }
      if (application.status === 'Approved' && application.startDate <= todayIso()) {
        this.conflictError('Leave that has already started cannot be cancelled.');
      }
      const cancelled = this.change(id, {
        status: 'Cancelled',
        actionedOn: todayIso(),
        approverRemarks: remarks.trim() || 'Cancelled.',
      });
      this.activity.record('leave', `${application.applicationNo} cancelled`);
      return cancelled;
    });
  }

  /** Approved leave replaces existing attendance records for the covered working days up to today. */
  private markAttendanceOnLeave(application: LeaveApplication): void {
    const today = todayIso();
    for (const record of this.attendance.records()) {
      if (
        record.employeeId === application.employeeId &&
        record.date >= application.startDate &&
        record.date <= application.endDate &&
        record.date <= today &&
        DAY_STATUSES_TO_REPLACE.has(record.status)
      ) {
        this.attendance.patchRecord(record.id, {
          status: 'On Leave',
          checkIn: null,
          checkOut: null,
          workedMinutes: 0,
          lateMinutes: 0,
          earlyLeaveMinutes: 0,
          overtimeMinutes: 0,
          remarks: application.applicationNo,
        });
      }
    }
  }
}

@Injectable({ providedIn: 'root' })
export class LeaveBalanceMockService implements LeaveBalanceDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly branches = inject(BranchMockService);
  private readonly types = inject(LeaveTypeMockService);
  private readonly policies = inject(LeavePolicyMockService);
  private readonly applications = inject(LeaveApplicationMockService);

  private balancesFor(employee: Employee, year: number): LeaveBalance[] {
    const asOf = year === new Date().getFullYear() ? todayIso() : `${year}-12-31`;
    return computeLeaveBalances(
      this.types.records(),
      this.applications.records().filter((a) => a.employeeId === employee.id),
      employee.gender,
      year,
      (type) => resolveEntitlement(type, this.policies.records(), employee, asOf),
    );
  }

  getForEmployee(employeeId: number, year: number): Observable<LeaveBalance[]> {
    return mockResponse(() => {
      const employee = this.employees.findById(employeeId);
      if (!employee) {
        throw mockError('NotFound', `Employee #${employeeId} was not found.`);
      }
      return this.balancesFor(employee, year);
    }, this.latency);
  }

  query(request: LeaveBalanceQuery): Observable<PagedResult<EmployeeLeaveBalances>> {
    return mockResponse(() => {
      const needle = request.search.trim().toLowerCase();
      const rows: EmployeeLeaveBalances[] = this.employees
        .records()
        .filter(
          (e) =>
            CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus) &&
            (request.departmentId === null || e.departmentId === request.departmentId) &&
            (request.branchId === null || e.branchId === request.branchId) &&
            (!needle || `${fullName(e)} ${e.employeeCode}`.toLowerCase().includes(needle)),
        )
        .map((e) => ({
          employeeId: e.id,
          employeeCode: e.employeeCode,
          employeeName: fullName(e),
          departmentId: e.departmentId,
          departmentName: this.departments.findById(e.departmentId)?.name ?? '—',
          branchId: e.branchId,
          branchName: this.branches.findById(e.branchId)?.name ?? '—',
          balances: this.balancesFor(e, request.year),
        }));
      const sort = request.sort?.active
        ? request.sort
        : { active: 'employeeCode', direction: 'asc' as const };
      return paginate(sortItems(rows, sort), request.pageIndex, request.pageSize);
    }, this.latency);
  }
}

@Injectable({ providedIn: 'root' })
export class LeaveCalendarMockService implements LeaveCalendarDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly weekendDays = inject(APP_CONFIG).weekendDays;
  private readonly employees = inject(EmployeeMockService);
  private readonly types = inject(LeaveTypeMockService);
  private readonly applications = inject(LeaveApplicationMockService);
  private readonly holidays = inject(HolidayMockService);

  getMonth(query: LeaveCalendarQuery): Observable<LeaveCalendarMonth> {
    return mockResponse(() => {
      const first = `${query.month}-01`;
      const last = endOfMonth(first);
      // Whole Monday → Sunday weeks around the month.
      const gridStart = addDays(first, -((dayOfWeek(first) + 6) % 7));
      const gridEnd = addDays(last, (7 - dayOfWeek(last)) % 7);
      const holidayByDate = new Map(this.holidays.records().map((h) => [h.date, h.name]));
      const employeeById = new Map(this.employees.records().map((e) => [e.id, e]));
      const typeById = new Map(this.types.records().map((t) => [t.id, t]));

      const relevant = this.applications
        .records()
        .filter(
          (a) =>
            (a.status === 'Approved' || (query.includePending && a.status === 'Submitted')) &&
            a.startDate <= gridEnd &&
            a.endDate >= gridStart &&
            (query.leaveTypeId === null || a.leaveTypeId === query.leaveTypeId) &&
            (query.departmentId === null ||
              employeeById.get(a.employeeId)?.departmentId === query.departmentId),
        );

      const days: LeaveCalendarDay[] = [];
      for (let date = gridStart; date <= gridEnd; date = addDays(date, 1)) {
        days.push({
          date,
          inMonth: date >= first && date <= last,
          isWeekend: this.weekendDays.includes(dayOfWeek(date)),
          holiday: holidayByDate.get(date) ?? null,
          entries: relevant
            .filter((a) => date >= a.startDate && date <= a.endDate)
            .map((a) => ({
              applicationId: a.id,
              employeeId: a.employeeId,
              employeeName: employeeById.has(a.employeeId)
                ? fullName(employeeById.get(a.employeeId)!)
                : `#${a.employeeId}`,
              leaveTypeId: a.leaveTypeId,
              leaveTypeName: typeById.get(a.leaveTypeId)?.name ?? 'Leave',
              color: typeById.get(a.leaveTypeId)?.color ?? '#6b7280',
              status: a.status as 'Approved' | 'Submitted',
              halfDay: a.halfDay,
            }))
            .sort((x, y) => x.employeeName.localeCompare(y.employeeName)),
        });
      }
      return { month: query.month, days };
    }, this.latency);
  }
}
