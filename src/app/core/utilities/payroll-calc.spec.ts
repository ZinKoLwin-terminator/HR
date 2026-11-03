import { OvertimeRecord } from '../models/attendance.models';
import { LeaveApplication, LeaveType } from '../models/leave.models';
import { SalaryComponent } from '../models/payroll.models';
import {
  calculatePay,
  collectPayrollAdjustments,
  describeComponent,
  PayInput,
} from './payroll-calc';

const comp = (
  overrides: Partial<SalaryComponent> & Pick<SalaryComponent, 'code'>,
): SalaryComponent => ({
  id: 1,
  name: overrides.code,
  type: 'Earning',
  calculation: 'Fixed',
  value: 0,
  description: '',
  status: 'Active',
  ...overrides,
});

const HOUSING = comp({
  id: 1,
  code: 'HOUSING',
  name: 'Housing',
  calculation: 'PercentOfBasic',
  value: 10,
});
const TRANSPORT = comp({
  id: 2,
  code: 'TRANSPORT',
  name: 'Transport',
  calculation: 'Fixed',
  value: 50,
});
const TAX = comp({
  id: 3,
  code: 'TAX',
  name: 'Tax',
  type: 'Deduction',
  calculation: 'PercentOfGross',
  value: 5,
});
const SOCIAL = comp({
  id: 4,
  code: 'SOCIAL',
  name: 'Social',
  type: 'Deduction',
  calculation: 'PercentOfGross',
  value: 2,
});
const PENSION = comp({
  id: 5,
  code: 'PENSION',
  name: 'Pension',
  type: 'Deduction',
  calculation: 'PercentOfBasic',
  value: 3,
});

const NOVEMBER: Omit<PayInput, 'basicSalary' | 'components'> = {
  periodStart: '2026-11-01',
  periodEnd: '2026-11-30',
  joiningDate: '2020-01-01',
  exitDate: null,
  workingDays: 20,
  unpaidLeaveDays: 0,
  overtimeMinutes: 0,
};
const pay = (overrides: Partial<PayInput> = {}) =>
  calculatePay({
    basicSalary: 1000,
    components: [HOUSING, TRANSPORT, TAX, SOCIAL],
    ...NOVEMBER,
    ...overrides,
  });

describe('calculatePay', () => {
  it('computes earnings, gross, deductions on gross, and net for a full month', () => {
    const r = pay()!;
    expect(r.basic).toBe(1000);
    expect(r.earnings.map((l) => [l.code, l.amount])).toEqual([
      ['HOUSING', 100],
      ['TRANSPORT', 50],
    ]);
    expect(r.gross).toBe(1150);
    expect(r.deductions.map((l) => [l.code, l.amount])).toEqual([
      ['TAX', 57.5],
      ['SOCIAL', 23],
    ]);
    expect(r.totalDeductions).toBe(80.5);
    expect(r.net).toBe(1069.5);
    expect(r.daysEmployed).toBe(30);
    expect(r.earnings[0].basis).toBe('10% of basic');
  });

  it('prorates basic and fixed amounts for joiners and leavers', () => {
    // Joined 16 Nov: 15 of 30 days
    const joiner = pay({ joiningDate: '2026-11-16' })!;
    expect(joiner.daysEmployed).toBe(15);
    expect(joiner.basic).toBe(500);
    expect(joiner.earnings.map((l) => l.amount)).toEqual([50, 25]); // 10% of prorated basic, half the fixed amount
    expect(joiner.gross).toBe(575);

    // Left on 10 Nov: 10 of 30 days
    const leaver = pay({ exitDate: '2026-11-10' })!;
    expect(leaver.daysEmployed).toBe(10);
    expect(leaver.basic).toBe(333.33);
  });

  it('returns null when the employee was not employed during the period', () => {
    expect(pay({ joiningDate: '2026-12-01' })).toBeNull();
    expect(pay({ exitDate: '2026-10-31' })).toBeNull();
  });

  it('adds overtime at 1.5× the hourly rate and deducts unpaid leave at the daily rate', () => {
    const r = pay({ overtimeMinutes: 120, unpaidLeaveDays: 2 })!;
    // hourly = 1000 / (20 × 8) = 6.25; 2 h × 6.25 × 1.5 = 18.75
    expect(r.earnings.find((l) => l.code === 'OT')).toMatchObject({
      amount: 18.75,
      name: 'Overtime pay',
    });
    // daily = 1000 / 20 = 50; 2 days = 100
    expect(r.deductions.find((l) => l.code === 'UL')).toMatchObject({
      amount: 100,
      name: 'Unpaid leave',
    });
    expect(r.gross).toBe(1168.75); // overtime is part of gross, so tax is taken on it
    expect(r.deductions.find((l) => l.code === 'TAX')?.amount).toBe(58.44);
    expect(r.net).toBeCloseTo(r.gross - r.totalDeductions, 2);
  });

  it('ignores inactive components and supports percentage-of-basic deductions', () => {
    const r = pay({ components: [HOUSING, { ...TRANSPORT, status: 'Inactive' }, PENSION] })!;
    expect(r.earnings.map((l) => l.code)).toEqual(['HOUSING']);
    expect(r.deductions).toEqual([
      { code: 'PENSION', name: 'Pension', amount: 30, basis: '3% of basic' },
    ]);
  });

  it('describes components', () => {
    expect(describeComponent(HOUSING)).toBe('10% of basic');
    expect(describeComponent(TAX)).toBe('5% of gross');
    expect(describeComponent(TRANSPORT)).toBe('Fixed 50');
  });
});

describe('collectPayrollAdjustments', () => {
  const types = [
    { id: 1, isPaid: true },
    { id: 6, isPaid: false },
  ] as LeaveType[];
  const leave = (o: Partial<LeaveApplication>): LeaveApplication =>
    ({
      employeeId: 1,
      leaveTypeId: 6,
      status: 'Approved',
      halfDay: false,
      startDate: '2026-11-02',
      endDate: '2026-11-03',
      ...o,
    }) as LeaveApplication;
  const ot = (o: Partial<OvertimeRecord>): OvertimeRecord =>
    ({
      employeeId: 1,
      status: 'Approved',
      date: '2026-11-05',
      minutes: 90,
      ...o,
    }) as OvertimeRecord;
  const collect = (
    leaves: LeaveApplication[],
    overtime: OvertimeRecord[],
    holidays = new Set<string>(),
  ) =>
    collectPayrollAdjustments(
      1,
      '2026-11-01',
      '2026-11-30',
      leaves,
      types,
      overtime,
      holidays,
      [0, 6],
    );

  it('counts only approved unpaid leave, clamped to the period and excluding weekends and holidays', () => {
    expect(collect([leave({})], [])).toEqual({ unpaidLeaveDays: 2, overtimeMinutes: 0 });
    expect(collect([leave({ leaveTypeId: 1 })], []).unpaidLeaveDays).toBe(0); // paid leave
    expect(collect([leave({ status: 'Submitted' })], []).unpaidLeaveDays).toBe(0);
    expect(collect([leave({ employeeId: 2 })], []).unpaidLeaveDays).toBe(0);
    // 26 Oct – 4 Nov: only 2 and 3 and 4 Nov fall in the period (Mon–Wed)
    expect(
      collect([leave({ startDate: '2026-10-26', endDate: '2026-11-04' })], []).unpaidLeaveDays,
    ).toBe(3);
    expect(
      collect(
        [leave({ startDate: '2026-11-02', endDate: '2026-11-08' })],
        [],
        new Set(['2026-11-04']),
      ).unpaidLeaveDays,
    ).toBe(4);
    expect(
      collect([leave({ halfDay: true, startDate: '2026-11-02', endDate: '2026-11-02' })], [])
        .unpaidLeaveDays,
    ).toBe(0.5);
  });

  it('sums approved overtime inside the period only', () => {
    const records = [
      ot({}),
      ot({ date: '2026-11-12', minutes: 60 }),
      ot({ date: '2026-10-31', minutes: 200 }),
      ot({ status: 'Pending', minutes: 500 }),
      ot({ employeeId: 2, minutes: 700 }),
    ];
    expect(collect([], records).overtimeMinutes).toBe(150);
  });
});
