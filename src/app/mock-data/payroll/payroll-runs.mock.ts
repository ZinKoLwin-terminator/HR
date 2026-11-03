import { Employee } from '../../core/models/employee.models';
import { PayrollRun, PayrollRunStatus } from '../../core/models/payroll.models';
import {
  addDays,
  addMonths,
  endOfMonth,
  startOfMonth,
  todayIso,
} from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { seededRandom } from '../mock-random';
import { MOCK_JOB_GRADES } from '../organization/job-grades.mock';

/**
 * SAMPLE payroll rules. These are configurable demonstration values only —
 * they do NOT represent any country's tax or social-security legislation.
 */
export const SAMPLE_PAYROLL_RULES = {
  housingAllowanceRate: 0.1,
  transportAllowance: 50,
  sampleIncomeTaxRate: 0.05,
  sampleSocialSecurityRate: 0.02,
  /** Pay date = this many days after period end. */
  payDateOffsetDays: 0,
} as const;

/** Deterministic sample basic salary inside the employee's job-grade band. */
export function sampleBasicSalary(employee: Pick<Employee, 'id' | 'jobGradeId'>): number {
  const grade = MOCK_JOB_GRADES.find((g) => g.id === employee.jobGradeId) ?? MOCK_JOB_GRADES[0];
  const span = grade.maxSalary - grade.minSalary;
  return Math.round((grade.minSalary + span * seededRandom(`salary-${employee.id}`)) / 10) * 10;
}

export interface SamplePayBreakdown {
  basic: number;
  allowances: number;
  gross: number;
  deductions: number;
  net: number;
}

export function samplePayBreakdown(basic: number): SamplePayBreakdown {
  const r = SAMPLE_PAYROLL_RULES;
  const allowances = Math.round(basic * r.housingAllowanceRate + r.transportAllowance);
  const gross = basic + allowances;
  const deductions = Math.round(gross * (r.sampleIncomeTaxRate + r.sampleSocialSecurityRate));
  return { basic, allowances, gross, deductions, net: gross - deductions };
}

function employedDuring(employee: Employee, from: string, to: string): boolean {
  return employee.joiningDate <= to && (!employee.exitDate || employee.exitDate >= from);
}

function buildRun(monthStart: string, id: number, status: PayrollRunStatus): PayrollRun {
  const endDate = endOfMonth(monthStart);
  const totals = MOCK_EMPLOYEES.filter((e) => employedDuring(e, monthStart, endDate)).reduce(
    (acc, e) => {
      const pay = samplePayBreakdown(sampleBasicSalary(e));
      acc.count++;
      acc.basic += pay.basic;
      acc.allowances += pay.allowances;
      acc.gross += pay.gross;
      acc.deductions += pay.deductions;
      acc.net += pay.net;
      return acc;
    },
    { count: 0, basic: 0, allowances: 0, gross: 0, deductions: 0, net: 0 },
  );
  const date = new Date(`${monthStart}T00:00:00`);
  return {
    id,
    periodName: date.toLocaleString('en-US', { month: 'long', year: 'numeric' }),
    year: date.getFullYear(),
    month: date.getMonth() + 1,
    startDate: monthStart,
    endDate,
    payDate: addDays(endDate, SAMPLE_PAYROLL_RULES.payDateOffsetDays),
    status,
    employeeCount: totals.count,
    totalBasic: totals.basic,
    totalAllowances: totals.allowances,
    totalGross: totals.gross,
    totalDeductions: totals.deductions,
    totalNet: totals.net,
  };
}

function generateRuns(): PayrollRun[] {
  const currentMonth = startOfMonth(todayIso());
  const runs: PayrollRun[] = [];
  for (let offset = -6; offset <= 0; offset++) {
    const status: PayrollRunStatus = offset === 0 ? 'Draft' : 'Paid';
    runs.push(buildRun(addMonths(currentMonth, offset), runs.length + 1, status));
  }
  return runs;
}

/** SAMPLE DATA — last six paid periods plus the current draft period. */
export const MOCK_PAYROLL_RUNS: PayrollRun[] = generateRuns();
