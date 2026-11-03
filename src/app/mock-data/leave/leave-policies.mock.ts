import { LeaveAccrual, LeavePolicy } from '../../core/models/leave.models';
import { EmploymentType } from '../../core/models/employee.models';

type Seed = [
  name: string,
  leaveTypeId: number,
  jobGradeId: number | null,
  employmentType: EmploymentType | null,
  daysPerYear: number,
  accrual: LeaveAccrual,
  minServiceMonths: number,
];

// Leave type ids: 1 Annual, 2 Casual, 3 Medical, 4 Maternity, 5 Paternity, 6 Unpaid, 7 Compassionate.
// Job grade ids: 6 Manager, 7 Senior Manager, 8 Director, 9 Executive.
const SEED: Seed[] = [
  ['Annual leave — standard (after 3 months)', 1, null, null, 15, 'Upfront', 3],
  ['Annual leave — managers', 1, 6, null, 18, 'Upfront', 0],
  ['Annual leave — senior managers', 1, 7, null, 20, 'Upfront', 0],
  ['Annual leave — directors', 1, 8, null, 22, 'Upfront', 0],
  ['Annual leave — executives', 1, 9, null, 25, 'Upfront', 0],
  ['Annual leave — contract staff', 1, null, 'Contract', 10, 'Upfront', 0],
  ['Annual leave — interns', 1, null, 'Intern', 6, 'Monthly', 0],
  ['Casual leave — all staff (monthly accrual)', 2, null, null, 6, 'Monthly', 0],
  ['Medical leave — interns', 3, null, 'Intern', 10, 'Upfront', 0],
];

/** SAMPLE DATA — illustrative entitlement rules, not statutory. */
export const MOCK_LEAVE_POLICIES: LeavePolicy[] = SEED.map(
  ([name, leaveTypeId, jobGradeId, employmentType, daysPerYear, accrual, minServiceMonths], i) => ({
    id: i + 1,
    name,
    leaveTypeId,
    jobGradeId,
    employmentType,
    daysPerYear,
    accrual,
    minServiceMonths,
    status: 'Active',
  }),
);
