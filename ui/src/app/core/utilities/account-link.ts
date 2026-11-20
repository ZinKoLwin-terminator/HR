import { Employee } from '../models/employee.models';
import { AppUser, BUILT_IN_ADMIN_EMPLOYEE_ID } from '../models/settings.models';

/**
 * Rules for linking a user account to its employee (Settings > Users > Link to employee). The
 * server applies the same rules; the mock and the screen use these so they agree with it.
 */

/** The employee fields the rules read. */
export type LinkCandidate = Pick<Employee, 'workEmail' | 'exitDate'>;

/** True when the account has no employee (the API sends null; the screens see `0`). */
export function isUnlinkedAccount(user: Pick<AppUser, 'employeeId'>): boolean {
  return user.employeeId === null || user.employeeId === BUILT_IN_ADMIN_EMPLOYEE_ID;
}

/** Email comparison of the rule: trimmed and case-insensitive. */
export function sameEmail(a: string | null | undefined, b: string | null | undefined): boolean {
  const left = (a ?? '').trim().toLowerCase();
  return left.length > 0 && left === (b ?? '').trim().toLowerCase();
}

/**
 * A current employee: no exit date. Same rule as the API (and as sign-in): any exit date, even a
 * future one, makes the employee a leaver, whatever the employment status says.
 */
export function isCurrentEmployee(employee: Pick<Employee, 'exitDate'>): boolean {
  return !employee.exitDate;
}

/** The current employee whose work email is the account's sign-in email, if any. */
export function employeeForAccountEmail<E extends LinkCandidate>(
  employees: readonly E[],
  email: string,
): E | undefined {
  return employees.find((e) => isCurrentEmployee(e) && sameEmail(e.workEmail, email));
}
