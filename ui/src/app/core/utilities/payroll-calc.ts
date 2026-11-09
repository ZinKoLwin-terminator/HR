import { LeaveApplication, LeaveType } from '../models/leave.models';
import { OvertimeRecord } from '../models/attendance.models';
import { PayLine, SalaryComponent } from '../models/payroll.models';
import { countWorkingDays, diffInDays } from '../../shared/utilities/date.util';

/**
 * SAMPLE payroll rules. These are demonstration values only — NOT real tax,
 * social-security or labour-law calculations.
 */
export const PAYROLL_RULES = {
  /** Overtime is paid at this multiple of the hourly rate. */
  overtimeMultiplier: 1.5,
  hoursPerWorkingDay: 8,
} as const;

export const round2 = (value: number): number => Math.round((value + Number.EPSILON) * 100) / 100;

/** "10% of basic", "2% of gross" or "Fixed 50". */
export function describeComponent(
  component: Pick<SalaryComponent, 'calculation' | 'value'>,
): string {
  switch (component.calculation) {
    case 'PercentOfBasic':
      return `${component.value}% of basic`;
    case 'PercentOfGross':
      return `${component.value}% of gross`;
    default:
      return `Fixed ${component.value}`;
  }
}

export interface PayInput {
  /** Full-month basic salary. */
  basicSalary: number;
  /** Active components of the employee's structure, in display order. */
  components: readonly SalaryComponent[];
  periodStart: string;
  periodEnd: string;
  joiningDate: string;
  exitDate: string | null;
  /** Working days in the period (weekends and holidays excluded). */
  workingDays: number;
  unpaidLeaveDays: number;
  /** Approved overtime within the period. */
  overtimeMinutes: number;
}

export interface PayResult {
  basic: number;
  earnings: PayLine[];
  deductions: PayLine[];
  gross: number;
  totalDeductions: number;
  net: number;
  periodDays: number;
  daysEmployed: number;
}

/**
 * Sample pay calculation for one employee and period:
 *  1. Part-month employment (joiners / leavers) prorates basic and fixed components by calendar days.
 *  2. Earnings: fixed amounts, or a percentage of the (prorated) basic. Approved overtime is added at
 *     hourly rate × 1.5, where hourly = full basic ÷ (working days × 8).
 *  3. Gross = basic + earnings.
 *  4. Deductions: fixed amounts, a percentage of basic, or a percentage of gross, plus unpaid leave at
 *     the daily rate (full basic ÷ working days).
 *  5. Net = gross − deductions.
 * Returns `null` when the employee was not employed on any day of the period.
 */
export function calculatePay(input: PayInput): PayResult | null {
  const periodDays = diffInDays(input.periodStart, input.periodEnd) + 1;
  const from = input.joiningDate > input.periodStart ? input.joiningDate : input.periodStart;
  const to = input.exitDate && input.exitDate < input.periodEnd ? input.exitDate : input.periodEnd;
  const daysEmployed = Math.max(0, diffInDays(from, to) + 1);
  if (daysEmployed === 0) {
    return null;
  }
  const proration = daysEmployed / periodDays;
  const basic = round2(input.basicSalary * proration);

  const earnings: PayLine[] = [];
  for (const c of input.components.filter((x) => x.type === 'Earning' && x.status === 'Active')) {
    const amount = c.calculation === 'Fixed' ? c.value * proration : (basic * c.value) / 100;
    earnings.push({
      code: c.code,
      name: c.name,
      amount: round2(amount),
      basis: describeComponent(c),
    });
  }
  if (input.overtimeMinutes > 0 && input.workingDays > 0) {
    const hours = input.overtimeMinutes / 60;
    const hourly = input.basicSalary / (input.workingDays * PAYROLL_RULES.hoursPerWorkingDay);
    earnings.push({
      code: 'OT',
      name: 'Overtime pay',
      amount: round2(hours * hourly * PAYROLL_RULES.overtimeMultiplier),
      basis: `${round2(hours)} h × ${round2(hourly)} × ${PAYROLL_RULES.overtimeMultiplier}`,
    });
  }
  const gross = round2(basic + earnings.reduce((sum, l) => sum + l.amount, 0));

  const deductions: PayLine[] = [];
  for (const c of input.components.filter((x) => x.type === 'Deduction' && x.status === 'Active')) {
    const base = c.calculation === 'PercentOfGross' ? gross : basic;
    const amount = c.calculation === 'Fixed' ? c.value * proration : (base * c.value) / 100;
    deductions.push({
      code: c.code,
      name: c.name,
      amount: round2(amount),
      basis: describeComponent(c),
    });
  }
  if (input.unpaidLeaveDays > 0 && input.workingDays > 0) {
    const daily = input.basicSalary / input.workingDays;
    deductions.push({
      code: 'UL',
      name: 'Unpaid leave',
      amount: round2(input.unpaidLeaveDays * daily),
      basis: `${input.unpaidLeaveDays} day(s) × ${round2(daily)}`,
    });
  }
  const totalDeductions = round2(deductions.reduce((sum, l) => sum + l.amount, 0));
  return {
    basic,
    earnings,
    deductions,
    gross,
    totalDeductions,
    net: round2(gross - totalDeductions),
    periodDays,
    daysEmployed,
  };
}

/** Unpaid-leave days and approved overtime of one employee inside a period. */
export function collectPayrollAdjustments(
  employeeId: number,
  periodStart: string,
  periodEnd: string,
  leaves: readonly LeaveApplication[],
  leaveTypes: readonly LeaveType[],
  overtime: readonly OvertimeRecord[],
  holidays: ReadonlySet<string>,
  weekendDays: readonly number[],
): { unpaidLeaveDays: number; overtimeMinutes: number } {
  const unpaidTypes = new Set(leaveTypes.filter((t) => !t.isPaid).map((t) => t.id));
  let unpaidLeaveDays = 0;
  for (const l of leaves) {
    if (l.employeeId !== employeeId || l.status !== 'Approved' || !unpaidTypes.has(l.leaveTypeId))
      continue;
    const start = l.startDate > periodStart ? l.startDate : periodStart;
    const end = l.endDate < periodEnd ? l.endDate : periodEnd;
    if (start <= end) {
      unpaidLeaveDays += l.halfDay ? 0.5 : countWorkingDays(start, end, weekendDays, holidays);
    }
  }
  const overtimeMinutes = overtime
    .filter(
      (o) =>
        o.employeeId === employeeId &&
        o.status === 'Approved' &&
        o.date >= periodStart &&
        o.date <= periodEnd,
    )
    .reduce((sum, o) => sum + o.minutes, 0);
  return { unpaidLeaveDays, overtimeMinutes };
}

/** Working days of a period: Monday–Friday style per the configured weekend, minus holidays. */
export function periodWorkingDays(
  periodStart: string,
  periodEnd: string,
  holidays: ReadonlySet<string>,
  weekendDays: readonly number[],
): number {
  return countWorkingDays(periodStart, periodEnd, weekendDays, holidays);
}
