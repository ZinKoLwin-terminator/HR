import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApprovalStatus,
  AttendanceAdjustment,
  OvertimeRecord,
} from '../../core/models/attendance.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import {
  AttendanceAdjustmentDataService,
  OvertimeDataService,
} from '../../core/services/data/attendance-data.services';
import { deriveAttendance, minutesBetween } from '../../core/utilities/attendance-calc';
import { MOCK_ATTENDANCE_ADJUSTMENTS } from '../../mock-data/attendance/adjustments.mock';
import { MOCK_OVERTIME_RECORDS } from '../../mock-data/attendance/overtime.mock';
import { todayIso } from '../../shared/utilities/date.util';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { AttendanceRecordMockService, ShiftMockService } from './attendance-mock.services';

type Decision = Exclude<ApprovalStatus, 'Pending'>;

@Injectable({ providedIn: 'root' })
export class AttendanceAdjustmentMockService
  extends InMemoryCrudService<AttendanceAdjustment>
  implements AttendanceAdjustmentDataService
{
  private readonly attendance = inject(AttendanceRecordMockService);
  private readonly shifts = inject(ShiftMockService);
  private readonly user = inject(CurrentUserService);

  protected readonly entityLabel = 'Adjustment request';
  protected readonly activityCategory = 'attendance';
  protected override readonly searchFields = ['reason'] as const;

  constructor() {
    super(MOCK_ATTENDANCE_ADJUSTMENTS, 'attendance_adjustments');
    // Seeded approvals are already reflected in the attendance records.
    for (const adjustment of this.store().filter((a) => a.status === 'Approved')) {
      this.applyToRecord(adjustment);
    }
  }

  protected override describe(entity: AttendanceAdjustment): string {
    return `ADJ-${String(entity.id).padStart(4, '0')}`;
  }

  protected override prepare(candidate: AttendanceAdjustment, isNew: boolean): void {
    const record = this.attendance
      .records()
      .find((r) => r.employeeId === candidate.employeeId && r.date === candidate.date);
    candidate.recordId = record?.id ?? null;
    candidate.currentCheckIn = record?.checkIn ?? null;
    candidate.currentCheckOut = record?.checkOut ?? null;
    if (isNew) {
      candidate.status = 'Pending';
      candidate.requestedOn = todayIso();
      candidate.decidedBy = null;
      candidate.decidedOn = null;
      candidate.remarks = '';
    } else {
      // The form sends placeholders for server-owned fields; keep what was stored.
      candidate.requestedOn = this.findById(candidate.id)?.requestedOn ?? candidate.requestedOn;
    }
    candidate.requestedCheckIn ||= null;
    candidate.requestedCheckOut ||= null;
  }

  protected override validate(candidate: AttendanceAdjustment, isNew: boolean): void {
    if (!isNew && this.findById(candidate.id)?.status !== 'Pending') {
      this.conflictError('Only pending requests can be changed.');
    }
    if (candidate.date > todayIso()) {
      this.validationError('The date cannot be in the future.', {
        date: 'Choose today or an earlier date.',
      });
    }
    if (candidate.recordId === null) {
      this.validationError('There is no attendance record for this employee on that date.', {
        date: 'No attendance record exists for this date.',
      });
    }
    if (!candidate.requestedCheckIn && !candidate.requestedCheckOut && !candidate.requestedStatus) {
      this.validationError('Request at least one change: a time or a status.', {
        requestedCheckIn: 'Enter a time or choose a status.',
      });
    }
    const record = this.attendance.findById(candidate.recordId ?? -1);
    const shift = this.shifts.findById(record?.shiftId ?? -1);
    const checkIn = candidate.requestedCheckIn ?? candidate.currentCheckIn;
    const checkOut = candidate.requestedCheckOut ?? candidate.currentCheckOut;
    if (checkIn && checkOut && !shift?.isNightShift && checkOut <= checkIn) {
      this.validationError('The check-out time must be after the check-in time.', {
        requestedCheckOut: 'Must be after the check-in time.',
      });
    }
    const duplicate = this.store().some(
      (a) =>
        a.id !== candidate.id &&
        a.status === 'Pending' &&
        a.employeeId === candidate.employeeId &&
        a.date === candidate.date,
    );
    if (duplicate) {
      this.validationError('A pending request already exists for this employee and date.', {
        date: 'A pending request already exists for this date.',
      });
    }
  }

  protected override beforeDelete(entity: AttendanceAdjustment): void {
    if (entity.status !== 'Pending') {
      this.conflictError('Only pending requests can be deleted.');
    }
  }

  decide(id: number, decision: Decision, remarks: string): Observable<AttendanceAdjustment> {
    return this.respond(() => {
      const request = this.require(id);
      if (request.status !== 'Pending') {
        this.conflictError(`This request was already ${request.status.toLowerCase()}.`);
      }
      const decided: AttendanceAdjustment = {
        ...request,
        status: decision,
        decidedBy: this.user.current().displayName,
        decidedOn: todayIso(),
        remarks: remarks.trim() || `${decision}.`,
      };
      this.store.update((items) => items.map((a) => (a.id === id ? decided : a)));
      if (decision === 'Approved') {
        this.applyToRecord(decided);
      }
      this.activity.record(
        'attendance',
        `${this.describe(decided)} ${decision.toLowerCase()} for ${decided.date}`,
      );
      return decided;
    });
  }

  /** Applies the requested times / status to the attendance record using the shared rules. */
  private applyToRecord(adjustment: AttendanceAdjustment): void {
    const record = this.attendance.findById(adjustment.recordId ?? -1);
    if (!record) {
      return;
    }
    const shift = this.shifts.findById(record.shiftId ?? -1) ?? this.shifts.records()[0];
    const checkIn = adjustment.requestedCheckIn ?? record.checkIn;
    const checkOut = adjustment.requestedCheckOut ?? record.checkOut;
    const derived = deriveAttendance(shift, checkIn, checkOut, adjustment.requestedStatus);
    this.attendance.patchRecord(record.id, {
      ...derived,
      checkIn,
      checkOut,
      source: 'Manual',
      remarks: `Adjusted (${this.describe(adjustment)})`,
    });
  }
}

@Injectable({ providedIn: 'root' })
export class OvertimeMockService
  extends InMemoryCrudService<OvertimeRecord>
  implements OvertimeDataService
{
  private readonly user = inject(CurrentUserService);

  protected readonly entityLabel = 'Overtime entry';
  protected readonly activityCategory = 'attendance';
  protected override readonly searchFields = ['reason'] as const;

  constructor() {
    super(MOCK_OVERTIME_RECORDS, 'overtime_records');
  }

  protected override describe(entity: OvertimeRecord): string {
    return `OT-${String(entity.id).padStart(4, '0')}`;
  }

  protected override prepare(candidate: OvertimeRecord, isNew: boolean): void {
    candidate.minutes =
      candidate.startTime && candidate.endTime
        ? minutesBetween(candidate.startTime, candidate.endTime)
        : 0;
    if (isNew) {
      candidate.status = 'Pending';
      candidate.requestedOn = todayIso();
      candidate.decidedBy = null;
      candidate.decidedOn = null;
      candidate.remarks = '';
    } else {
      candidate.requestedOn = this.findById(candidate.id)?.requestedOn ?? candidate.requestedOn;
    }
  }

  protected override validate(candidate: OvertimeRecord, isNew: boolean): void {
    if (!isNew && this.findById(candidate.id)?.status !== 'Pending') {
      this.conflictError('Only pending entries can be changed.');
    }
    if (candidate.date > todayIso()) {
      this.validationError('The date cannot be in the future.', {
        date: 'Choose today or an earlier date.',
      });
    }
    if (candidate.minutes < 15 || candidate.minutes > 12 * 60) {
      this.validationError('Overtime must be between 15 minutes and 12 hours.', {
        endTime: 'Duration must be between 15 minutes and 12 hours.',
      });
    }
    const duplicate = this.store().some(
      (o) =>
        o.id !== candidate.id && o.employeeId === candidate.employeeId && o.date === candidate.date,
    );
    if (duplicate) {
      this.validationError('Overtime is already recorded for this employee on that date.', {
        date: 'Already recorded for this date.',
      });
    }
  }

  protected override beforeDelete(entity: OvertimeRecord): void {
    if (entity.status !== 'Pending') {
      this.conflictError('Only pending entries can be deleted.');
    }
  }

  decide(id: number, decision: Decision, remarks: string): Observable<OvertimeRecord> {
    return this.respond(() => {
      const entry = this.require(id);
      if (entry.status !== 'Pending') {
        this.conflictError(`This entry was already ${entry.status.toLowerCase()}.`);
      }
      const decided: OvertimeRecord = {
        ...entry,
        status: decision,
        decidedBy: this.user.current().displayName,
        decidedOn: todayIso(),
        remarks: remarks.trim() || `${decision}.`,
      };
      this.store.update((items) => items.map((o) => (o.id === id ? decided : o)));
      this.activity.record('attendance', `${this.describe(decided)} ${decision.toLowerCase()}`);
      return decided;
    });
  }
}
