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
import { isDateInRange } from '../../shared/utilities/date.util';
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
  DepartmentMockService,
  PositionMockService,
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

  protected override validate(candidate: Employee): void {
    this.ensureUnique(candidate, 'employeeCode', 'Employee code');
    this.ensureUnique(candidate, 'workEmail', 'Work email');
    if (candidate.managerId === candidate.id) {
      this.validationError('An employee cannot be their own manager.', {
        managerId: 'Choose a different manager.',
      });
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

  override create(model: CreateModel<Employee>): Observable<Employee> {
    return super
      .create(model)
      .pipe(
        tap((created) =>
          this.history.record(
            created.id,
            'Joined',
            `Joined as ${this.positions.findById(created.positionId)?.title ?? 'employee'}`,
            `${created.employmentType} employee. Record created.`,
          ),
        ),
      );
  }

  override update(id: number, changes: UpdateModel<Employee>): Observable<Employee> {
    const before = this.findById(id);
    return super
      .update(id, changes)
      .pipe(tap((after) => before && this.recordChanges(before, after)));
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
