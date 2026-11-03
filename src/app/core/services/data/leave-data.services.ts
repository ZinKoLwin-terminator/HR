import { Observable } from 'rxjs';
import { PagedResult } from '../../models/common.models';
import {
  EmployeeLeaveBalances,
  LeaveApplication,
  LeaveBalance,
  LeaveBalanceQuery,
  LeaveCalendarMonth,
  LeaveCalendarQuery,
  LeavePolicy,
  LeaveType,
} from '../../models/leave.models';
import { CrudDataService } from './crud-data.service';

export type LeaveDecision = 'Approved' | 'Rejected';

export abstract class LeaveTypeDataService extends CrudDataService<LeaveType> {}
export abstract class LeavePolicyDataService extends CrudDataService<LeavePolicy> {}

export abstract class LeaveApplicationDataService extends CrudDataService<LeaveApplication> {
  /** Draft → Submitted. Checks balance, documents and overlaps. */
  abstract submit(id: number): Observable<LeaveApplication>;
  /** Submitted → Approved / Rejected (a rejection needs remarks). */
  abstract decide(
    id: number,
    decision: LeaveDecision,
    remarks: string,
  ): Observable<LeaveApplication>;
  /** Submitted / future Approved → Cancelled. */
  abstract cancel(id: number, remarks: string): Observable<LeaveApplication>;
}

export abstract class LeaveBalanceDataService {
  abstract getForEmployee(employeeId: number, year: number): Observable<LeaveBalance[]>;
  abstract query(request: LeaveBalanceQuery): Observable<PagedResult<EmployeeLeaveBalances>>;
}

export abstract class LeaveCalendarDataService {
  abstract getMonth(query: LeaveCalendarQuery): Observable<LeaveCalendarMonth>;
}
