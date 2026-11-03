import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EmployeeDocument,
  EmployeeSalarySummary,
  EmploymentHistoryEntry,
  HistoryEventType,
} from '../../core/models/employee-extras.models';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { CurrentUserService } from '../../core/services/current-user.service';
import {
  EmployeeDocumentDataService,
  EmployeeSalaryDataService,
  EmploymentHistoryDataService,
} from '../../core/services/data/employee-extras-data.services';
import { MOCK_EMPLOYEE_DOCUMENTS } from '../../mock-data/employees/documents.mock';
import { MOCK_EMPLOYMENT_HISTORY } from '../../mock-data/employees/history.mock';
import {
  SAMPLE_PAYROLL_RULES,
  sampleBasicSalary,
  samplePayBreakdown,
} from '../../mock-data/payroll/payroll-runs.mock';
import { todayIso } from '../../shared/utilities/date.util';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { mockError, mockResponse } from '../mock-response';
import { JobGradeMockService } from '../organization/organization-mock.services';
import { EmployeeMockService } from './employee-mock.service';

@Injectable({ providedIn: 'root' })
export class EmployeeDocumentMockService
  extends InMemoryCrudService<EmployeeDocument>
  implements EmployeeDocumentDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Document';
  protected readonly activityCategory = 'employee';
  protected override readonly searchFields = ['title', 'fileName', 'type'] as const;

  constructor() {
    super(MOCK_EMPLOYEE_DOCUMENTS);
  }

  protected override describe(entity: EmployeeDocument): string {
    return entity.title;
  }

  protected override validate(candidate: EmployeeDocument): void {
    // Resolved lazily: the employee service depends on this one (cascade delete).
    if (!this.injector.get(EmployeeMockService).findById(candidate.employeeId)) {
      this.validationError('The selected employee does not exist.', {
        employeeId: 'Choose an employee.',
      });
    }
  }

  /** Used by the employee service when an employee is deleted. */
  removeForEmployee(employeeId: number): void {
    this.store.update((items) => items.filter((d) => d.employeeId !== employeeId));
  }
}

@Injectable({ providedIn: 'root' })
export class EmploymentHistoryMockService
  extends InMemoryCrudService<EmploymentHistoryEntry>
  implements EmploymentHistoryDataService
{
  private readonly currentUser = inject(CurrentUserService);
  protected readonly entityLabel = 'History entry';
  protected readonly activityCategory = 'employee';

  constructor() {
    super(MOCK_EMPLOYMENT_HISTORY);
  }

  /** Appends an entry (called by the employee service when key fields change). */
  record(employeeId: number, type: HistoryEventType, title: string, details: string): void {
    this.store.update((items) => [
      ...items,
      {
        id: this.nextId(),
        employeeId,
        date: todayIso(),
        type,
        title,
        details,
        recordedBy: this.currentUser.current().displayName,
      },
    ]);
  }

  removeForEmployee(employeeId: number): void {
    this.store.update((items) => items.filter((h) => h.employeeId !== employeeId));
  }
}

/** Sample salary overview derived from configurable sample rules (not real payroll). */
@Injectable({ providedIn: 'root' })
export class EmployeeSalaryMockService implements EmployeeSalaryDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly injector = inject(Injector);

  getSummary(employeeId: number): Observable<EmployeeSalarySummary> {
    return mockResponse(() => {
      const employee = this.injector.get(EmployeeMockService).findById(employeeId);
      if (!employee) {
        throw mockError('NotFound', `Employee #${employeeId} was not found.`);
      }
      const grade = this.injector.get(JobGradeMockService).findById(employee.jobGradeId);
      const basic = sampleBasicSalary(employee);
      const pay = samplePayBreakdown(basic);
      const min = grade?.minSalary ?? basic;
      const max = grade?.maxSalary ?? basic;
      return {
        employeeId,
        ...pay,
        gradeCode: grade?.code ?? '—',
        gradeName: grade?.name ?? '—',
        bandMin: min,
        bandMax: max,
        bandPosition: max > min ? Math.round(((basic - min) / (max - min)) * 100) : 0,
        assumptions: [
          `Basic salary is a sample value inside the grade band.`,
          `Housing allowance: ${SAMPLE_PAYROLL_RULES.housingAllowanceRate * 100}% of basic, plus a fixed transport allowance.`,
          `Deductions: sample income tax ${SAMPLE_PAYROLL_RULES.sampleIncomeTaxRate * 100}% and social contribution ${SAMPLE_PAYROLL_RULES.sampleSocialSecurityRate * 100}% of gross.`,
          `These are demonstration rules, not real tax or statutory calculations.`,
        ],
      };
    }, this.latency);
  }
}
