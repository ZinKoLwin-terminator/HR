import { Entity, RecordStatus } from './common.models';
import { EmploymentType, Gender } from './employee.models';

export interface LeaveType extends Entity {
  code: string;
  name: string;
  description: string;
  defaultDaysPerYear: number;
  isPaid: boolean;
  requiresDocument: boolean;
  maxConsecutiveDays: number;
  carryForward: boolean;
  maxCarryForwardDays: number;
  color: string;
  /** When set, only employees of this gender are eligible (e.g. maternity leave). */
  eligibleGender: Gender | null;
  status: RecordStatus;
}

export type LeaveAccrual = 'Upfront' | 'Monthly';

/**
 * Entitlement rule. The most specific active policy for an employee's grade and
 * employment type decides the yearly days; otherwise the leave type's default applies.
 */
export interface LeavePolicy extends Entity {
  name: string;
  leaveTypeId: number;
  /** Null applies to every job grade. */
  jobGradeId: number | null;
  /** Null applies to every employment type. */
  employmentType: EmploymentType | null;
  daysPerYear: number;
  accrual: LeaveAccrual;
  /** Employees with less service are not entitled under this policy. */
  minServiceMonths: number;
  status: RecordStatus;
}

export type LeaveStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Cancelled';

export const LEAVE_STATUSES: readonly LeaveStatus[] = [
  'Draft',
  'Submitted',
  'Approved',
  'Rejected',
  'Cancelled',
];

export interface LeaveApplication extends Entity {
  applicationNo: string;
  employeeId: number;
  leaveTypeId: number;
  startDate: string;
  endDate: string;
  /** Working days requested (0.5 for a half day). */
  days: number;
  halfDay: boolean;
  reason: string;
  documentName: string | null;
  status: LeaveStatus;
  appliedOn: string;
  approverId: number | null;
  actionedOn: string | null;
  approverRemarks: string | null;
}

// ---- Balances ---------------------------------------------------------------

export interface LeaveBalance {
  leaveTypeId: number;
  code: string;
  name: string;
  color: string;
  /** Days granted for the year (after policy, accrual and service rules). */
  entitled: number;
  /** Approved days taken in the year. */
  taken: number;
  /** Days on submitted (not yet decided) requests. */
  pending: number;
  /** entitled − taken − pending (never below 0). */
  available: number;
}

export interface EmployeeLeaveBalances {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  branchId: number;
  branchName: string;
  balances: LeaveBalance[];
}

export interface LeaveBalanceQuery {
  year: number;
  search: string;
  departmentId: number | null;
  branchId: number | null;
  pageIndex: number;
  pageSize: number;
  sort?: { active: string; direction: 'asc' | 'desc' | '' };
}

// ---- Calendar ---------------------------------------------------------------

export interface LeaveCalendarEntry {
  applicationId: number;
  employeeId: number;
  employeeName: string;
  leaveTypeId: number;
  leaveTypeName: string;
  color: string;
  status: 'Approved' | 'Submitted';
  halfDay: boolean;
}

export interface LeaveCalendarDay {
  date: string;
  /** False for the leading/trailing days that complete the first and last week. */
  inMonth: boolean;
  isWeekend: boolean;
  holiday: string | null;
  entries: LeaveCalendarEntry[];
}

export interface LeaveCalendarQuery {
  /** yyyy-MM */
  month: string;
  departmentId: number | null;
  leaveTypeId: number | null;
  includePending: boolean;
}

export interface LeaveCalendarMonth {
  month: string;
  /** Whole Monday–Sunday weeks covering the month. */
  days: LeaveCalendarDay[];
}
