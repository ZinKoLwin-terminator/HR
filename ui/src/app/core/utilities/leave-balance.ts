import { Employee, Gender } from '../models/employee.models';
import { LeaveApplication, LeaveBalance, LeavePolicy, LeaveType } from '../models/leave.models';

export type { LeaveBalance } from '../models/leave.models';

/** Optional per-type entitlement override (see {@link resolveEntitlement}). */
export type EntitlementResolver = (type: LeaveType) => number;

/**
 * Sample leave-balance rule: entitlement comes from the resolver (default: the
 * type's yearly days); approved leave counts as taken; submitted leave is
 * reserved as pending. Only active types the employee is eligible for are returned.
 */
export function computeLeaveBalances(
  types: readonly LeaveType[],
  applications: readonly LeaveApplication[],
  gender: Gender,
  year: number,
  entitlementFor: EntitlementResolver = (type) => type.defaultDaysPerYear,
): LeaveBalance[] {
  const prefix = String(year);
  return types
    .filter((t) => t.status === 'Active' && (!t.eligibleGender || t.eligibleGender === gender))
    .map((type) => {
      const mine = applications.filter(
        (a) => a.leaveTypeId === type.id && a.startDate.startsWith(prefix),
      );
      const sum = (status: LeaveApplication['status']) =>
        mine.filter((a) => a.status === status).reduce((total, a) => total + a.days, 0);
      const taken = sum('Approved');
      const pending = sum('Submitted');
      const entitled = entitlementFor(type);
      return {
        leaveTypeId: type.id,
        code: type.code,
        name: type.name,
        color: type.color,
        entitled,
        taken,
        pending,
        available: Math.max(0, entitled - taken - pending),
      };
    });
}

/** Whole months between two ISO dates (never negative). */
function monthsBetween(from: string, to: string): number {
  const a = new Date(`${from}T00:00:00`);
  const b = new Date(`${to}T00:00:00`);
  let months = (b.getFullYear() - a.getFullYear()) * 12 + (b.getMonth() - a.getMonth());
  if (b.getDate() < a.getDate()) months--;
  return Math.max(0, months);
}

type EntitlementEmployee = Pick<Employee, 'jobGradeId' | 'employmentType' | 'joiningDate'>;

/**
 * Yearly entitlement of a leave type for an employee.
 *
 *  1. Active policies for the type that match the employee's job grade and
 *     employment type (a `null` policy field matches everyone) are candidates.
 *  2. The most specific candidate wins (grade match > employment-type match).
 *     Candidates whose minimum service has not been reached are skipped.
 *  3. With no candidate at all, the leave type's default applies. If candidates
 *     exist but none is satisfied (e.g. too little service), the entitlement is 0.
 *  4. Monthly accrual grants `days × month / 12` for the current year, in
 *     half-day steps (full amount for past years).
 */
export function resolveEntitlement(
  type: LeaveType,
  policies: readonly LeavePolicy[],
  employee: EntitlementEmployee,
  asOf: string,
): number {
  const candidates = policies.filter(
    (p) =>
      p.status === 'Active' &&
      p.leaveTypeId === type.id &&
      (p.jobGradeId === null || p.jobGradeId === employee.jobGradeId) &&
      (p.employmentType === null || p.employmentType === employee.employmentType),
  );
  if (candidates.length === 0) {
    return type.defaultDaysPerYear;
  }
  const service = monthsBetween(employee.joiningDate, asOf);
  const score = (p: LeavePolicy) =>
    (p.jobGradeId !== null ? 2 : 0) + (p.employmentType !== null ? 1 : 0);
  const eligible = candidates
    .filter((p) => service >= p.minServiceMonths)
    .sort((a, b) => score(b) - score(a) || b.daysPerYear - a.daysPerYear);
  const chosen = eligible[0];
  if (!chosen) {
    return 0;
  }
  if (chosen.accrual === 'Monthly') {
    const month = Number(asOf.slice(5, 7));
    return Math.round(((chosen.daysPerYear * month) / 12) * 2) / 2;
  }
  return chosen.daysPerYear;
}
