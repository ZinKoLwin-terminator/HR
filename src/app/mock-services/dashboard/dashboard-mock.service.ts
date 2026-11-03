import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AttendanceRecord } from '../../core/models/attendance.models';
import {
  AttendanceTrendPoint,
  CountByLabel,
  DashboardSummary,
  HeadcountGrowthPoint,
  PayrollSummary,
  PendingLeaveItem,
  TodayAttendanceSummary,
  UpcomingEvent,
} from '../../core/models/dashboard.models';
import { CURRENT_EMPLOYMENT_STATUSES, Employee, fullName } from '../../core/models/employee.models';
import { DashboardDataService } from '../../core/services/data/dashboard-data.service';
import {
  addDays,
  addMonths,
  endOfMonth,
  startOfMonth,
  todayIso,
  yearsBetween,
} from '../../shared/utilities/date.util';
import { ActivityMockService } from '../activity/activity-mock.service';
import {
  AttendanceRecordMockService,
  HolidayMockService,
} from '../attendance/attendance-mock.services';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { LeaveApplicationMockService, LeaveTypeMockService } from '../leave/leave-mock.services';
import { mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import { PayrollRunMockService } from '../payroll/payroll-mock.services';

const NON_WORKING = new Set(['Weekend', 'Holiday']);
const UPCOMING_WINDOW_DAYS = 30;

/** Builds the dashboard from the live in-memory stores so it reflects CRUD changes. */
@Injectable({ providedIn: 'root' })
export class DashboardMockService implements DashboardDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly attendance = inject(AttendanceRecordMockService);
  private readonly holidays = inject(HolidayMockService);
  private readonly leaves = inject(LeaveApplicationMockService);
  private readonly leaveTypes = inject(LeaveTypeMockService);
  private readonly payrollRuns = inject(PayrollRunMockService);
  private readonly activity = inject(ActivityMockService);

  getSummary(): Observable<DashboardSummary> {
    return mockResponse(() => this.buildSummary(), this.latency);
  }

  private buildSummary(): DashboardSummary {
    const today = todayIso();
    const employees = this.employees.records();
    const current = employees.filter((e) =>
      CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus),
    );
    const attendanceToday = this.todayAttendance(today);
    const pending = this.leaves
      .records()
      .filter((l) => l.status === 'Submitted')
      .sort((a, b) => a.startDate.localeCompare(b.startDate));
    const monthStart = startOfMonth(today);

    return {
      generatedAt: new Date().toISOString(),
      headcount: {
        total: current.length,
        active: current.filter((e) => e.employmentStatus === 'Active').length,
        probation: current.filter((e) => e.employmentStatus === 'Probation').length,
        newThisMonth: employees.filter(
          (e) => e.joiningDate >= addDays(today, -30) && e.joiningDate <= today,
        ).length,
        onLeaveToday: attendanceToday.onLeave,
        exitsThisMonth: employees.filter(
          (e) => e.exitDate && e.exitDate >= monthStart && e.exitDate <= today,
        ).length,
      },
      attendanceToday,
      pendingLeaveApprovals: pending.length,
      pendingLeaves: pending.slice(0, 5).map((l) => this.toPendingItem(l.id)),
      payroll: this.payrollSummary(),
      departmentDistribution: this.departmentDistribution(current),
      attendanceTrend: this.attendanceTrend(today, 14),
      headcountGrowth: this.headcountGrowth(today, employees),
      recentActivities: this.activity.snapshot(6),
      upcomingEvents: this.upcomingEvents(today, current),
    };
  }

  private todayAttendance(today: string): TodayAttendanceSummary {
    const all = this.attendance.records();
    // On non-working days fall back to the most recent working day.
    const workingDates = [
      ...new Set(all.filter((r) => !NON_WORKING.has(r.status)).map((r) => r.date)),
    ]
      .filter((d) => d <= today)
      .sort();
    const date = workingDates.at(-1) ?? today;
    const records = all.filter((r) => r.date === date && !NON_WORKING.has(r.status));
    const count = (status: AttendanceRecord['status']) =>
      records.filter((r) => r.status === status).length;
    const present = count('Present');
    const late = count('Late');
    const halfDay = count('Half Day');
    const scheduled = records.length;
    return {
      date,
      scheduled,
      present,
      late,
      halfDay,
      absent: count('Absent'),
      onLeave: count('On Leave'),
      attendanceRate: scheduled
        ? Math.round(((present + late + halfDay) / scheduled) * 1000) / 10
        : 0,
    };
  }

  private toPendingItem(id: number): PendingLeaveItem {
    const leave = this.leaves.findById(id)!;
    const employee = this.employees.findById(leave.employeeId);
    return {
      id: leave.id,
      employeeName: employee ? fullName(employee) : `Employee #${leave.employeeId}`,
      leaveTypeName: this.leaveTypes.findById(leave.leaveTypeId)?.name ?? 'Leave',
      startDate: leave.startDate,
      endDate: leave.endDate,
      days: leave.days,
      appliedOn: leave.appliedOn,
    };
  }

  private payrollSummary(): PayrollSummary | null {
    // Only periods that have been processed carry figures (a draft has no payslips yet).
    const runs = [...this.payrollRuns.records()]
      .filter((r) => r.status !== 'Draft')
      .sort((a, b) => a.startDate.localeCompare(b.startDate));
    const latest = runs.at(-1);
    if (!latest) {
      return null;
    }
    const previous = runs.at(-2);
    const change = previous?.totalNet
      ? ((latest.totalNet - previous.totalNet) / previous.totalNet) * 100
      : 0;
    return {
      periodName: latest.periodName,
      status: latest.status,
      payDate: latest.payDate,
      employeeCount: latest.employeeCount,
      totalGross: latest.totalGross,
      totalDeductions: latest.totalDeductions,
      totalNet: latest.totalNet,
      netChangePercent: Math.round(change * 10) / 10,
    };
  }

  private departmentDistribution(current: Employee[]): CountByLabel[] {
    const counts = new Map<number, number>();
    for (const e of current) {
      counts.set(e.departmentId, (counts.get(e.departmentId) ?? 0) + 1);
    }
    return [...counts.entries()]
      .map(([id, count]) => ({
        label: this.departments.findById(id)?.name ?? `Department #${id}`,
        count,
      }))
      .sort((a, b) => b.count - a.count);
  }

  private attendanceTrend(today: string, days: number): AttendanceTrendPoint[] {
    const byDate = new Map<string, AttendanceTrendPoint>();
    for (const r of this.attendance.records()) {
      if (r.date > today || NON_WORKING.has(r.status)) {
        continue;
      }
      const point = byDate.get(r.date) ?? {
        date: r.date,
        present: 0,
        late: 0,
        absent: 0,
        onLeave: 0,
      };
      if (r.status === 'Present' || r.status === 'Half Day') point.present++;
      else if (r.status === 'Late') point.late++;
      else if (r.status === 'Absent') point.absent++;
      else if (r.status === 'On Leave') point.onLeave++;
      byDate.set(r.date, point);
    }
    return (
      [...byDate.values()]
        .sort((a, b) => a.date.localeCompare(b.date))
        // Ignore lightly-staffed days (e.g. Saturday rotations) so the trend compares like with like.
        .filter((p) => p.present + p.late + p.absent + p.onLeave >= 20)
        .slice(-days)
    );
  }

  private headcountGrowth(today: string, employees: Employee[]): HeadcountGrowthPoint[] {
    const points: HeadcountGrowthPoint[] = [];
    for (let offset = -11; offset <= 0; offset++) {
      const from = addMonths(startOfMonth(today), offset);
      const to = endOfMonth(from);
      points.push({
        month: from.slice(0, 7),
        headcount: employees.filter((e) => e.joiningDate <= to && (!e.exitDate || e.exitDate > to))
          .length,
        joiners: employees.filter((e) => e.joiningDate >= from && e.joiningDate <= to).length,
        exits: employees.filter((e) => e.exitDate && e.exitDate >= from && e.exitDate <= to).length,
      });
    }
    return points;
  }

  private upcomingEvents(today: string, current: Employee[]): UpcomingEvent[] {
    const until = addDays(today, UPCOMING_WINDOW_DAYS);
    const inWindow = (d: string) => d >= today && d <= until;
    /** Next occurrence (this year or next) of a recurring month-day. */
    const nextOccurrence = (date: string) => {
      const thisYear = `${today.slice(0, 4)}${date.slice(4, 10)}`;
      return thisYear >= today ? thisYear : `${Number(today.slice(0, 4)) + 1}${date.slice(4, 10)}`;
    };
    const events: UpcomingEvent[] = [];

    for (const h of this.holidays.records()) {
      if (inWindow(h.date)) {
        events.push({
          date: h.date,
          title: h.name,
          subtitle: `${h.type} holiday`,
          type: 'Holiday',
        });
      }
    }
    for (const e of current) {
      const birthday = nextOccurrence(e.dateOfBirth);
      if (inWindow(birthday)) {
        events.push({ date: birthday, title: fullName(e), subtitle: 'Birthday', type: 'Birthday' });
      }
      const anniversary = nextOccurrence(e.joiningDate);
      const years = yearsBetween(e.joiningDate, anniversary);
      if (years >= 1 && inWindow(anniversary)) {
        events.push({
          date: anniversary,
          title: fullName(e),
          subtitle: `${years} year${years > 1 ? 's' : ''} with the company`,
          type: 'Work Anniversary',
        });
      }
      if (
        e.employmentStatus === 'Probation' &&
        e.probationEndDate &&
        inWindow(e.probationEndDate)
      ) {
        events.push({
          date: e.probationEndDate,
          title: fullName(e),
          subtitle: 'Probation review due',
          type: 'Probation',
        });
      }
    }
    for (const l of this.leaves.records()) {
      if (l.status === 'Approved' && l.startDate > today && inWindow(l.startDate)) {
        const employee = this.employees.findById(l.employeeId);
        events.push({
          date: l.startDate,
          title: employee ? fullName(employee) : `Employee #${l.employeeId}`,
          subtitle: `${this.leaveTypes.findById(l.leaveTypeId)?.name ?? 'Leave'} · ${l.days} day(s)`,
          type: 'Leave',
        });
      }
    }
    return events.sort((a, b) => a.date.localeCompare(b.date)).slice(0, 10);
  }
}
