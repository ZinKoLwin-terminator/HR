import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { PayrollRun, SalaryAssignment } from '../../core/models/payroll.models';
import { OvertimeDataService } from '../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { EmployeeSalaryDataService } from '../../core/services/data/employee-extras-data.services';
import { LeaveApplicationDataService } from '../../core/services/data/leave-data.services';
import {
  PayrollRunDataService,
  PayslipDataService,
  SalaryAssignmentDataService,
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../core/services/data/payroll-data.services';
import { DashboardDataService } from '../../core/services/data/dashboard-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import {
  countWorkingDays,
  endOfMonth,
  startOfMonth,
  todayIso,
  addDays,
  dayOfWeek,
} from '../../shared/utilities/date.util';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('payroll data', () => {
  let components: SalaryComponentDataService;
  let structures: SalaryStructureDataService;
  let assignments: SalaryAssignmentDataService;
  let runs: PayrollRunDataService;
  let payslips: PayslipDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    components = TestBed.inject(SalaryComponentDataService);
    structures = TestBed.inject(SalaryStructureDataService);
    assignments = TestBed.inject(SalaryAssignmentDataService);
    runs = TestBed.inject(PayrollRunDataService);
    payslips = TestBed.inject(PayslipDataService);
  });

  const today = todayIso();
  const monthStart = startOfMonth(today);
  const currentDraft = async (): Promise<PayrollRun> =>
    (await firstValueFrom(runs.getAll())).find((r) => r.status === 'Draft')!;
  const slipsOf = async (runId: number, extra: object = {}) =>
    (
      await firstValueFrom(
        payslips.query({ pageIndex: 0, pageSize: 500, filters: { runId, ...extra } }),
      )
    ).items;

  describe('components and structures', () => {
    const newComponent = {
      code: 'BONUS',
      name: 'Bonus',
      type: 'Earning' as const,
      calculation: 'Fixed' as const,
      value: 100,
      description: '',
      status: 'Active' as const,
    };

    it('validates components and protects those used by a structure', async () => {
      await expectError(
        firstValueFrom(components.create({ ...newComponent, code: 'TAX' })),
        'Validation',
      );
      await expectError(
        firstValueFrom(components.create({ ...newComponent, value: 0 })),
        'Validation',
      );
      const percent = await expectError(
        firstValueFrom(
          components.create({ ...newComponent, calculation: 'PercentOfBasic', value: 150 }),
        ),
        'Validation',
      );
      expect(percent.fieldErrors?.['value']).toBeTruthy();

      const used = await expectError(firstValueFrom(components.delete(1)), 'Conflict'); // Housing allowance
      expect(used.message).toContain('salary structure(s)');
      const created = await firstValueFrom(components.create(newComponent));
      await firstValueFrom(components.delete(created.id));
    });

    it('validates structures and protects those assigned to employees', async () => {
      const base = { code: 'SALES', name: 'Sales', description: '', status: 'Active' as const };
      await expectError(
        firstValueFrom(structures.create({ ...base, componentIds: [] })),
        'Validation',
      );
      const noEarning = await expectError(
        firstValueFrom(structures.create({ ...base, componentIds: [6, 7] })),
        'Validation',
      );
      expect(noEarning.fieldErrors?.['componentIds']).toBeTruthy();
      await expectError(
        firstValueFrom(structures.create({ ...base, componentIds: [1, 999] })),
        'Validation',
      );
      await expectError(
        firstValueFrom(structures.create({ ...base, code: 'STAFF', componentIds: [1] })),
        'Validation',
      );

      const created = await firstValueFrom(structures.create({ ...base, componentIds: [1, 2, 6] }));
      await firstValueFrom(structures.delete(created.id));
      const inUse = await expectError(firstValueFrom(structures.delete(1)), 'Conflict');
      expect(inUse.message).toContain('salary assignment(s)');
    });
  });

  describe('salary assignments', () => {
    const model = (overrides: Partial<SalaryAssignment> = {}): Omit<SalaryAssignment, 'id'> => ({
      employeeId: 15, // grade G3: band 900 – 1,500
      structureId: 1,
      basicSalary: 1200,
      effectiveFrom: today,
      remarks: 'Revision',
      ...overrides,
    });

    it('enforces the grade band, joining date and one assignment per start date', async () => {
      const band = await expectError(
        firstValueFrom(assignments.create(model({ basicSalary: 5000 }))),
        'Validation',
      );
      expect(band.fieldErrors?.['basicSalary']).toMatch(/G3 band/);
      await expectError(
        firstValueFrom(assignments.create(model({ basicSalary: 100 }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(assignments.create(model({ effectiveFrom: '2000-01-01' }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(assignments.create(model({ employeeId: 999 }))),
        'Validation',
      );
      await expectError(
        firstValueFrom(assignments.create(model({ structureId: 999 }))),
        'Validation',
      );

      await firstValueFrom(assignments.create(model()));
      const clash = await expectError(
        firstValueFrom(assignments.create(model({ basicSalary: 1250 }))),
        'Validation',
      );
      expect(clash.fieldErrors?.['effectiveFrom']).toBeTruthy();
    });

    it('keeps at least one assignment per employee and uses the latest one in force', async () => {
      const all = await firstValueFrom(assignments.getAll());
      const mine = all
        .filter((a) => a.employeeId === 15)
        .sort((a, b) => a.effectiveFrom.localeCompare(b.effectiveFrom));
      expect(mine.length).toBe(2); // starting salary + this year's revision

      await firstValueFrom(assignments.delete(mine[0].id)); // the earlier one can go
      const error = await expectError(firstValueFrom(assignments.delete(mine[1].id)), 'Conflict');
      expect(error.message).toMatch(/at least one salary assignment/);
    });

    it('drives the salary summary shown on the employee profile', async () => {
      const summary = TestBed.inject(EmployeeSalaryDataService);
      const before = await firstValueFrom(summary.getSummary(15));
      await firstValueFrom(assignments.create(model({ basicSalary: 1400 })));
      const after = await firstValueFrom(summary.getSummary(15));
      expect(after.basic).toBe(1400);
      expect(after.basic).not.toBe(before.basic);
      expect(after.gross).toBeCloseTo(after.basic + after.allowances, 2);
      expect(after.net).toBeCloseTo(after.gross - after.deductions, 2);
      expect(after.assumptions.join(' ')).toMatch(/Staff structure/);
      expect(after.assumptions.join(' ')).toMatch(/not real tax/i);
    });
  });

  describe('payroll periods', () => {
    const period = (year: number, month: number, overrides: Partial<PayrollRun> = {}) =>
      ({
        year,
        month,
        payDate: '',
        periodName: '',
        startDate: '',
        endDate: '',
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
        ...overrides,
      }) as Omit<PayrollRun, 'id'>;

    it('seeds six paid periods with payslips and one draft', async () => {
      const all = await firstValueFrom(runs.getAll());
      expect(all).toHaveLength(7);
      expect(all.filter((r) => r.status === 'Paid')).toHaveLength(6);
      const draft = await currentDraft();
      expect(draft).toMatchObject({ startDate: monthStart, employeeCount: 0, totalNet: 0 });
      expect(await slipsOf(draft.id)).toHaveLength(0);
      const paid = all.find((r) => r.status === 'Paid')!;
      expect((await slipsOf(paid.id)).length).toBe(paid.employeeCount);
    });

    it('creates a period from a year and month, rejecting future, duplicate and invalid ones', async () => {
      const year = new Date().getFullYear();
      const created = await firstValueFrom(runs.create(period(year, 1)));
      expect(created).toMatchObject({
        periodName: `January ${year}`,
        startDate: `${year}-01-01`,
        endDate: `${year}-01-31`,
        payDate: `${year}-01-31`,
        status: 'Draft',
      });

      const dup = await expectError(firstValueFrom(runs.create(period(year, 1))), 'Validation');
      expect(dup.fieldErrors?.['month']).toBeTruthy();
      await expectError(firstValueFrom(runs.create(period(year + 1, 1))), 'Validation'); // future
      await expectError(firstValueFrom(runs.create(period(year, 13))), 'Validation');
      await expectError(
        firstValueFrom(runs.create(period(year, 2, { payDate: `${year - 1}-01-01` }))),
        'Validation',
      );

      const edited = await firstValueFrom(runs.update(created.id, { month: 2, year, payDate: '' }));
      expect(edited.periodName).toBe(`February ${year}`);
      await firstValueFrom(runs.delete(created.id));
    });

    it('processes a period into payslips whose totals match, and snapshots the setup', async () => {
      const draft = await currentDraft();
      const processed = await firstValueFrom(runs.process(draft.id));
      expect(processed.status).toBe('Pending Approval');
      expect(processed.processedOn).toBe(today);
      expect(processed.employeeCount).toBeGreaterThan(55);

      const slips = await slipsOf(draft.id);
      expect(slips).toHaveLength(processed.employeeCount);
      const cents = (n: number) => Math.round(n * 100);
      expect(cents(slips.reduce((s, p) => s + p.netSalary, 0))).toBe(cents(processed.totalNet));
      expect(cents(processed.totalGross)).toBe(
        cents(processed.totalBasic + processed.totalAllowances),
      );
      for (const p of slips) {
        expect(cents(p.netSalary)).toBe(cents(p.grossSalary - p.totalDeductions));
        expect(p.periodName).toBe(draft.periodName);
        expect(p.earnings.some((l) => l.code === 'HOUSING')).toBe(true);
        expect(p.deductions.some((l) => l.code === 'TAX')).toBe(true);
      }

      // Later setup changes do not alter an issued payslip.
      const before = slips.find((p) => p.employeeCode === 'EMP001')!;
      await firstValueFrom(components.update(1, { value: 50 })); // housing 10% → 50%
      const same = await firstValueFrom(payslips.getById(before.id));
      expect(same.earnings.find((l) => l.code === 'HOUSING')?.amount).toBe(
        before.earnings.find((l) => l.code === 'HOUSING')?.amount,
      );
    });

    it('moves through approval and payment and can be reopened until paid', async () => {
      const draft = await currentDraft();
      await expectError(firstValueFrom(runs.approve(draft.id)), 'Conflict'); // not processed yet
      await expectError(firstValueFrom(runs.markPaid(draft.id)), 'Conflict');
      await expectError(firstValueFrom(runs.returnToDraft(draft.id, '')), 'Conflict');

      await firstValueFrom(runs.process(draft.id));
      await expectError(firstValueFrom(runs.process(draft.id)), 'Conflict');
      await expectError(firstValueFrom(runs.delete(draft.id)), 'Conflict');
      await expectError(firstValueFrom(runs.update(draft.id, { month: 1 })), 'Conflict');
      await expectError(firstValueFrom(runs.markPaid(draft.id)), 'Conflict'); // must be approved first

      const approved = await firstValueFrom(runs.approve(draft.id));
      expect(approved).toMatchObject({
        status: 'Approved',
        approvedBy: 'Su Myat Noe',
        approvedOn: today,
      });

      const reopened = await firstValueFrom(runs.returnToDraft(draft.id, 'Wrong allowance'));
      expect(reopened).toMatchObject({
        status: 'Draft',
        employeeCount: 0,
        totalNet: 0,
        approvedBy: null,
      });
      expect(await slipsOf(draft.id)).toHaveLength(0);

      await firstValueFrom(runs.process(draft.id));
      await firstValueFrom(runs.approve(draft.id));
      const paid = await firstValueFrom(runs.markPaid(draft.id));
      expect(paid).toMatchObject({ status: 'Paid', paidOn: today });
      await expectError(firstValueFrom(runs.returnToDraft(draft.id, '')), 'Conflict');
    });

    it('makes earlier periods finish first', async () => {
      const year = new Date().getFullYear();
      const early = await firstValueFrom(runs.create(period(year, 1)));
      const draft = await currentDraft();
      const blocked = await expectError(firstValueFrom(runs.process(draft.id)), 'Conflict');
      expect(blocked.message).toContain(`Finish January ${year} (Draft)`);
      await firstValueFrom(runs.delete(early.id));
      await firstValueFrom(runs.process(draft.id));
    });

    it('warns about employees without a salary assignment and skips them', async () => {
      const employees = TestBed.inject(EmployeeDataService);
      const template = await firstValueFrom(employees.getById(60));
      const { id: _id, ...model } = template;
      void _id;
      const created = await firstValueFrom(
        employees.create({
          ...model,
          employeeCode: 'EMP061',
          workEmail: 'nobody@apexhorizon.example',
          joiningDate: monthStart,
          managerId: null,
        }),
      );
      const draft = await currentDraft();
      const processed = await firstValueFrom(runs.process(draft.id));
      expect(processed.warnings).toEqual([expect.stringContaining('EMP061')]);
      expect(await slipsOf(draft.id, { employeeId: created.id })).toHaveLength(0);
    });

    it('includes approved overtime and unpaid leave from the other modules', async () => {
      const overtime = TestBed.inject(OvertimeDataService);
      const leave = TestBed.inject(LeaveApplicationDataService);
      const employeeId = 5;
      const taken = new Set(
        (await firstValueFrom(overtime.getAll()))
          .filter((o) => o.employeeId === employeeId)
          .map((o) => o.date),
      );
      const otDate = Array.from({ length: Number(today.slice(8)) }, (_, i) =>
        addDays(monthStart, i),
      ).find((d) => !taken.has(d))!;
      const ot = await firstValueFrom(
        overtime.create({
          employeeId,
          date: otDate,
          startTime: '18:00',
          endTime: '20:00',
          minutes: 0,
          reason: 'Release',
          status: 'Pending',
          requestedOn: '',
          decidedBy: null,
          decidedOn: null,
          remarks: '',
        }),
      );
      await firstValueFrom(overtime.decide(ot.id, 'Approved', ''));

      // Unpaid leave (type 6) on the first working day(s) of the month for another employee
      let start = monthStart;
      while ([0, 6].includes(dayOfWeek(start))) start = addDays(start, 1);
      const end = addDays(start, 1);
      const unpaidId = 15;
      const application = await firstValueFrom(
        leave.create({
          applicationNo: '',
          employeeId: unpaidId,
          leaveTypeId: 6,
          startDate: start,
          endDate: end,
          days: 0,
          halfDay: false,
          reason: 'Personal',
          documentName: null,
          status: 'Draft',
          appliedOn: '',
          approverId: null,
          actionedOn: null,
          approverRemarks: null,
        }),
      );
      await firstValueFrom(leave.submit(application.id));
      await firstValueFrom(leave.decide(application.id, 'Approved', ''));
      const expectedDays = countWorkingDays(start, end, [0, 6], new Set());

      const draft = await currentDraft();
      await firstValueFrom(runs.process(draft.id));
      const overtimeSlip = (await slipsOf(draft.id, { employeeId }))[0];
      const line = overtimeSlip.earnings.find((l) => l.code === 'OT')!;
      expect(overtimeSlip.overtimeMinutes).toBeGreaterThanOrEqual(120);
      expect(line.amount).toBeGreaterThan(0);

      const unpaidSlip = (await slipsOf(draft.id, { employeeId: unpaidId }))[0];
      expect(unpaidSlip.unpaidLeaveDays).toBeGreaterThanOrEqual(expectedDays - 0.01);
      expect(unpaidSlip.deductions.find((l) => l.code === 'UL')!.amount).toBeGreaterThan(0);
    });
  });

  describe('payslips', () => {
    it('filters by period, employee and department and searches by name', async () => {
      const all = await firstValueFrom(runs.getAll());
      const latestPaid = all
        .filter((r) => r.status === 'Paid')
        .sort((a, b) => b.startDate.localeCompare(a.startDate))[0];
      const mine = await slipsOf(latestPaid.id, { employeeId: 3 });
      expect(mine).toHaveLength(1);
      expect(mine[0]).toMatchObject({
        employeeCode: 'EMP003',
        departmentName: 'Engineering',
        periodName: latestPaid.periodName,
      });

      const eng = await slipsOf(latestPaid.id, { departmentId: 4 });
      expect(eng.every((p) => p.departmentName === 'Engineering')).toBe(true);
      const found = (
        await firstValueFrom(
          payslips.query({
            pageIndex: 0,
            pageSize: 20,
            search: 'carter',
            filters: { runId: latestPaid.id },
          }),
        )
      ).items;
      expect(found.map((p) => p.employeeCode)).toEqual(['EMP005']);
      await expectError(firstValueFrom(payslips.getById(99999)), 'NotFound');
    });

    it('feeds the dashboard with the latest processed (not draft) period', async () => {
      const summary = (await firstValueFrom(TestBed.inject(DashboardDataService).getSummary()))
        .payroll!;
      expect(summary.status).toBe('Paid');
      const draft = await currentDraft();
      expect(summary.periodName).not.toBe(draft.periodName);
      expect(summary.totalNet).toBeGreaterThan(0);
    });
  });

  it('ends a month at the right date', () => {
    expect(endOfMonth('2026-02-10')).toBe('2026-02-28');
  });
});
