import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import {
  AttendanceException,
  AttendanceOverview,
  AttendanceRecord,
  DepartmentAttendance,
  ScheduleCell,
  ScheduleQuery,
  WorkSchedule,
} from '../../core/models/attendance.models';
import { CURRENT_EMPLOYMENT_STATUSES, fullName } from '../../core/models/employee.models';
import {
  AttendanceOverviewDataService,
  WorkScheduleDataService,
} from '../../core/services/data/attendance-data.services';
import { addDays, dayOfWeek } from '../../shared/utilities/date.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { LeaveApplicationMockService } from '../leave/leave-mock.services';
import { mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import {
  AttendanceRecordMockService,
  HolidayMockService,
  ShiftMockService,
} from './attendance-mock.services';

const NON_WORKING = new Set(['Weekend', 'Holiday']);
const TREND_DAYS = 14;

/** Day-level attendance dashboard figures, computed from the live record store. */
@Injectable({ providedIn: 'root' })
export class AttendanceOverviewMockService implements AttendanceOverviewDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly records = inject(AttendanceRecordMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);

  getOverview(date: string, branchId: number | null): Observable<AttendanceOverview> {
    return mockResponse(() => {
      const employeeById = new Map(this.employees.records().map((e) => [e.id, e]));
      const inScope = (r: AttendanceRecord) => {
        const e = employeeById.get(r.employeeId);
        return !!e && (branchId === null || e.branchId === branchId);
      };
      const all = this.records.records().filter(inScope);

      const day = all.filter((r) => r.date === date && !NON_WORKING.has(r.status));
      const count = (status: AttendanceRecord['status']) =>
        day.filter((r) => r.status === status).length;
      const present = count('Present');
      const late = count('Late');
      const halfDay = count('Half Day');

      const byDept = new Map<number, DepartmentAttendance>();
      for (const r of day) {
        const e = employeeById.get(r.employeeId)!;
        const row =
          byDept.get(e.departmentId) ??
          ({
            departmentId: e.departmentId,
            department:
              this.departments.findById(e.departmentId)?.name ?? `Department #${e.departmentId}`,
            present: 0,
            late: 0,
            halfDay: 0,
            absent: 0,
            onLeave: 0,
            total: 0,
          } satisfies DepartmentAttendance);
        row.total++;
        if (r.status === 'Present') row.present++;
        else if (r.status === 'Late') row.late++;
        else if (r.status === 'Half Day') row.halfDay++;
        else if (r.status === 'Absent') row.absent++;
        else if (r.status === 'On Leave') row.onLeave++;
        byDept.set(e.departmentId, row);
      }

      const toException = (r: AttendanceRecord, detail: string): AttendanceException => {
        const e = employeeById.get(r.employeeId)!;
        return {
          employeeId: e.id,
          employeeCode: e.employeeCode,
          name: fullName(e),
          department: this.departments.findById(e.departmentId)?.name ?? '—',
          detail,
        };
      };

      // Trend: working days up to `date`; days with far fewer scheduled staff (rotations) are skipped.
      const perDay = new Map<
        string,
        { present: number; late: number; absent: number; onLeave: number }
      >();
      for (const r of all) {
        if (r.date > date || NON_WORKING.has(r.status)) continue;
        const p = perDay.get(r.date) ?? { present: 0, late: 0, absent: 0, onLeave: 0 };
        if (r.status === 'Present' || r.status === 'Half Day') p.present++;
        else if (r.status === 'Late') p.late++;
        else if (r.status === 'Absent') p.absent++;
        else if (r.status === 'On Leave') p.onLeave++;
        perDay.set(r.date, p);
      }
      const totals = [...perDay.values()].map((p) => p.present + p.late + p.absent + p.onLeave);
      const peak = Math.max(0, ...totals);
      const trend = [...perDay.entries()]
        .filter(([, p]) => p.present + p.late + p.absent + p.onLeave >= peak * 0.5)
        .sort(([a], [b]) => a.localeCompare(b))
        .slice(-TREND_DAYS)
        .map(([d, p]) => ({ date: d, ...p }));

      return {
        summary: {
          date,
          scheduled: day.length,
          present,
          late,
          halfDay,
          absent: count('Absent'),
          onLeave: count('On Leave'),
          attendanceRate: day.length
            ? Math.round(((present + late + halfDay) / day.length) * 1000) / 10
            : 0,
        },
        byDepartment: [...byDept.values()].sort((a, b) => b.total - a.total),
        late: day
          .filter((r) => r.status === 'Late')
          .sort((a, b) => b.lateMinutes - a.lateMinutes)
          .slice(0, 8)
          .map((r) => toException(r, `Checked in ${r.checkIn} · ${r.lateMinutes} min late`)),
        absent: day
          .filter((r) => r.status === 'Absent')
          .slice(0, 8)
          .map((r) => toException(r, 'No check-in recorded')),
        trend,
      };
    }, this.latency);
  }
}

/** Weekly roster: which shift each employee works on each day, with holidays and leave. */
@Injectable({ providedIn: 'root' })
export class WorkScheduleMockService implements WorkScheduleDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly shifts = inject(ShiftMockService);
  private readonly holidays = inject(HolidayMockService);
  private readonly leaves = inject(LeaveApplicationMockService);

  getWeek(query: ScheduleQuery): Observable<WorkSchedule> {
    return mockResponse(() => {
      const days = Array.from({ length: 7 }, (_, i) => addDays(query.weekStart, i));
      const holidayByDate = new Map(this.holidays.records().map((h) => [h.date, h.name]));
      const approvedLeaves = this.leaves.records().filter((l) => l.status === 'Approved');
      const fallbackShift = this.shifts.records()[0];

      const rows = this.employees
        .records()
        .filter(
          (e) =>
            CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus) &&
            (query.departmentId === null || e.departmentId === query.departmentId) &&
            (query.branchId === null || e.branchId === query.branchId),
        )
        .map((e) => {
          const shift = this.shifts.findById(e.shiftId ?? -1) ?? fallbackShift;
          const cells: ScheduleCell[] = days.map((date) => {
            const holiday = holidayByDate.get(date);
            if (holiday) return { date, kind: 'Holiday', label: holiday };
            if (
              approvedLeaves.some(
                (l) => l.employeeId === e.id && date >= l.startDate && date <= l.endDate,
              )
            ) {
              return { date, kind: 'Leave', label: 'On leave' };
            }
            return shift.workDays.includes(dayOfWeek(date))
              ? { date, kind: 'Shift', label: `${shift.startTime}–${shift.endTime}` }
              : { date, kind: 'Off', label: 'Off' };
          });
          return {
            employeeId: e.id,
            employeeCode: e.employeeCode,
            employeeName: fullName(e),
            department: this.departments.findById(e.departmentId)?.name ?? '—',
            shiftName: shift.name,
            shiftColor: shift.color,
            cells,
          };
        })
        .sort(
          (a, b) =>
            a.department.localeCompare(b.department) ||
            a.employeeName.localeCompare(b.employeeName),
        );

      return { weekStart: query.weekStart, days, rows };
    }, this.latency);
  }
}
