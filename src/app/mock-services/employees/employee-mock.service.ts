import { Injectable, Injector, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import {
  Employee,
  EmployeeFilter,
  EmployeeListItem,
  fullName,
} from '../../core/models/employee.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { isDateInRange, todayIso, yearsBetween } from '../../shared/utilities/date.util';
import { matchesEqualityFilters } from '../../shared/utilities/query.util';
import { ShiftMockService } from '../attendance/attendance-mock.services';
import { InMemoryCrudService } from '../in-memory-crud.service';
// Import cycle with the extras file is safe: classes are only used inside methods / injection.
import {
  EmployeeDocumentMockService,
  EmploymentHistoryMockService,
} from './employee-extras-mock.services';
import {
  BranchMockService,
  CompanyMockService,
  DepartmentMockService,
  JobGradeMockService,
  PositionMockService,
  SectionMockService,
} from '../organization/organization-mock.services';

@Injectable({ providedIn: 'root' })
export class EmployeeMockService
  extends InMemoryCrudService<Employee, EmployeeListItem, EmployeeFilter>
  implements EmployeeDataService
{
  protected readonly entityLabel = 'Employee';
  protected readonly activityCategory = 'employee';
  protected override readonly searchFields = [
    'employeeCode',
    'fullName',
    'workEmail',
    'positionTitle',
  ] as const;

  private readonly companies = inject(CompanyMockService);
  private readonly sections = inject(SectionMockService);
  private readonly grades = inject(JobGradeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly branches = inject(BranchMockService);
  private readonly positions = inject(PositionMockService);
  private readonly injector = inject(Injector);
  /** Resolved lazily: the attendance services depend on this one. */
  private readonly shifts = () => this.injector.get(ShiftMockService);
  private readonly history = inject(EmploymentHistoryMockService);
  private readonly documents = inject(EmployeeDocumentMockService);

  constructor() {
    super(MOCK_EMPLOYEES);
  }

  protected override describe(entity: Employee): string {
    return `${fullName(entity)} (${entity.employeeCode})`;
  }

  protected override toListItem(e: Employee): EmployeeListItem {
    return {
      id: e.id,
      employeeCode: e.employeeCode,
      fullName: fullName(e),
      firstName: e.firstName,
      lastName: e.lastName,
      workEmail: e.workEmail,
      photoUrl: e.photoUrl,
      gender: e.gender,
      companyId: e.companyId,
      branchId: e.branchId,
      branchName: this.branches.findById(e.branchId)?.name ?? '—',
      departmentId: e.departmentId,
      departmentName: this.departments.findById(e.departmentId)?.name ?? '—',
      positionId: e.positionId,
      positionTitle: this.positions.findById(e.positionId)?.title ?? '—',
      employmentType: e.employmentType,
      employmentStatus: e.employmentStatus,
      joiningDate: e.joiningDate,
      shiftId: e.shiftId,
      shiftName: e.shiftId ? (this.shifts().findById(e.shiftId)?.name ?? '—') : '—',
    };
  }

  protected override matchesFilters(
    item: EmployeeListItem,
    filters: Partial<EmployeeFilter>,
  ): boolean {
    const { joiningFrom, joiningTo, ...equality } = filters;
    return (
      matchesEqualityFilters(item, equality) &&
      isDateInRange(item.joiningDate, joiningFrom, joiningTo)
    );
  }

  protected override validate(candidate: Employee, isNew: boolean): void {
    this.ensureUnique(candidate, 'employeeCode', 'Employee code');
    this.ensureUnique(candidate, 'workEmail', 'Work email');
    if (candidate.managerId === candidate.id) {
      this.validationError('An employee cannot be their own manager.', {
        managerId: 'Choose a different manager.',
      });
    }
    this.validateDetails(candidate, isNew ? undefined : this.findById(candidate.id));
  }

  /**
   * Rules every employee must satisfy, however it is saved (form or Excel import): age, dates and
   * a consistent, existing organization assignment.
   */
  private validateDetails(candidate: Employee, stored: Employee | undefined): void {
    const errors: Record<string, string> = {};
    const fail = (field: string, message: string) => (errors[field] ??= message);
    const today = todayIso();

    if (candidate.dateOfBirth) {
      if (candidate.dateOfBirth > today) {
        fail('dateOfBirth', 'The date of birth cannot be in the future.');
      } else if (yearsBetween(candidate.dateOfBirth, today) < 18) {
        fail('dateOfBirth', 'Employees must be at least 18 years old.');
      }
    }
    if (candidate.probationEndDate && candidate.probationEndDate < candidate.joiningDate) {
      fail('probationEndDate', 'The probation cannot end before the joining date.');
    }
    if (candidate.exitDate && candidate.exitDate < candidate.joiningDate) {
      fail('exitDate', 'The exit date cannot be before the joining date.');
    }
    if (
      (candidate.employmentStatus === 'Resigned' || candidate.employmentStatus === 'Terminated') &&
      !candidate.exitDate
    ) {
      fail('exitDate', 'An exit date is required for resigned or terminated employees.');
    }

    const company = this.companies.findById(candidate.companyId);
    const branch = this.branches.findById(candidate.branchId);
    const department = this.departments.findById(candidate.departmentId);
    const position = this.positions.findById(candidate.positionId);
    if (!company) fail('companyId', 'Choose an existing company.');
    if (!branch) fail('branchId', 'Choose an existing branch.');
    else if (company && branch.companyId !== company.id) {
      fail('branchId', 'This branch does not belong to the chosen company.');
    }
    if (!department) fail('departmentId', 'Choose an existing department.');
    else if (branch && department.branchId !== branch.id) {
      fail('departmentId', 'This department does not belong to the chosen branch.');
    }
    if (candidate.sectionId !== null && candidate.sectionId !== undefined) {
      const section = this.sections.findById(candidate.sectionId);
      if (!section) fail('sectionId', 'Choose an existing section.');
      else if (department && section.departmentId !== department.id) {
        fail('sectionId', 'This section does not belong to the chosen department.');
      }
    }
    if (!position) fail('positionId', 'Choose an existing position.');
    else if (department && position.departmentId !== department.id) {
      fail('positionId', 'This position belongs to a different department.');
    }
    if (!this.grades.findById(candidate.jobGradeId)) {
      fail('jobGradeId', 'Choose an existing job grade.');
    }
    if (candidate.managerId !== null && candidate.managerId !== undefined) {
      if (!this.findById(candidate.managerId))
        fail('managerId', 'The chosen manager does not exist.');
    }
    if (candidate.shiftId !== null && candidate.shiftId !== undefined) {
      const shift = this.shifts().findById(candidate.shiftId);
      if (!shift) fail('shiftId', 'Choose an existing shift.');
      else if (shift.status !== 'Active' && stored?.shiftId !== candidate.shiftId) {
        fail('shiftId', 'This shift is inactive.');
      }
    }

    // Inactive organization units cannot receive an employee (existing assignments may stay).
    const assigned = (key: 'companyId' | 'branchId' | 'departmentId' | 'positionId') =>
      !stored || stored[key] !== candidate[key];
    if (company && assigned('companyId') && company.status !== 'Active') {
      fail('companyId', 'This company is inactive.');
    }
    if (branch && assigned('branchId') && branch.status !== 'Active') {
      fail('branchId', 'This branch is inactive.');
    }
    if (department && assigned('departmentId') && department.status !== 'Active') {
      fail('departmentId', 'This department is inactive.');
    }
    if (position && assigned('positionId') && position.status !== 'Active') {
      fail('positionId', 'This position is inactive.');
    }

    const messages = Object.values(errors);
    if (messages.length > 0) {
      this.validationError(messages[0], errors);
    }
  }
  protected override beforeDelete(entity: Employee): void {
    const reports = this.store().filter((e) => e.managerId === entity.id).length;
    if (reports > 0) {
      this.conflictError(
        `${fullName(entity)} manages ${reports} employee(s). Reassign their direct reports before deleting.`,
      );
    }
  }

  /** Next sequential employee code, e.g. EMP061. */
  nextEmployeeCode(): Observable<string> {
    return this.respond(() => {
      const max = this.store().reduce(
        (m, e) => Math.max(m, Number(e.employeeCode.replace(/\D/g, '')) || 0),
        0,
      );
      return `EMP${String(max + 1).padStart(3, '0')}`;
    });
  }

  // ---- Side effects: history entries and cascade delete -----------------------

  /** History entries are recorded here so every way of saving (form, import) leaves the same trail. */
  override createNow(model: CreateModel<Employee>, log = true): Employee {
    const created = super.createNow(model, log);
    this.history.record(
      created.id,
      'Joined',
      `Joined as ${this.positions.findById(created.positionId)?.title ?? 'employee'}`,
      `${created.employmentType} employee. Record created.`,
    );
    return created;
  }

  override updateNow(id: number, changes: UpdateModel<Employee>, log = true): Employee {
    const before = this.findById(id);
    const after = super.updateNow(id, changes, log);
    if (before) this.recordChanges(before, after);
    return after;
  }
  override delete(id: number): Observable<void> {
    return super.delete(id).pipe(
      tap(() => {
        this.documents.removeForEmployee(id);
        this.history.removeForEmployee(id);
      }),
    );
  }

  private recordChanges(before: Employee, after: Employee): void {
    const title = (positionId: number) =>
      this.positions.findById(positionId)?.title ?? `#${positionId}`;
    if (before.positionId !== after.positionId) {
      const promoted = after.jobGradeId > before.jobGradeId;
      this.history.record(
        after.id,
        promoted ? 'Promotion' : 'Transfer',
        `${promoted ? 'Promoted' : 'Moved'}: ${title(before.positionId)} → ${title(after.positionId)}`,
        'Position changed.',
      );
    } else if (before.departmentId !== after.departmentId || before.branchId !== after.branchId) {
      this.history.record(
        after.id,
        'Transfer',
        'Organization assignment changed',
        'Branch or department updated.',
      );
    }
    if (before.employmentStatus !== after.employmentStatus) {
      const exited =
        after.employmentStatus === 'Resigned' || after.employmentStatus === 'Terminated';
      this.history.record(
        after.id,
        exited ? 'Exit' : 'Status Change',
        `Status changed to ${after.employmentStatus}`,
        `Previously ${before.employmentStatus}.`,
      );
    }
    if (before.employmentType !== after.employmentType) {
      this.history.record(
        after.id,
        'Contract Change',
        `Employment type changed to ${after.employmentType}`,
        `Previously ${before.employmentType}.`,
      );
    }
  }
}
