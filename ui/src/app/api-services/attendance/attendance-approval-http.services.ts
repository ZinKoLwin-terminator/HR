import { Injectable } from '@angular/core';
import { Observable, catchError, tap, throwError } from 'rxjs';
import {
  ApprovalStatus,
  AttendanceAdjustment,
  OvertimeRecord,
} from '../../core/models/attendance.models';
import { CreateModel, Entity } from '../../core/models/common.models';
import {
  AttendanceAdjustmentDataService,
  OvertimeDataService,
} from '../../core/services/data/attendance-data.services';
import {
  AttendanceQueryHttpBase,
  hhmm,
  notSupported,
  remarksRequired,
} from './attendance-query-http.base';

type Decision = Exclude<ApprovalStatus, 'Pending'>;
interface RowVersioned {
  rowVersion?: string;
}

/**
 * Adjustment requests and overtime entries share one workflow on the API: create, delete while
 * Pending, and approve/reject commands sent with `{ rowVersion, remarks }`. There is no update.
 * A rejection without remarks is refused locally with a field error on `remarks`.
 */
abstract class ApprovalHttpBase<T extends Entity> extends AttendanceQueryHttpBase<T> {
  protected abstract readonly noun: string;
  protected abstract createBody(model: Record<string, unknown>): Record<string, unknown>;

  create(model: CreateModel<T>): Observable<T> {
    return this.http
      .post<T & RowVersioned>(this.url, this.createBody(model as Record<string, unknown>))
      .pipe(
        tap((item) => this.remember(item)),
        catchError((e) => this.fail(e)),
      );
  }

  update(): Observable<T> {
    return throwError(() =>
      notSupported(
        `A ${this.noun} cannot be edited. Delete the pending request and file a new one.`,
      ),
    );
  }

  delete(id: number): Observable<void> {
    return this.removeById(id);
  }

  decide(id: number, decision: Decision, remarks: string): Observable<T> {
    const text = remarks.trim();
    if (decision === 'Rejected') {
      return text ? this.command(id, 'reject', text) : throwError(() => remarksRequired());
    }
    return this.command(id, 'approve', text || undefined);
  }
}

@Injectable({ providedIn: 'root' })
export class AttendanceAdjustmentHttpService
  extends ApprovalHttpBase<AttendanceAdjustment>
  implements AttendanceAdjustmentDataService
{
  protected readonly resource = 'attendance-adjustments';
  protected readonly noun = 'adjustment request';
  protected readonly filterKeys = ['employeeId', 'status', 'dateFrom', 'dateTo'] as const;
  protected readonly sortKeys = [
    'id',
    'employeeId',
    'date',
    'status',
    'requestedOn',
    'decidedOn',
  ] as const;

  /** Only the create fields. There is no recordId: the record is the one of employee and date. */
  protected createBody(model: Record<string, unknown>): Record<string, unknown> {
    return {
      employeeId: model['employeeId'],
      date: model['date'],
      requestedCheckIn: hhmm(model['requestedCheckIn']),
      requestedCheckOut: hhmm(model['requestedCheckOut']),
      requestedStatus: model['requestedStatus'] || null,
      reason: model['reason'],
    };
  }
}

@Injectable({ providedIn: 'root' })
export class OvertimeHttpService
  extends ApprovalHttpBase<OvertimeRecord>
  implements OvertimeDataService
{
  protected readonly resource = 'overtime-records';
  protected readonly noun = 'overtime entry';
  protected readonly filterKeys = ['employeeId', 'status', 'dateFrom', 'dateTo'] as const;
  protected readonly sortKeys = [
    'id',
    'employeeId',
    'date',
    'minutes',
    'status',
    'requestedOn',
  ] as const;

  /** Minutes are derived by the server from the two times, so they are not sent. */
  protected createBody(model: Record<string, unknown>): Record<string, unknown> {
    return {
      employeeId: model['employeeId'],
      date: model['date'],
      startTime: hhmm(model['startTime']),
      endTime: hhmm(model['endTime']),
      reason: model['reason'],
    };
  }
}
