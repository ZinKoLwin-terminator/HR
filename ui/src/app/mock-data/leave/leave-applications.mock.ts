import { DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { LeaveApplication, LeaveStatus } from '../../core/models/leave.models';
import { addDays, countWorkingDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_HOLIDAYS } from '../attendance/holidays.mock';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';

type LeaveSeed = [
  employeeId: number,
  leaveTypeId: number,
  /** Start date relative to today (days). */
  startOffset: number,
  /** Calendar days covered (end = start + span - 1). */
  span: number,
  status: LeaveStatus,
  reason: string,
  halfDay?: boolean,
];

const SEED: LeaveSeed[] = [
  // Past, approved
  [14, 1, -41, 5, 'Approved', 'Family vacation to Bagan.'],
  [21, 2, -38, 1, 'Approved', 'Bank and government office errands.'],
  [27, 3, -34, 2, 'Approved', 'Fever and flu.'],
  [9, 1, -30, 3, 'Approved', 'Attending a wedding in Mandalay.'],
  [33, 2, -27, 1, 'Approved', 'Personal matter.', true],
  [19, 3, -22, 1, 'Approved', 'Dental appointment.'],
  [16, 1, -19, 4, 'Approved', 'Annual trip.'],
  [29, 7, -16, 3, 'Approved', 'Bereavement in the family.'],
  [37, 2, -13, 1, 'Approved', 'Child school event.'],
  [24, 1, -9, 2, 'Approved', 'Long weekend.'],
  [46, 3, -6, 1, 'Approved', 'Food poisoning.'],
  [12, 2, -4, 1, 'Approved', 'Vehicle registration.', true],
  // Covering today
  [35, 4, -60, 98, 'Approved', 'Maternity leave.'],
  [20, 1, -1, 4, 'Approved', 'Visiting family in Taunggyi.'],
  [31, 3, 0, 2, 'Approved', 'Medical check-up and recovery.'],
  [43, 2, 0, 1, 'Approved', 'Moving to a new apartment.'],
  // Pending approval (future)
  [17, 1, 3, 3, 'Submitted', 'Short holiday at Ngapali beach.'],
  [23, 2, 2, 1, 'Submitted', 'Personal errands.'],
  [26, 1, 8, 5, 'Submitted', 'Year-end family trip.'],
  [41, 3, 1, 2, 'Submitted', 'Scheduled minor surgery.'],
  [52, 5, 10, 10, 'Submitted', 'Birth of first child.'],
  [8, 1, 15, 4, 'Submitted', 'Travel abroad.'],
  [28, 2, 4, 1, 'Submitted', 'House maintenance.', true],
  // Rejected / cancelled / draft
  [38, 1, -20, 3, 'Rejected', 'Personal travel.'],
  [47, 6, -10, 5, 'Rejected', 'Extended personal break.'],
  [22, 2, 6, 1, 'Cancelled', 'Plans changed.'],
  [45, 1, -15, 2, 'Cancelled', 'No longer required.'],
  [49, 1, 20, 3, 'Draft', 'Planning a short trip.'],
  [13, 2, 12, 1, 'Draft', 'Family event.'],
];

const holidaySet = new Set(MOCK_HOLIDAYS.map((h) => h.date));

function buildApplication(seed: LeaveSeed, index: number): LeaveApplication {
  const [employeeId, leaveTypeId, startOffset, span, status, reason, halfDay = false] = seed;
  const today = todayIso();
  const startDate = addDays(today, startOffset);
  const endDate = addDays(startDate, span - 1);
  const employee = MOCK_EMPLOYEES.find((e) => e.id === employeeId);
  const appliedOn = addDays(startDate, startOffset > 0 ? -Math.min(startOffset, 3) : -7);
  const actioned = status === 'Approved' || status === 'Rejected';
  const workingDays = countWorkingDays(
    startDate,
    endDate,
    DEFAULT_APP_CONFIG.weekendDays,
    holidaySet,
  );

  return {
    id: index + 1,
    applicationNo: `LV-${today.slice(0, 4)}-${String(index + 1).padStart(4, '0')}`,
    employeeId,
    leaveTypeId,
    startDate,
    endDate,
    days: halfDay ? 0.5 : Math.max(workingDays, 1),
    halfDay,
    reason,
    documentName: leaveTypeId === 3 || leaveTypeId === 4 ? 'medical-certificate.pdf' : null,
    status,
    appliedOn: appliedOn > today ? today : appliedOn,
    approverId: actioned ? (employee?.managerId ?? 2) : null,
    actionedOn: actioned ? addDays(appliedOn, 1) : null,
    approverRemarks:
      status === 'Rejected'
        ? 'Insufficient team coverage for the requested dates.'
        : status === 'Approved'
          ? 'Approved.'
          : null,
  };
}

/** SAMPLE DATA — dates are relative to the current day. */
export const MOCK_LEAVE_APPLICATIONS: LeaveApplication[] = SEED.map(buildApplication);
