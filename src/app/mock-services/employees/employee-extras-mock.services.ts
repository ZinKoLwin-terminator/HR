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
  calculatePay,
  describeComponent,
  periodWorkingDays,
  round2,
} from '../../core/utilities/payroll-calc';
import { endOfMonth, startOfMonth, todayIso } from '../../shared/utilities/date.util';
import { HolidayMockService } from '../attendance/attendance-mock.services';
import {
  SalaryAssignmentMockService,
  SalaryComponentMockService,
  SalaryStructureMockService,
} from '../payroll/payroll-setup-mock.services';
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

/** Sample salary overview for the employee's current salary assignment (not real payroll). */
@Injectable({ providedIn: 'root' })
export class EmployeeSalaryMockService implements EmployeeSalaryDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly weekendDays = inject(APP_CONFIG).weekendDays;
  private readonly injector = inject(Injector);

  getSummary(employeeId: number): Observable<EmployeeSalarySummary> {
    return mockResponse(() => {
      const employee = this.injector.get(EmployeeMockService).findById(employeeId);
      if (!employee) {
        throw mockError('NotFound', `Employee #${employeeId} was not found.`);
      }
      const today = todayIso();
      const assignment = this.injector
        .get(SalaryAssignmentMockService)
        .currentFor(employeeId, today);
      const structure = assignment
        ? this.injector.get(SalaryStructureMockService).findById(assignment.structureId)
        : undefined;
      if (!assignment || !structure) {
        throw mockError(
          'NotFound',
          `${employee.firstName} ${employee.lastName} has no salary assignment yet.`,
        );
      }
      const components = structure.componentIds
        .map((id) => this.injector.get(SalaryComponentMockService).findById(id))
        .filter((c): c is NonNullable<typeof c> => !!c);
      const holidays = new Set(
        this.injector
          .get(HolidayMockService)
          .records()
          .map((h) => h.date),
      );
      const periodStart = startOfMonth(today);
      const periodEnd = endOfMonth(today);
      // A full month at the current salary: no proration, overtime or unpaid leave.
      const pay = calculatePay({
        basicSalary: assignment.basicSalary,
        components,
        periodStart,
        periodEnd,
        joiningDate: '1900-01-01',
        exitDate: null,
        workingDays: periodWorkingDays(periodStart, periodEnd, holidays, this.weekendDays),
        unpaidLeaveDays: 0,
        overtimeMinutes: 0,
      })!;
      const grade = this.injector.get(JobGradeMockService).findById(employee.jobGradeId);
      const min = grade?.minSalary ?? assignment.basicSalary;
      const max = grade?.maxSalary ?? assignment.basicSalary;
      const active = components.filter((c) => c.status === 'Active');
      return {
        employeeId,
        basic: pay.basic,
        allowances: round2(pay.gross - pay.basic),
        gross: pay.gross,
        deductions: pay.totalDeductions,
        net: pay.net,
        gradeCode: grade?.code ?? '—',
        gradeName: grade?.name ?? '—',
        bandMin: min,
        bandMax: max,
        bandPosition:
          max > min ? Math.round(((assignment.basicSalary - min) / (max - min)) * 100) : 0,
        assumptions: [
          `Basic salary comes from the current assignment: ${structure.name}, effective ${assignment.effectiveFrom}.`,
          ...active.map((c) => `${c.name} (${c.type.toLowerCase()}): ${describeComponent(c)}.`),
          `These are demonstration rules, not real tax or statutory calculations.`,
        ],
      };
    }, this.latency);
  }
}
