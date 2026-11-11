import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  AttendanceException,
  AttendanceOverview,
  ScheduleQuery,
  WorkSchedule,
} from '../../core/models/attendance.models';
import { isAppError } from '../../core/models/common.models';
import {
  AttendanceOverviewDataService,
  WorkScheduleDataService,
} from '../../core/services/data/attendance-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

const failWith = (e: unknown) => throwError(() => (isAppError(e) ? e : mapHttpError(e)));

const exception = (e: AttendanceException): AttendanceException => ({
  employeeId: e.employeeId,
  employeeCode: e.employeeCode,
  name: e.name,
  department: e.department,
  detail: e.detail,
});

/**
 * HTTP implementation of the attendance overview (`GET attendance/overview`). The API shape already
 * matches the screen model; each member is copied explicitly so only the model's fields reach it.
 */
@Injectable({ providedIn: 'root' })
export class AttendanceOverviewHttpService implements AttendanceOverviewDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getOverview(date: string, branchId: number | null): Observable<AttendanceOverview> {
    let params = new HttpParams().set('date', date);
    if (branchId !== null) {
      params = params.set('branchId', branchId);
    }
    return this.http
      .get<AttendanceOverview>(`${this.baseUrl}/attendance/overview`, { params })
      .pipe(
        map((o) => ({
          summary: {
            date: o.summary.date,
            scheduled: o.summary.scheduled,
            present: o.summary.present,
            late: o.summary.late,
            halfDay: o.summary.halfDay,
            absent: o.summary.absent,
            onLeave: o.summary.onLeave,
            attendanceRate: o.summary.attendanceRate,
          },
          byDepartment: o.byDepartment.map((d) => ({
            departmentId: d.departmentId,
            department: d.department,
            present: d.present,
            late: d.late,
            halfDay: d.halfDay,
            absent: d.absent,
            onLeave: d.onLeave,
            total: d.total,
          })),
          late: o.late.map(exception),
          absent: o.absent.map(exception),
          trend: o.trend.map((t) => ({
            date: t.date,
            present: t.present,
            late: t.late,
            absent: t.absent,
            onLeave: t.onLeave,
          })),
        })),
        catchError(failWith),
      );
  }
}

/** HTTP implementation of the weekly work schedule (`GET attendance/schedule`). */
@Injectable({ providedIn: 'root' })
export class WorkScheduleHttpService implements WorkScheduleDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getWeek(query: ScheduleQuery): Observable<WorkSchedule> {
    let params = new HttpParams().set('weekStart', query.weekStart);
    if (query.departmentId !== null) {
      params = params.set('departmentId', query.departmentId);
    }
    if (query.branchId !== null) {
      params = params.set('branchId', query.branchId);
    }
    return this.http.get<WorkSchedule>(`${this.baseUrl}/attendance/schedule`, { params }).pipe(
      map((w) => ({
        weekStart: w.weekStart,
        days: [...w.days],
        rows: w.rows.map((r) => ({
          employeeId: r.employeeId,
          employeeCode: r.employeeCode,
          employeeName: r.employeeName,
          department: r.department,
          shiftName: r.shiftName,
          shiftColor: r.shiftColor,
          cells: r.cells.map((c) => ({ date: c.date, kind: c.kind, label: c.label })),
        })),
      })),
      catchError(failWith),
    );
  }
}
