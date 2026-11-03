import { Injectable, Injector, Signal, Type, inject } from '@angular/core';
import { Entity } from '../../core/models/common.models';
import {
  Branch,
  Company,
  Department,
  JobGrade,
  Position,
  Section,
} from '../../core/models/organization.models';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../../core/services/data/organization-data.services';
import { MOCK_BRANCHES } from '../../mock-data/organization/branches.mock';
import { MOCK_COMPANIES } from '../../mock-data/organization/companies.mock';
import { MOCK_DEPARTMENTS } from '../../mock-data/organization/departments.mock';
import { MOCK_JOB_GRADES } from '../../mock-data/organization/job-grades.mock';
import { MOCK_POSITIONS } from '../../mock-data/organization/positions.mock';
import { MOCK_SECTIONS } from '../../mock-data/organization/sections.mock';
// Employees reference organization data and vice versa (delete protection). The
// import cycle is safe: classes are only touched inside methods, never at load time.
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';

/** Anything exposing a live record list (every in-memory mock service does). */
interface RecordStore<T extends Entity> {
  records: Signal<T[]>;
}

/** Counts records in another mock store. Resolved lazily to avoid circular injection. */
function countWhere<T extends Entity>(
  injector: Injector,
  token: Type<RecordStore<T>>,
  predicate: (item: T) => boolean,
): number {
  return injector.get(token).records().filter(predicate).length;
}

@Injectable({ providedIn: 'root' })
export class CompanyMockService extends InMemoryCrudService<Company> implements CompanyDataService {
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Company';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'name', 'legalName', 'city'] as const;

  constructor() {
    super(MOCK_COMPANIES);
  }

  protected override describe(entity: Company): string {
    return entity.name;
  }

  protected override validate(candidate: Company): void {
    this.ensureUnique(candidate, 'code', 'Company code');
    this.ensureUnique(candidate, 'name', 'Company name');
  }

  protected override beforeDelete(entity: Company): void {
    const i = this.injector;
    this.ensureNotReferenced(entity.name, [
      ['branch(es)', countWhere(i, BranchMockService, (b) => b.companyId === entity.id)],
      ['department(s)', countWhere(i, DepartmentMockService, (d) => d.companyId === entity.id)],
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.companyId === entity.id)],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class BranchMockService extends InMemoryCrudService<Branch> implements BranchDataService {
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Branch';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'name', 'city'] as const;

  constructor() {
    super(MOCK_BRANCHES);
  }

  protected override describe(entity: Branch): string {
    return entity.name;
  }

  protected override validate(candidate: Branch): void {
    this.ensureUnique(candidate, 'code', 'Branch code');
  }

  protected override beforeDelete(entity: Branch): void {
    const i = this.injector;
    this.ensureNotReferenced(entity.name, [
      ['department(s)', countWhere(i, DepartmentMockService, (d) => d.branchId === entity.id)],
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.branchId === entity.id)],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class DepartmentMockService
  extends InMemoryCrudService<Department>
  implements DepartmentDataService
{
  private readonly injector = inject(Injector);
  private readonly branches = inject(BranchMockService);
  protected readonly entityLabel = 'Department';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'name', 'costCenter'] as const;

  constructor() {
    super(MOCK_DEPARTMENTS);
  }

  protected override describe(entity: Department): string {
    return entity.name;
  }

  protected override validate(candidate: Department): void {
    this.ensureUnique(candidate, 'code', 'Department code');
    const branch = this.branches.findById(candidate.branchId);
    if (branch && branch.companyId !== candidate.companyId) {
      this.validationError('The selected branch does not belong to the selected company.', {
        branchId: 'Choose a branch of the selected company.',
      });
    }
  }

  protected override beforeDelete(entity: Department): void {
    const i = this.injector;
    this.ensureNotReferenced(entity.name, [
      ['section(s)', countWhere(i, SectionMockService, (s) => s.departmentId === entity.id)],
      ['position(s)', countWhere(i, PositionMockService, (p) => p.departmentId === entity.id)],
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.departmentId === entity.id)],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class SectionMockService extends InMemoryCrudService<Section> implements SectionDataService {
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Section';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_SECTIONS);
  }

  protected override describe(entity: Section): string {
    return entity.name;
  }

  protected override validate(candidate: Section): void {
    this.ensureUnique(candidate, 'code', 'Section code');
  }

  protected override beforeDelete(entity: Section): void {
    const i = this.injector;
    this.ensureNotReferenced(entity.name, [
      ['position(s)', countWhere(i, PositionMockService, (p) => p.sectionId === entity.id)],
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.sectionId === entity.id)],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class JobGradeMockService
  extends InMemoryCrudService<JobGrade>
  implements JobGradeDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Job grade';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_JOB_GRADES);
  }

  protected override describe(entity: JobGrade): string {
    return `${entity.code} ${entity.name}`;
  }

  protected override validate(candidate: JobGrade): void {
    this.ensureUnique(candidate, 'code', 'Grade code');
    if (candidate.maxSalary < candidate.minSalary) {
      this.validationError('Maximum salary must be greater than or equal to minimum salary.', {
        maxSalary: 'Must be ≥ minimum salary.',
      });
    }
  }

  protected override beforeDelete(entity: JobGrade): void {
    const i = this.injector;
    this.ensureNotReferenced(`${entity.code} ${entity.name}`, [
      ['position(s)', countWhere(i, PositionMockService, (p) => p.jobGradeId === entity.id)],
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.jobGradeId === entity.id)],
    ]);
  }
}

@Injectable({ providedIn: 'root' })
export class PositionMockService
  extends InMemoryCrudService<Position>
  implements PositionDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Position';
  protected readonly activityCategory = 'organization';
  protected override readonly searchFields = ['code', 'title'] as const;

  constructor() {
    super(MOCK_POSITIONS);
  }

  protected override describe(entity: Position): string {
    return entity.title;
  }

  protected override validate(candidate: Position): void {
    this.ensureUnique(candidate, 'code', 'Position code');
    if (candidate.reportsToPositionId === candidate.id) {
      this.validationError('A position cannot report to itself.', {
        reportsToPositionId: 'Choose a different position.',
      });
    }
    if (candidate.sectionId !== null) {
      const section = this.injector.get(SectionMockService).findById(candidate.sectionId);
      if (section && section.departmentId !== candidate.departmentId) {
        this.validationError('The selected section does not belong to the selected department.', {
          sectionId: 'Choose a section of the selected department.',
        });
      }
    }
  }

  protected override beforeDelete(entity: Position): void {
    const i = this.injector;
    this.ensureNotReferenced(entity.title, [
      ['employee(s)', countWhere(i, EmployeeMockService, (e) => e.positionId === entity.id)],
      [
        'position(s) reporting to it',
        countWhere(i, PositionMockService, (p) => p.reportsToPositionId === entity.id),
      ],
    ]);
  }
}
