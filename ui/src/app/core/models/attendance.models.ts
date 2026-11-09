import { Entity, RecordStatus } from './common.models';

export type AttendanceStatus =
  'Present' | 'Absent' | 'Late' | 'Half Day' | 'On Leave' | 'Holiday' | 'Weekend';

export const ATTENDANCE_STATUSES: readonly AttendanceStatus[] = [
  'Present',
  'Absent',
  'Late',
  'Half Day',
  'On Leave',
  'Holiday',
  'Weekend',
];

export type AttendanceSource = 'Biometric' | 'Web' | 'Mobile' | 'Manual';

export interface Shift extends Entity {
  code: string;
  name: string;
  /** HH:mm (24h) */
  startTime: string;
  endTime: string;
  breakMinutes: number;
  graceMinutes: number;
  /** Working weekdays, 0 = Sunday … 6 = Saturday. */
  workDays: number[];
  isNightShift: boolean;
  color: string;
  status: RecordStatus;
}

export interface AttendanceRecord extends Entity {
  employeeId: number;
  /** yyyy-MM-dd */
  date: string;
  shiftId: number | null;
  /** HH:mm or null when not recorded. */
  checkIn: string | null;
  checkOut: string | null;
  workedMinutes: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
  status: AttendanceStatus;
  source: AttendanceSource;
  remarks: string;
}

/** Record joined with employee / organization / shift names for list screens. */
export interface AttendanceListItem extends AttendanceRecord {
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  branchId: number;
  branchName: string;
  shiftName: string;
}

export interface AttendanceFilter {
  employeeId: number | null;
  departmentId: number | null;
  branchId: number | null;
  status: AttendanceStatus | null;
  /** Inclusive yyyy-MM-dd bounds. */
  dateFrom: string | null;
  dateTo: string | null;
  /** Only records with late minutes / early-leave minutes / overtime. */
  lateOnly: boolean | null;
  earlyOnly: boolean | null;
  overtimeOnly: boolean | null;
}

export interface Holiday extends Entity {
  date: string;
  name: string;
  type: 'Public' | 'Company';
}

export type PunchKind = 'in' | 'out';

/** A check-in / check-out request. The client supplies the local date and time. */
export interface PunchRequest {
  employeeId: number;
  date: string;
  time: string;
  kind: PunchKind;
}

// ---- Approval workflows -------------------------------------------------------

export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export const APPROVAL_STATUSES: readonly ApprovalStatus[] = ['Pending', 'Approved', 'Rejected'];

/** A request to correct an attendance record. Approval applies it to the record. */
export interface AttendanceAdjustment extends Entity {
  employeeId: number;
  date: string;
  /** Record being corrected (resolved by the service from employee + date). */
  recordId: number | null;
  currentCheckIn: string | null;
  currentCheckOut: string | null;
  requestedCheckIn: string | null;
  requestedCheckOut: string | null;
  /** Optional explicit status (e.g. Half Day). Otherwise derived from the times. */
  requestedStatus: AttendanceStatus | null;
  reason: string;
  status: ApprovalStatus;
  requestedOn: string;
  decidedBy: string | null;
  decidedOn: string | null;
  remarks: string;
}

export interface OvertimeRecord extends Entity {
  employeeId: number;
  date: string;
  startTime: string;
  endTime: string;
  /** Derived from the start and end time. */
  minutes: number;
  reason: string;
  status: ApprovalStatus;
  requestedOn: string;
  decidedBy: string | null;
  decidedOn: string | null;
  remarks: string;
}

// ---- Dashboard & schedule views ------------------------------------------------

export interface AttendanceDaySummary {
  date: string;
  /** Employees expected to work that day. */
  scheduled: number;
  present: number;
  late: number;
  halfDay: number;
  absent: number;
  onLeave: number;
  /** % of scheduled employees who checked in (present + late + half day). */
  attendanceRate: number;
}

export interface DepartmentAttendance {
  departmentId: number;
  department: string;
  present: number;
  late: number;
  halfDay: number;
  absent: number;
  onLeave: number;
  total: number;
}

export interface AttendanceException {
  employeeId: number;
  employeeCode: string;
  name: string;
  department: string;
  /** e.g. "Checked in 09:42 (27 min late)". */
  detail: string;
}

export interface AttendanceTrendDay {
  date: string;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
}

export interface AttendanceOverview {
  summary: AttendanceDaySummary;
  byDepartment: DepartmentAttendance[];
  late: AttendanceException[];
  absent: AttendanceException[];
  trend: AttendanceTrendDay[];
}

export type ScheduleCellKind = 'Shift' | 'Off' | 'Holiday' | 'Leave';

export interface ScheduleCell {
  date: string;
  kind: ScheduleCellKind;
  /** Short text, e.g. "09:00–18:00", "Off", "Water Festival". */
  label: string;
}

export interface ScheduleRow {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  department: string;
  shiftName: string;
  shiftColor: string;
  cells: ScheduleCell[];
}

export interface WorkSchedule {
  weekStart: string;
  days: string[];
  rows: ScheduleRow[];
}

export interface ScheduleQuery {
  weekStart: string;
  departmentId: number | null;
  branchId: number | null;
}
