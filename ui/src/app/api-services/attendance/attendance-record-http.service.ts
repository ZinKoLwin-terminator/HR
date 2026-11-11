import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  AttendanceListItem,
  AttendanceRecord,
  PunchRequest,
} from '../../core/models/attendance.models';
import { AttendanceRecordDataService } from '../../core/services/data/attendance-data.services';
import { AttendanceQueryHttpBase, notSupported } from './attendance-query-http.base';

const READ_ONLY =
  'Attendance records are made by punches and corrected by adjustment requests; they cannot be edited directly.';

/**
 * HTTP implementation of the attendance record contract. Records are READ-ONLY on the API:
 * `create`, `update` and `delete` are refused here and no request is sent. The only write is
 * `punch`, which sends just `{ employeeId, kind }`: the server stamps the date and time.
 */
@Injectable({ providedIn: 'root' })
export class AttendanceRecordHttpService
  extends AttendanceQueryHttpBase<AttendanceListItem>
  implements AttendanceRecordDataService
{
  protected readonly resource = 'attendance-records';
  protected readonly filterKeys = [
    'employeeId',
    'departmentId',
    'branchId',
    'status',
    'dateFrom',
    'dateTo',
    'lateOnly',
    'earlyOnly',
    'overtimeOnly',
  ] as const;
  /** The API cannot sort by employee, department, branch or shift name, or by source. */
  protected readonly sortKeys = [
    'id',
    'date',
    'employeeId',
    'checkIn',
    'checkOut',
    'workedMinutes',
    'lateMinutes',
    'earlyLeaveMinutes',
    'overtimeMinutes',
    'status',
  ] as const;

  create(): Observable<AttendanceRecord> {
    return throwError(() => notSupported(READ_ONLY));
  }

  update(): Observable<AttendanceRecord> {
    return throwError(() => notSupported(READ_ONLY));
  }

  delete(): Observable<void> {
    return throwError(() => notSupported(READ_ONLY));
  }

  punch(request: PunchRequest): Observable<AttendanceRecord> {
    // Date and time are never sent: the server uses its own clock.
    const body = {
      employeeId: request.employeeId,
      kind: request.kind === 'in' ? 'CheckIn' : 'CheckOut',
    };
    return this.http
      .post<AttendanceRecord>(`${this.baseUrl}/attendance/punches`, body)
      .pipe(catchError((e) => this.fail(e)));
  }
}
