import { Entity, RecordStatus } from './common.models';

/**
 * ALL PAYROLL FIGURES IN THIS APP ARE SAMPLE DATA produced from configurable
 * demonstration rules. They are not real tax, statutory or banking calculations.
 */

export type PayrollRunStatus = 'Draft' | 'Pending Approval' | 'Approved' | 'Paid';

export const PAYROLL_RUN_STATUSES: readonly PayrollRunStatus[] = [
  'Draft',
  'Pending Approval',
  'Approved',
  'Paid',
];

/** One payroll period (a calendar month) and its processing state. */
export interface PayrollRun extends Entity {
  periodName: string;
  year: number;
  /** 1–12 */
  month: number;
  startDate: string;
  endDate: string;
  payDate: string;
  status: PayrollRunStatus;
  employeeCount: number;
  /** Basic salary actually payable (after proration). */
  totalBasic: number;
  /** Earnings above basic (allowances, overtime). totalGross = totalBasic + totalAllowances. */
  totalAllowances: number;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
  /** Notes from processing (e.g. employees skipped for a missing salary). */
  warnings: string[];
  processedOn: string | null;
  /** Who processed the run (API only; the person who processed it cannot approve it). */
  processedBy?: string | null;
  approvedBy: string | null;
  approvedOn: string | null;
  paidOn: string | null;
}

export type ComponentType = 'Earning' | 'Deduction';
export type CalculationMethod = 'Fixed' | 'PercentOfBasic' | 'PercentOfGross';

/** A reusable pay element: an allowance (earning) or a deduction. */
export interface SalaryComponent extends Entity {
  code: string;
  name: string;
  type: ComponentType;
  calculation: CalculationMethod;
  /** Amount for `Fixed`, otherwise a percentage (0–100). */
  value: number;
  description: string;
  status: RecordStatus;
}

/** A named set of components applied to employees (e.g. staff, management, executive). */
export interface SalaryStructure extends Entity {
  code: string;
  name: string;
  description: string;
  componentIds: number[];
  status: RecordStatus;
}

/** An employee's basic salary and structure from a date onwards. Newer rows supersede older ones. */
export interface SalaryAssignment extends Entity {
  employeeId: number;
  structureId: number;
  basicSalary: number;
  effectiveFrom: string;
  remarks: string;
}

export interface PayLine {
  code: string;
  name: string;
  amount: number;
  /** How the amount was worked out, e.g. "10% of basic". */
  basis: string;
}

/** Snapshot of one employee's pay for one period (unaffected by later setup changes). */
export interface Payslip extends Entity {
  runId: number;
  periodName: string;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  positionTitle: string;
  companyName: string;
  bankName: string;
  /** Masked, e.g. "•••• 4821". */
  accountMasked: string;
  /** Basic after proration for part-month employment. */
  basicSalary: number;
  earnings: PayLine[];
  deductions: PayLine[];
  grossSalary: number;
  totalDeductions: number;
  netSalary: number;
  periodDays: number;
  daysEmployed: number;
  workingDays: number;
  unpaidLeaveDays: number;
  overtimeMinutes: number;
  generatedOn: string;
}

export interface PayslipFilter {
  runId: number | null;
  employeeId: number | null;
  departmentId: number | null;
}
