import { LeaveApplication, LeavePolicy, LeaveType } from '../models/leave.models';
import { computeLeaveBalances, resolveEntitlement } from './leave-balance';

const type = (overrides: Partial<LeaveType> = {}): LeaveType => ({
  id: 1,
  code: 'AL',
  name: 'Annual Leave',
  description: '',
  defaultDaysPerYear: 15,
  isPaid: true,
  requiresDocument: false,
  maxConsecutiveDays: 10,
  carryForward: false,
  maxCarryForwardDays: 0,
  color: '#000000',
  eligibleGender: null,
  status: 'Active',
  ...overrides,
});

const policy = (overrides: Partial<LeavePolicy> = {}): LeavePolicy => ({
  id: 1,
  name: 'p',
  leaveTypeId: 1,
  jobGradeId: null,
  employmentType: null,
  daysPerYear: 15,
  accrual: 'Upfront',
  minServiceMonths: 0,
  status: 'Active',
  ...overrides,
});

const employee = { jobGradeId: 3, employmentType: 'Full-time' as const, joiningDate: '2020-01-01' };
const AS_OF = '2026-11-03';

const application = (overrides: Partial<LeaveApplication>): LeaveApplication => ({
  id: 1,
  applicationNo: 'LV-1',
  employeeId: 1,
  leaveTypeId: 1,
  startDate: '2026-03-02',
  endDate: '2026-03-03',
  days: 2,
  halfDay: false,
  reason: '',
  documentName: null,
  status: 'Approved',
  appliedOn: '2026-02-20',
  approverId: null,
  actionedOn: null,
  approverRemarks: null,
  ...overrides,
});

describe('resolveEntitlement', () => {
  it('uses the leave type default when no policy applies', () => {
    expect(resolveEntitlement(type(), [], employee, AS_OF)).toBe(15);
    expect(
      resolveEntitlement(type(), [policy({ jobGradeId: 8, daysPerYear: 22 })], employee, AS_OF),
    ).toBe(15);
  });

  it('prefers the most specific matching policy', () => {
    const policies = [
      policy({ id: 1, daysPerYear: 15 }),
      policy({ id: 2, employmentType: 'Full-time', daysPerYear: 16 }),
      policy({ id: 3, jobGradeId: 3, daysPerYear: 18 }),
      policy({ id: 4, jobGradeId: 3, employmentType: 'Full-time', daysPerYear: 19 }),
    ];
    expect(resolveEntitlement(type(), policies, employee, AS_OF)).toBe(19);
    expect(resolveEntitlement(type(), policies.slice(0, 3), employee, AS_OF)).toBe(18);
    expect(resolveEntitlement(type(), policies.slice(0, 2), employee, AS_OF)).toBe(16);
  });

  it('ignores inactive policies and other leave types', () => {
    const policies = [
      policy({ jobGradeId: 3, daysPerYear: 30, status: 'Inactive' }),
      policy({ id: 2, leaveTypeId: 2, jobGradeId: 3, daysPerYear: 40 }),
    ];
    expect(resolveEntitlement(type(), policies, employee, AS_OF)).toBe(15);
  });

  it('requires the minimum service, falling back to a satisfied policy or to zero', () => {
    const newcomer = { ...employee, joiningDate: '2026-09-15' }; // 1 month of service
    const standard = policy({ daysPerYear: 15, minServiceMonths: 3 });
    expect(resolveEntitlement(type(), [standard], newcomer, AS_OF)).toBe(0);
    expect(resolveEntitlement(type(), [standard], employee, AS_OF)).toBe(15);

    const intern = policy({ id: 2, employmentType: 'Intern', daysPerYear: 6 });
    expect(
      resolveEntitlement(
        type(),
        [standard, intern],
        { ...newcomer, employmentType: 'Intern' },
        AS_OF,
      ),
    ).toBe(6);
  });

  it('accrues monthly in half-day steps', () => {
    const monthly = policy({ daysPerYear: 6, accrual: 'Monthly' });
    expect(resolveEntitlement(type(), [monthly], employee, '2026-03-10')).toBe(1.5);
    expect(resolveEntitlement(type(), [monthly], employee, '2026-11-03')).toBe(5.5);
    expect(resolveEntitlement(type(), [monthly], employee, '2026-12-31')).toBe(6);
  });
});

describe('computeLeaveBalances', () => {
  const types = [
    type(),
    type({
      id: 4,
      code: 'MAT',
      name: 'Maternity',
      eligibleGender: 'Female',
      defaultDaysPerYear: 98,
    }),
  ];

  it('subtracts approved and pending days and ignores other statuses and years', () => {
    const apps = [
      application({ id: 1, days: 3 }),
      application({ id: 2, status: 'Submitted', days: 2, startDate: '2026-05-04' }),
      application({ id: 3, status: 'Rejected', days: 5 }),
      application({ id: 4, status: 'Cancelled', days: 4 }),
      application({ id: 5, status: 'Draft', days: 1 }),
      application({ id: 6, days: 9, startDate: '2025-12-29' }),
    ];
    const [al] = computeLeaveBalances(types, apps, 'Male', 2026);
    expect(al).toMatchObject({ entitled: 15, taken: 3, pending: 2, available: 10 });
  });

  it('never goes below zero and applies the entitlement resolver and gender eligibility', () => {
    const apps = [application({ days: 12 })];
    const [al] = computeLeaveBalances(types, apps, 'Male', 2026, () => 10);
    expect(al).toMatchObject({ entitled: 10, taken: 12, available: 0 });
    expect(computeLeaveBalances(types, [], 'Male', 2026).map((b) => b.code)).toEqual(['AL']);
    expect(computeLeaveBalances(types, [], 'Female', 2026).map((b) => b.code)).toEqual([
      'AL',
      'MAT',
    ]);
  });
});
