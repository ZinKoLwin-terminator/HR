import { Injectable } from '@angular/core';
import { LeavePolicy, LeaveType } from '../../core/models/leave.models';
import {
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../../core/services/data/leave-data.services';
import { HttpCrudService } from '../http-crud.service';

// HTTP implementations of the leave type and leave policy contracts. Status changes go through the
// shared activate/deactivate commands and every PUT/DELETE carries the rowVersion (see HttpCrudService).

@Injectable({ providedIn: 'root' })
export class LeaveTypeHttpService
  extends HttpCrudService<LeaveType>
  implements LeaveTypeDataService
{
  protected readonly resource = 'leave-types';
  protected readonly filterKeys = ['status'] as const;
  protected override readonly sortKeys = [
    'id',
    'code',
    'name',
    'defaultDaysPerYear',
    'maxConsecutiveDays',
    'status',
  ] as const;
}

@Injectable({ providedIn: 'root' })
export class LeavePolicyHttpService
  extends HttpCrudService<LeavePolicy>
  implements LeavePolicyDataService
{
  protected readonly resource = 'leave-policies';
  protected readonly filterKeys = [
    'status',
    'leaveTypeId',
    'jobGradeId',
    'employmentType',
    'accrual',
  ] as const;
  /** The API cannot sort by accrual, grade or employment type: those clicks keep the default order. */
  protected override readonly sortKeys = [
    'id',
    'name',
    'leaveTypeId',
    'daysPerYear',
    'minServiceMonths',
    'status',
  ] as const;
}
