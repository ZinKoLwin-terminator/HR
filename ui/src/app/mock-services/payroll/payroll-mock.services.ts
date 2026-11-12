import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PayrollRun, Payslip, PayslipFilter } from '../../core/models/payroll.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { mockError, mockResponse } from '../mock-response';
import { PagedResult } from '../../core/models/common.models';
import {
  MyPayslipDataService,
  PayrollRunDataService,
  PayslipDataService,
} from '../../core/services/data/payroll-data.services';
import {
  calculatePay,
  collectPayrollAdjustments,
  periodWorkingDays,
  round2,
} from '../../core/utilities/payroll-calc';
import { MOCK_PAYROLL_RUNS, MOCK_PAYSLIPS } from '../../mock-data/payroll/payroll-runs.mock';
import { paginate } from '../../shared/utilities/query.util';
import { endOfMonth, startOfMonth, todayIso } from '../../shared/utilities/date.util';
import { HolidayMockService } from '../attendance/attendance-mock.services';
import { OvertimeMockService } from '../attendance/attendance-workflow-mock.services';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { LeaveApplicationMockService, LeaveTypeMockService } from '../leave/leave-mock.services';
import {
  BranchMockService,
  CompanyMockService,
  DepartmentMockService,
  PositionMockService,
} from '../organization/organization-mock.services';
import {
  SalaryAssignmentMockService,
  SalaryComponentMockService,
  SalaryStructureMockService,
} from './payroll-setup-mock.services';

type PayslipDraft = Omit<Payslip, 'id'>;

/** Read-only payslips, created and removed by payroll processing. */
@Injectable({ providedIn: 'root' })
export class PayslipMockService
  extends InMemoryCrudService<Payslip, Payslip, PayslipFilter>
  implements PayslipDataService
{
  protected readonly entityLabel = 'Payslip';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['employeeName', 'employeeCode'] as const;

  constructor() {
    super(MOCK_PAYSLIPS, 'payslips');
  }

  protected override describe(entity: Payslip): string {
    return `${entity.employeeName} · ${entity.periodName}`;
  }

  /** Replaces all payslips of a run with new ones. */
  replaceForRun(runId: number, drafts: PayslipDraft[]): void {
    let next = this.nextId();
    const created = drafts.map((d) => ({ ...d, id: next++ }));
    this.store.update((items) => [...items.filter((p) => p.runId !== runId), ...created]);
  }

  removeForRun(runId: number): void {
    this.store.update((items) => items.filter((p) => p.runId !== runId));
  }
}

/**
 * "My payslips" (demo). Same rules as `GET /api/me/payslips`: only payslips of the signed-in
 * user's own employee, only from Approved or Paid runs, newest period first (page size defaults
 * to 12); a user without a linked employee gets an empty page; anyone else's payslip is NotFound.
 */
@Injectable({ providedIn: 'root' })
export class MyPayslipMockService implements MyPayslipDataService {
  private readonly payslips = inject(PayslipMockService);
  private readonly runs = inject(PayrollRunMockService);
  private readonly user = inject(CurrentUserService);
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;

  private mine(): Payslip[] {
    const employeeId = this.user.current().employeeId;
    if (employeeId === null) return [];
    const released = new Map(
      this.runs
        .records()
        .filter((r) => r.status === 'Approved' || r.status === 'Paid')
        .map((r) => [r.id, r.startDate]),
    );
    return this.payslips
      .records()
      .filter((p) => p.employeeId === employeeId && released.has(p.runId))
      .sort(
        (a, b) =>
          (released.get(b.runId) ?? '').localeCompare(released.get(a.runId) ?? '') || b.id - a.id,
      );
  }

  query(request: { pageIndex: number; pageSize: number }): Observable<PagedResult<Payslip>> {
    return mockResponse(
      () => paginate(this.mine(), request.pageIndex, request.pageSize || 12),
      this.latency,
    );
  }

  getById(id: number): Observable<Payslip> {
    return mockResponse(() => {
      const found = this.mine().find((p) => p.id === id);
      if (!found) {
        throw mockError('NotFound', 'Payslip not found.');
      }
      return found;
    }, this.latency);
  }
}

@Injectable({ providedIn: 'root' })
export class PayrollRunMockService
  extends InMemoryCrudService<PayrollRun>
  implements PayrollRunDataService
{
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly positions = inject(PositionMockService);
  private readonly companies = inject(CompanyMockService);
  private readonly branches = inject(BranchMockService);
  private readonly assignments = inject(SalaryAssignmentMockService);
  private readonly structures = inject(SalaryStructureMockService);
  private readonly components = inject(SalaryComponentMockService);
  private readonly payslips = inject(PayslipMockService);
  private readonly leaves = inject(LeaveApplicationMockService);
  private readonly leaveTypes = inject(LeaveTypeMockService);
  private readonly overtime = inject(OvertimeMockService);
  private readonly holidays = inject(HolidayMockService);
  private readonly user = inject(CurrentUserService);
  private readonly weekendDays = inject(APP_CONFIG).weekendDays;

  protected readonly entityLabel = 'Payroll period';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['periodName', 'status'] as const;

  constructor() {
    super(MOCK_PAYROLL_RUNS, 'payroll_runs');
  }

  protected override describe(entity: PayrollRun): string {
    return entity.periodName;
  }

  /** Derives the period name and dates from the chosen year and month. */
  protected override prepare(candidate: PayrollRun, isNew: boolean): void {
    candidate.year = Number(candidate.year);
    candidate.month = Number(candidate.month);
    candidate.startDate = `${candidate.year}-${String(candidate.month).padStart(2, '0')}-01`;
    candidate.endDate = endOfMonth(candidate.startDate);
    candidate.periodName = new Date(`${candidate.startDate}T00:00:00`).toLocaleString('en-US', {
      month: 'long',
      year: 'numeric',
    });
    candidate.payDate ||= candidate.endDate;
    if (isNew) {
      Object.assign(candidate, {
        status: 'Draft',
        employeeCount: 0,
        totalBasic: 0,
        totalAllowances: 0,
        totalGross: 0,
        totalDeductions: 0,
        totalNet: 0,
        warnings: [],
        processedOn: null,
        approvedBy: null,
        approvedOn: null,
        paidOn: null,
      });
    }
  }

  protected override validate(candidate: PayrollRun, isNew: boolean): void {
    if (!isNew && this.findById(candidate.id)?.status !== 'Draft') {
      this.conflictError('Only a draft period can be changed. Return it to draft first.');
    }
    if (!(candidate.month >= 1 && candidate.month <= 12)) {
      this.validationError('Choose a month.', { month: 'Choose a month.' });
    }
    if (candidate.startDate > startOfMonth(todayIso())) {
      this.validationError('A payroll period cannot be in the future.', {
        month: 'Choose the current or an earlier month.',
      });
    }
    const duplicate = this.store().some(
      (r) => r.id !== candidate.id && r.year === candidate.year && r.month === candidate.month,
    );
    if (duplicate) {
      this.validationError(`A payroll period for ${candidate.periodName} already exists.`, {
        month: 'This period already exists.',
      });
    }
    if (candidate.payDate < candidate.startDate) {
      this.validationError('The pay date cannot be before the period starts.', {
        payDate: 'Must be on or after the period start.',
      });
    }
  }

  protected override beforeDelete(entity: PayrollRun): void {
    if (entity.status !== 'Draft') {
      this.conflictError('Only a draft period can be deleted.');
    }
  }

  private change(id: number, changes: Partial<PayrollRun>): PayrollRun {
    const updated = { ...this.require(id), ...changes };
    this.store.update((items) => items.map((r) => (r.id === id ? updated : r)));
    return updated;
  }

  process(id: number): Observable<PayrollRun> {
    return this.respond(() => {
      const run = this.require(id);
      if (run.status !== 'Draft') {
        this.conflictError(`Only a draft period can be processed (this one is ${run.status}).`);
      }
      const earlier = this.store().find(
        (r) =>
          r.startDate < run.startDate && (r.status === 'Draft' || r.status === 'Pending Approval'),
      );
      if (earlier) {
        this.conflictError(
          `Finish ${earlier.periodName} (${earlier.status}) before processing ${run.periodName}.`,
        );
      }

      const holidaySet = new Set(this.holidays.records().map((h) => h.date));
      const workingDays = periodWorkingDays(
        run.startDate,
        run.endDate,
        holidaySet,
        this.weekendDays,
      );
      const drafts: PayslipDraft[] = [];
      const warnings: string[] = [];

      for (const e of this.employees.records()) {
        if (e.joiningDate > run.endDate || (e.exitDate && e.exitDate < run.startDate)) {
          continue; // not employed in this period
        }
        const label = `${e.employeeCode} ${e.firstName} ${e.lastName}`;
        const assignment = this.assignments.currentFor(e.id, run.endDate);
        const structure = assignment ? this.structures.findById(assignment.structureId) : undefined;
        if (!assignment || !structure) {
          warnings.push(`${label} has no salary assignment and was skipped.`);
          continue;
        }
        const components = structure.componentIds
          .map((cid) => this.components.findById(cid))
          .filter((c): c is NonNullable<typeof c> => !!c);
        const adjustments = collectPayrollAdjustments(
          e.id,
          run.startDate,
          run.endDate,
          this.leaves.records(),
          this.leaveTypes.records(),
          this.overtime.records(),
          holidaySet,
          this.weekendDays,
        );
        const pay = calculatePay({
          basicSalary: assignment.basicSalary,
          components,
          periodStart: run.startDate,
          periodEnd: run.endDate,
          joiningDate: e.joiningDate,
          exitDate: e.exitDate,
          workingDays,
          ...adjustments,
        });
        if (!pay) continue;
        drafts.push({
          runId: id,
          periodName: run.periodName,
          employeeId: e.id,
          employeeCode: e.employeeCode,
          employeeName: `${e.firstName} ${e.lastName}`,
          departmentId: e.departmentId,
          departmentName: this.departments.findById(e.departmentId)?.name ?? '—',
          positionTitle: this.positions.findById(e.positionId)?.title ?? '—',
          companyName: this.companies.findById(e.companyId)?.name ?? '—',
          bankName: e.bank.bankName,
          accountMasked: `•••• ${e.bank.accountNumber.slice(-4)}`,
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
          generatedOn: todayIso(),
        });
      }
      if (drafts.length === 0) {
        this.conflictError('There are no employees to pay in this period.');
      }

      this.payslips.replaceForRun(id, drafts);
      const sum = (pick: (p: PayslipDraft) => number) =>
        round2(drafts.reduce((t, p) => t + pick(p), 0));
      const totalBasic = sum((p) => p.basicSalary);
      const totalGross = sum((p) => p.grossSalary);
      const processed = this.change(id, {
        status: 'Pending Approval',
        employeeCount: drafts.length,
        totalBasic,
        totalAllowances: round2(totalGross - totalBasic),
        totalGross,
        totalDeductions: sum((p) => p.totalDeductions),
        totalNet: sum((p) => p.netSalary),
        warnings,
        processedOn: todayIso(),
        approvedBy: null,
        approvedOn: null,
        paidOn: null,
      });
      this.activity.record(
        'payroll',
        `Payroll for ${run.periodName} processed (${drafts.length} payslips)`,
      );
      return processed;
    });
  }

  approve(id: number): Observable<PayrollRun> {
    return this.respond(() => {
      const run = this.require(id);
      if (run.status !== 'Pending Approval') {
        this.conflictError(
          `Only a period pending approval can be approved (this one is ${run.status}).`,
        );
      }
      const approved = this.change(id, {
        status: 'Approved',
        approvedBy: this.user.current().displayName,
        approvedOn: todayIso(),
      });
      this.activity.record('payroll', `Payroll for ${run.periodName} approved`);
      return approved;
    });
  }

  markPaid(id: number): Observable<PayrollRun> {
    return this.respond(() => {
      const run = this.require(id);
      if (run.status !== 'Approved') {
        this.conflictError(
          `Only an approved period can be marked as paid (this one is ${run.status}).`,
        );
      }
      const paid = this.change(id, { status: 'Paid', paidOn: todayIso() });
      this.activity.record('payroll', `Payroll for ${run.periodName} marked as paid (sample)`);
      return paid;
    });
  }

  returnToDraft(id: number, reason: string): Observable<PayrollRun> {
    return this.respond(() => {
      const run = this.require(id);
      if (run.status !== 'Pending Approval' && run.status !== 'Approved') {
        this.conflictError(
          `Only a period pending approval or approved can be returned to draft (this one is ${run.status}).`,
        );
      }
      this.payslips.removeForRun(id);
      const draft = this.change(id, {
        status: 'Draft',
        employeeCount: 0,
        totalBasic: 0,
        totalAllowances: 0,
        totalGross: 0,
        totalDeductions: 0,
        totalNet: 0,
        warnings: [],
        processedOn: null,
        approvedBy: null,
        approvedOn: null,
        paidOn: null,
      });
      this.activity.record(
        'payroll',
        `Payroll for ${run.periodName} returned to draft${reason.trim() ? `: ${reason.trim()}` : ''}`,
      );
      return draft;
    });
  }
}
