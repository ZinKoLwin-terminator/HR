import { Injectable, Injector, inject } from '@angular/core';
import {
  SalaryAssignment,
  SalaryComponent,
  SalaryStructure,
} from '../../core/models/payroll.models';
import {
  SalaryAssignmentDataService,
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../core/services/data/payroll-data.services';
import {
  MOCK_SALARY_ASSIGNMENTS,
  MOCK_SALARY_COMPONENTS,
  MOCK_SALARY_STRUCTURES,
} from '../../mock-data/payroll/salary-setup.mock';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { JobGradeMockService } from '../organization/organization-mock.services';

@Injectable({ providedIn: 'root' })
export class SalaryComponentMockService
  extends InMemoryCrudService<SalaryComponent>
  implements SalaryComponentDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Salary component';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_SALARY_COMPONENTS, 'salary_components');
  }

  protected override describe(entity: SalaryComponent): string {
    return entity.name;
  }

  protected override validate(candidate: SalaryComponent): void {
    this.ensureUnique(candidate, 'code', 'Component code');
    if (!(candidate.value > 0)) {
      this.validationError('The amount or percentage must be greater than zero.', {
        value: 'Must be greater than 0.',
      });
    }
    if (candidate.calculation !== 'Fixed' && candidate.value > 100) {
      this.validationError('A percentage cannot exceed 100.', {
        value: 'Enter a percentage up to 100.',
      });
    }
  }

  protected override beforeDelete(entity: SalaryComponent): void {
    const structures = this.injector
      .get(SalaryStructureMockService)
      .records()
      .filter((s) => s.componentIds.includes(entity.id)).length;
    this.ensureNotReferenced(entity.name, [['salary structure(s)', structures]]);
  }
}

@Injectable({ providedIn: 'root' })
export class SalaryStructureMockService
  extends InMemoryCrudService<SalaryStructure>
  implements SalaryStructureDataService
{
  private readonly injector = inject(Injector);
  private readonly components = inject(SalaryComponentMockService);
  protected readonly entityLabel = 'Salary structure';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_SALARY_STRUCTURES, 'salary_structures');
  }

  protected override describe(entity: SalaryStructure): string {
    return entity.name;
  }

  protected override validate(candidate: SalaryStructure): void {
    this.ensureUnique(candidate, 'code', 'Structure code');
    if (!candidate.componentIds?.length) {
      this.validationError('Choose at least one component.', {
        componentIds: 'Choose at least one component.',
      });
    }
    const missing = candidate.componentIds.filter((id) => !this.components.findById(id));
    if (missing.length) {
      this.validationError('Some chosen components no longer exist.', {
        componentIds: 'Choose existing components.',
      });
    }
    if (!candidate.componentIds.some((id) => this.components.findById(id)?.type === 'Earning')) {
      this.validationError('A structure needs at least one earning (an allowance).', {
        componentIds: 'Include at least one earning.',
      });
    }
  }

  protected override beforeDelete(entity: SalaryStructure): void {
    const assigned = this.injector
      .get(SalaryAssignmentMockService)
      .records()
      .filter((a) => a.structureId === entity.id).length;
    this.ensureNotReferenced(entity.name, [['salary assignment(s)', assigned]]);
  }
}

@Injectable({ providedIn: 'root' })
export class SalaryAssignmentMockService
  extends InMemoryCrudService<SalaryAssignment>
  implements SalaryAssignmentDataService
{
  private readonly employees = inject(EmployeeMockService);
  private readonly grades = inject(JobGradeMockService);
  private readonly structures = inject(SalaryStructureMockService);
  protected readonly entityLabel = 'Salary assignment';
  protected readonly activityCategory = 'payroll';
  protected override readonly searchFields = ['remarks'] as const;

  constructor() {
    super(MOCK_SALARY_ASSIGNMENTS, 'salary_assignments');
  }

  protected override describe(entity: SalaryAssignment): string {
    const employee = this.employees.findById(entity.employeeId);
    return `${employee ? `${employee.firstName} ${employee.lastName}` : `employee #${entity.employeeId}`} from ${entity.effectiveFrom}`;
  }

  protected override validate(candidate: SalaryAssignment): void {
    const employee = this.employees.findById(candidate.employeeId);
    if (!employee) {
      this.validationError('The selected employee does not exist.', {
        employeeId: 'Choose an employee.',
      });
    }
    const structure = this.structures.findById(candidate.structureId);
    if (!structure || structure.status !== 'Active') {
      this.validationError('Choose an active salary structure.', {
        structureId: 'Choose an active structure.',
      });
    }
    const grade = this.grades.findById(employee.jobGradeId);
    if (
      grade &&
      (candidate.basicSalary < grade.minSalary || candidate.basicSalary > grade.maxSalary)
    ) {
      this.validationError(
        `Basic salary must be within the ${grade.code} band (${grade.minSalary} – ${grade.maxSalary}).`,
        {
          basicSalary: `Outside the ${grade.code} band (${grade.minSalary} – ${grade.maxSalary}).`,
        },
      );
    }
    if (candidate.effectiveFrom < employee.joiningDate) {
      this.validationError('The salary cannot start before the joining date.', {
        effectiveFrom: `Must be on or after ${employee.joiningDate}.`,
      });
    }
    const clash = this.store().some(
      (a) =>
        a.id !== candidate.id &&
        a.employeeId === candidate.employeeId &&
        a.effectiveFrom === candidate.effectiveFrom,
    );
    if (clash) {
      this.validationError('This employee already has a salary starting on that date.', {
        effectiveFrom: 'Another assignment starts on this date.',
      });
    }
  }

  protected override beforeDelete(entity: SalaryAssignment): void {
    const others = this.store().filter(
      (a) => a.employeeId === entity.employeeId && a.id !== entity.id,
    ).length;
    if (others === 0) {
      this.conflictError(
        'An employee needs at least one salary assignment. Add a new one instead.',
      );
    }
  }

  /** Assignment in force on a date (latest `effectiveFrom` on or before it). */
  currentFor(employeeId: number, asOf: string): SalaryAssignment | undefined {
    return this.store()
      .filter((a) => a.employeeId === employeeId && a.effectiveFrom <= asOf)
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0];
  }
}
