import { DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { PayrollRun, PayrollRunStatus, Payslip } from '../../core/models/payroll.models';
import {
  calculatePay,
  collectPayrollAdjustments,
  periodWorkingDays,
  round2,
} from '../../core/utilities/payroll-calc';
import { addMonths, endOfMonth, startOfMonth, todayIso } from '../../shared/utilities/date.util';
import { MOCK_HOLIDAYS } from '../attendance/holidays.mock';
import { MOCK_OVERTIME_RECORDS } from '../attendance/overtime.mock';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { MOCK_LEAVE_APPLICATIONS } from '../leave/leave-applications.mock';
import { MOCK_LEAVE_TYPES } from '../leave/leave-types.mock';
import { MOCK_BRANCHES } from '../organization/branches.mock';
import { MOCK_COMPANIES } from '../organization/companies.mock';
import { MOCK_DEPARTMENTS } from '../organization/departments.mock';
import { MOCK_POSITIONS } from '../organization/positions.mock';
import {
  MOCK_SALARY_ASSIGNMENTS,
  MOCK_SALARY_COMPONENTS,
  MOCK_SALARY_STRUCTURES,
} from './salary-setup.mock';

const HOLIDAYS = new Set(MOCK_HOLIDAYS.map((h) => h.date));

/** Snapshot pieces of an employee that appear on a payslip. */
export function payslipIdentity(employeeId: number) {
  const e = MOCK_EMPLOYEES.find((x) => x.id === employeeId)!;
  return {
    employeeCode: e.employeeCode,
    employeeName: `${e.firstName} ${e.lastName}`,
    departmentId: e.departmentId,
    departmentName: MOCK_DEPARTMENTS.find((d) => d.id === e.departmentId)?.name ?? '—',
    positionTitle: MOCK_POSITIONS.find((p) => p.id === e.positionId)?.title ?? '—',
    companyName: MOCK_COMPANIES.find((c) => c.id === e.companyId)?.name ?? '—',
    branchName: MOCK_BRANCHES.find((b) => b.id === e.branchId)?.name ?? '—',
    bankName: e.bank.bankName,
    accountMasked: `•••• ${e.bank.accountNumber.slice(-4)}`,
  };
}

interface GeneratedRun {
  run: PayrollRun;
  payslips: Payslip[];
}

function buildRun(
  monthStart: string,
  id: number,
  status: PayrollRunStatus,
  firstPayslipId: number,
): GeneratedRun {
  const startDate = monthStart;
  const endDate = endOfMonth(monthStart);
  const date = new Date(`${monthStart}T00:00:00`);
  const periodName = date.toLocaleString('en-US', { month: 'long', year: 'numeric' });
  const workingDays = periodWorkingDays(
    startDate,
    endDate,
    HOLIDAYS,
    DEFAULT_APP_CONFIG.weekendDays,
  );
  const payslips: Payslip[] = [];

  if (status !== 'Draft') {
    for (const e of MOCK_EMPLOYEES) {
      const assignment = MOCK_SALARY_ASSIGNMENTS.filter(
        (a) => a.employeeId === e.id && a.effectiveFrom <= endDate,
      ).sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0];
      if (!assignment) continue;
      const structure = MOCK_SALARY_STRUCTURES.find((s) => s.id === assignment.structureId)!;
      const components = structure.componentIds
        .map((cid) => MOCK_SALARY_COMPONENTS.find((c) => c.id === cid)!)
        .filter(Boolean);
      const adjustments = collectPayrollAdjustments(
        e.id,
        startDate,
        endDate,
        MOCK_LEAVE_APPLICATIONS,
        MOCK_LEAVE_TYPES,
        MOCK_OVERTIME_RECORDS,
        HOLIDAYS,
        DEFAULT_APP_CONFIG.weekendDays,
      );
      const pay = calculatePay({
        basicSalary: assignment.basicSalary,
        components,
        periodStart: startDate,
        periodEnd: endDate,
        joiningDate: e.joiningDate,
        exitDate: e.exitDate,
        workingDays,
        ...adjustments,
      });
      if (!pay) continue;
      const who = payslipIdentity(e.id);
      payslips.push({
        id: firstPayslipId + payslips.length,
        runId: id,
        periodName,
        employeeId: e.id,
        employeeCode: who.employeeCode,
        employeeName: who.employeeName,
        departmentId: who.departmentId,
        departmentName: who.departmentName,
        positionTitle: who.positionTitle,
        companyName: who.companyName,
        bankName: who.bankName,
        accountMasked: who.accountMasked,
        basicSalary: pay.basic,
        earnings: pay.earnings,
        deductions: pay.deductions,
        grossSalary: pay.gross,
        totalDeductions: pay.totalDeductions,
        netSalary: pay.net,
        periodDays: pay.periodDays,
        daysEmployed: pay.daysEmployed,
        workingDays,
        unpaidLeaveDays: adjustments.unpaidLeaveDays,
        overtimeMinutes: adjustments.overtimeMinutes,
        generatedOn: endDate,
      });
    }
  }

  const sum = (pick: (p: Payslip) => number) =>
    round2(payslips.reduce((total, p) => total + pick(p), 0));
  const totalBasic = sum((p) => p.basicSalary);
  const totalGross = sum((p) => p.grossSalary);
  const paid = status === 'Paid';
  const today = todayIso();
  return {
    run: {
      id,
      periodName,
      year: date.getFullYear(),
      month: date.getMonth() + 1,
      startDate,
      endDate,
      payDate: endDate,
      status,
      employeeCount: payslips.length,
      totalBasic,
      totalAllowances: round2(totalGross - totalBasic),
      totalGross,
      totalDeductions: sum((p) => p.totalDeductions),
      totalNet: sum((p) => p.netSalary),
      warnings: [],
      processedOn: status === 'Draft' ? null : endDate < today ? endDate : today,
      approvedBy: paid ? 'Su Myat Noe' : null,
      approvedOn: paid ? endDate : null,
      paidOn: paid ? endDate : null,
    },
    payslips,
  };
}

function generate(): GeneratedRun[] {
  const currentMonth = startOfMonth(todayIso());
  const result: GeneratedRun[] = [];
  let nextPayslip = 1;
  for (let offset = -6; offset <= 0; offset++) {
    const generated = buildRun(
      addMonths(currentMonth, offset),
      result.length + 1,
      offset === 0 ? 'Draft' : 'Paid',
      nextPayslip,
    );
    nextPayslip += generated.payslips.length;
    result.push(generated);
  }
  return result;
}

const GENERATED = generate();

/** SAMPLE DATA — the last six months are paid; the current month is an unprocessed draft. */
export const MOCK_PAYROLL_RUNS: PayrollRun[] = GENERATED.map((g) => g.run);
/** SAMPLE DATA — payslips for the paid periods. */
export const MOCK_PAYSLIPS: Payslip[] = GENERATED.flatMap((g) => g.payslips);
