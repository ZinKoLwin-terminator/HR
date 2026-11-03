import { MOCK_ATTENDANCE_RECORDS } from './attendance/attendance-records.mock';
import { MOCK_SHIFTS } from './attendance/shifts.mock';
import { MOCK_EMPLOYEES } from './employees/employees.mock';
import { MOCK_LEAVE_APPLICATIONS } from './leave/leave-applications.mock';
import { MOCK_LEAVE_TYPES } from './leave/leave-types.mock';
import { MOCK_BRANCHES } from './organization/branches.mock';
import { MOCK_COMPANIES } from './organization/companies.mock';
import { MOCK_DEPARTMENTS } from './organization/departments.mock';
import { MOCK_JOB_GRADES } from './organization/job-grades.mock';
import { MOCK_POSITIONS } from './organization/positions.mock';
import { MOCK_SECTIONS } from './organization/sections.mock';
import { MOCK_PAYROLL_RUNS } from './payroll/payroll-runs.mock';

const ids = (items: { id: number }[]) => new Set(items.map((i) => i.id));

describe('mock data integrity', () => {
  const companies = ids(MOCK_COMPANIES);
  const branches = ids(MOCK_BRANCHES);
  const departments = ids(MOCK_DEPARTMENTS);
  const sections = ids(MOCK_SECTIONS);
  const grades = ids(MOCK_JOB_GRADES);
  const positions = ids(MOCK_POSITIONS);
  const employees = ids(MOCK_EMPLOYEES);

  it('has unique ids per dataset', () => {
    for (const data of [
      MOCK_COMPANIES,
      MOCK_BRANCHES,
      MOCK_DEPARTMENTS,
      MOCK_EMPLOYEES,
      MOCK_ATTENDANCE_RECORDS,
    ]) {
      expect(ids(data).size).toBe(data.length);
    }
  });

  it('links Company → Branch → Department → Section → Position', () => {
    MOCK_BRANCHES.forEach((b) => expect(companies.has(b.companyId)).toBe(true));
    MOCK_DEPARTMENTS.forEach((d) => {
      expect(branches.has(d.branchId)).toBe(true);
      expect(MOCK_BRANCHES.find((b) => b.id === d.branchId)?.companyId).toBe(d.companyId);
    });
    MOCK_SECTIONS.forEach((s) => expect(departments.has(s.departmentId)).toBe(true));
    MOCK_POSITIONS.forEach((p) => {
      expect(departments.has(p.departmentId)).toBe(true);
      expect(grades.has(p.jobGradeId)).toBe(true);
      if (p.sectionId !== null) {
        expect(sections.has(p.sectionId)).toBe(true);
        expect(MOCK_SECTIONS.find((s) => s.id === p.sectionId)?.departmentId).toBe(p.departmentId);
      }
      if (p.reportsToPositionId !== null) {
        expect(positions.has(p.reportsToPositionId)).toBe(true);
      }
    });
  });

  it('references valid people from the organization datasets', () => {
    const people = [
      ...MOCK_BRANCHES.map((b) => b.managerEmployeeId),
      ...MOCK_DEPARTMENTS.map((d) => d.headEmployeeId),
      ...MOCK_SECTIONS.map((s) => s.supervisorEmployeeId),
    ].filter((id): id is number => id !== null);
    people.forEach((id) => expect(employees.has(id)).toBe(true));
  });

  it('builds consistent employees', () => {
    expect(MOCK_EMPLOYEES).toHaveLength(60);
    const codes = new Set(MOCK_EMPLOYEES.map((e) => e.employeeCode));
    expect(codes.size).toBe(60);
    MOCK_EMPLOYEES.forEach((e) => {
      expect(positions.has(e.positionId)).toBe(true);
      expect(MOCK_POSITIONS.find((p) => p.id === e.positionId)?.departmentId).toBe(e.departmentId);
      expect(MOCK_DEPARTMENTS.find((d) => d.id === e.departmentId)?.branchId).toBe(e.branchId);
      expect(MOCK_SHIFTS.some((s) => s.id === e.shiftId)).toBe(true);
      if (e.managerId !== null) {
        expect(employees.has(e.managerId)).toBe(true);
        expect(e.managerId).not.toBe(e.id);
      }
    });
    // Only the CEO has no manager.
    expect(MOCK_EMPLOYEES.filter((e) => e.managerId === null).map((e) => e.id)).toEqual([1]);
  });

  it('links leave and attendance to employees and types', () => {
    const leaveTypes = ids(MOCK_LEAVE_TYPES);
    MOCK_LEAVE_APPLICATIONS.forEach((l) => {
      expect(employees.has(l.employeeId)).toBe(true);
      expect(leaveTypes.has(l.leaveTypeId)).toBe(true);
      expect(l.endDate >= l.startDate).toBe(true);
      expect(l.days).toBeGreaterThan(0);
    });
    MOCK_ATTENDANCE_RECORDS.forEach((r) => expect(employees.has(r.employeeId)).toBe(true));
  });

  it('marks approved leave days as On Leave in attendance', () => {
    const approved = MOCK_LEAVE_APPLICATIONS.filter((l) => l.status === 'Approved' && !l.halfDay);
    for (const leave of approved) {
      const records = MOCK_ATTENDANCE_RECORDS.filter(
        (r) =>
          r.employeeId === leave.employeeId && r.date >= leave.startDate && r.date <= leave.endDate,
      );
      records.forEach((r) => expect(['On Leave', 'Weekend', 'Holiday']).toContain(r.status));
    }
  });

  it('produces payroll totals that add up', () => {
    MOCK_PAYROLL_RUNS.forEach((run) => {
      expect(run.totalGross).toBe(run.totalBasic + run.totalAllowances);
      expect(run.totalNet).toBe(run.totalGross - run.totalDeductions);
    });
  });
});
