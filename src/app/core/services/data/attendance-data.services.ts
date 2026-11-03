import { Observable } from 'rxjs';
import {
  ApprovalStatus,
  AttendanceAdjustment,
  AttendanceFilter,
  AttendanceListItem,
  AttendanceOverview,
  AttendanceRecord,
  Holiday,
  OvertimeRecord,
  PunchRequest,
  ScheduleQuery,
  Shift,
  WorkSchedule,
} from '../../models/attendance.models';
import { CrudDataService } from './crud-data.service';

export abstract class ShiftDataService extends CrudDataService<Shift> {}
export abstract class HolidayDataService extends CrudDataService<Holiday> {}

export abstract class AttendanceRecordDataService extends CrudDataService<
  AttendanceRecord,
  AttendanceListItem,
  AttendanceFilter
> {
  /** Records a check-in or check-out and returns the updated day record. */
  abstract punch(request: PunchRequest): Observable<AttendanceRecord>;
}

export abstract class AttendanceAdjustmentDataService extends CrudDataService<AttendanceAdjustment> {
  /** Approving applies the requested times to the attendance record. */
  abstract decide(
    id: number,
    decision: Exclude<ApprovalStatus, 'Pending'>,
    remarks: string,
  ): Observable<AttendanceAdjustment>;
}

export abstract class OvertimeDataService extends CrudDataService<OvertimeRecord> {
  abstract decide(
    id: number,
    decision: Exclude<ApprovalStatus, 'Pending'>,
    remarks: string,
  ): Observable<OvertimeRecord>;
}

export abstract class AttendanceOverviewDataService {
  abstract getOverview(date: string, branchId: number | null): Observable<AttendanceOverview>;
}

export abstract class WorkScheduleDataService {
  abstract getWeek(query: ScheduleQuery): Observable<WorkSchedule>;
}
