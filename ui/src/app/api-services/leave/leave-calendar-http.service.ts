import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { isAppError } from '../../core/models/common.models';
import {
  LeaveCalendarDay,
  LeaveCalendarEntry,
  LeaveCalendarMonth,
  LeaveCalendarQuery,
} from '../../core/models/leave.models';
import { LeaveCalendarDataService } from '../../core/services/data/leave-data.services';
import { addDays, dayOfWeek, endOfMonth } from '../../shared/utilities/date.util';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

interface CalendarApiDay {
  date: string;
  isWeekend: boolean;
  holiday: string | null;
  entries: LeaveCalendarEntry[];
}

interface CalendarApi {
  from: string;
  to: string;
  days: CalendarApiDay[];
}

/**
 * HTTP implementation of the leave calendar contract. The API returns a flat range
 * (`GET /leave/calendar?from&to...`); the month grid of whole Monday to Sunday weeks, the `inMonth`
 * flag and the per-day sort by employee name are built here, exactly as the mock does.
 */
@Injectable({ providedIn: 'root' })
export class LeaveCalendarHttpService implements LeaveCalendarDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getMonth(query: LeaveCalendarQuery): Observable<LeaveCalendarMonth> {
    const first = `${query.month}-01`;
    const last = endOfMonth(first);
    const gridStart = addDays(first, -((dayOfWeek(first) + 6) % 7));
    const gridEnd = addDays(last, (7 - dayOfWeek(last)) % 7);

    let params = new HttpParams()
      .set('from', gridStart)
      .set('to', gridEnd)
      .set('includePending', query.includePending);
    if (query.departmentId !== null) {
      params = params.set('departmentId', query.departmentId);
    }
    if (query.leaveTypeId !== null) {
      params = params.set('leaveTypeId', query.leaveTypeId);
    }

    return this.http.get<CalendarApi>(`${this.baseUrl}/leave/calendar`, { params }).pipe(
      map((range) => {
        const byDate = new Map(range.days.map((d) => [d.date, d]));
        const days: LeaveCalendarDay[] = [];
        for (let date = gridStart; date <= gridEnd; date = addDays(date, 1)) {
          const day = byDate.get(date);
          days.push({
            date,
            inMonth: date >= first && date <= last,
            isWeekend: day?.isWeekend ?? false,
            holiday: day?.holiday ?? null,
            entries: [...(day?.entries ?? [])]
              .map((e) => ({
                applicationId: e.applicationId,
                employeeId: e.employeeId,
                employeeName: e.employeeName,
                leaveTypeId: e.leaveTypeId,
                leaveTypeName: e.leaveTypeName,
                color: e.color,
                status: e.status,
                halfDay: e.halfDay,
              }))
              .sort((x, y) => x.employeeName.localeCompare(y.employeeName)),
          });
        }
        return { month: query.month, days };
      }),
      catchError((e) => throwError(() => (isAppError(e) ? e : mapHttpError(e)))),
    );
  }
}
