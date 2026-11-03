import { Entity } from './common.models';

export type PayrollRunStatus = 'Draft' | 'Processing' | 'Pending Approval' | 'Approved' | 'Paid';

/**
 * Summary of one payroll period. All figures are SAMPLE data produced from
 * configurable sample rules — not real tax or statutory calculations.
 */
export interface PayrollRun extends Entity {
  periodName: string;
  year: number;
  /** 1-12 */
  month: number;
  startDate: string;
  endDate: string;
  payDate: string;
  status: PayrollRunStatus;
  employeeCount: number;
  totalBasic: number;
  totalAllowances: number;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
}
