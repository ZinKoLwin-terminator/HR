import { Provider, Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CheckInComponent } from '../features/attendance/check-in/check-in.component';
import { LeaveApplicationsPageComponent } from '../features/leave-management/leave-applications/leave-pages.component';
import { LeaveBalancesComponent } from '../features/leave-management/leave-balances/leave-balances.component';
import { DepartmentsPageComponent } from '../features/organization/departments/departments-page.component';
import { UNNAMED_PERSON } from '../features/organization/organization-lookups';
import { PositionsPageComponent } from '../features/organization/positions/positions-page.component';
import { ReportingStructureComponent } from '../features/organization/reporting-structure/reporting-structure.component';
import { PayslipsListComponent } from '../features/payroll/payslips/payslips-list.component';
import { KpiReportsComponent } from '../features/performance-management/reports/kpi-reports.component';
import { JobOpeningsPageComponent } from '../features/recruitment/job-openings/job-openings-page.component';
import { MOCK_EMPLOYEES } from '../mock-data/employees/employees.mock';
import { provideMockDataServices } from '../mock-services/provide-mock-data-services';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from './configuration/app-config';
import { AppError } from './models/common.models';
import { fullName } from './models/employee.models';
import { CurrentUser, CurrentUserService } from './services/current-user.service';
import {
  AttendanceRecordDataService,
  HolidayDataService,
  ShiftDataService,
} from './services/data/attendance-data.services';
import {
  EmployeeDataService,
  EmployeeLookupDataService,
} from './services/data/employee-data.service';
import { ExcelModuleCatalogDataService } from './services/data/excel-data.services';
import { KpiCategoryDataService } from './services/data/kpi-data.services';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from './services/data/leave-data.services';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from './services/data/organization-data.services';
import { PayrollRunDataService, PayslipDataService } from './services/data/payroll-data.services';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from './services/data/recruitment-data.services';

/**
 * Role access sweep: each starter role opens screens it can see, while every data contract it has
 * no permission for answers 403 (as the API does). Nothing may throw (a `ResourceValueError` in a
 * template freezes the screen), and each screen shows data or its error state.
 */

const FORBIDDEN: AppError = {
  code: 'Forbidden',
  message: 'You do not have permission to do that.',
};

/** Starter role grants (the database seed); what matters in API mode. */
const ROLES = {
  employee: [
    'dashboard:View',
    'attendance:View',
    'attendance:Create',
    'leave:View',
    'leave:Create',
    'kpi:View',
    'kpi:Create',
    'grievance:View',
    'grievance:Create',
  ],
  payrollOfficer: [
    'dashboard:View',
    'employees:View',
    'payroll:View',
    'payroll:Create',
    'payroll:Edit',
    'payroll:Approve',
    'kpi:View',
    'kpi:Create',
    'grievance:View',
    'grievance:Create',
  ],
  recruiter: [
    'dashboard:View',
    'recruitment:View',
    'recruitment:Create',
    'recruitment:Edit',
    'recruitment:Delete',
    'kpi:View',
    'kpi:Create',
    'grievance:View',
    'grievance:Create',
  ],
  departmentManager: [
    'dashboard:View',
    'employees:View',
    'organization:View',
    'recruitment:View',
    'attendance:View',
    'attendance:Approve',
    'leave:View',
    'leave:Approve',
    'kpi:View',
    'kpi:Create',
    'kpi:Approve',
    'grievance:View',
    'grievance:Create',
  ],
  hrOfficer: [
    'dashboard:View',
    ...['View', 'Create', 'Edit'].map((a) => `organization:${a}`),
    ...['View', 'Create', 'Edit'].map((a) => `employees:${a}`),
    ...['View', 'Create', 'Edit', 'Approve'].map((a) => `attendance:${a}`),
    ...['View', 'Create', 'Edit', 'Approve'].map((a) => `leave:${a}`),
    'payroll:View',
    'payroll:Approve',
    ...['View', 'Create', 'Edit'].map((a) => `recruitment:${a}`),
    ...['View', 'Create', 'Edit', 'Delete'].map((a) => `kpi-setup:${a}`),
    'kpi:View',
    'kpi:Create',
    'kpi:Approve',
    'grievance:View',
    'grievance:Create',
    'grievance-investigation:View',
    'grievance-investigation:Edit',
  ],
} as const;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Token = abstract new (...args: any[]) => unknown;

/** The permission each read contract needs on the API (any one key of a list is enough). */
const READ_RULES: [Token, string[]][] = [
  [CompanyDataService, ['organization:View']],
  [BranchDataService, ['organization:View']],
  [DepartmentDataService, ['organization:View']],
  [SectionDataService, ['organization:View']],
  [JobGradeDataService, ['organization:View']],
  [PositionDataService, ['organization:View']],
  [EmployeeDataService, ['employees:View']],
  [EmployeeLookupDataService, ['recruitment:Edit', 'employees:View']],
  [ExcelModuleCatalogDataService, ['excel:View']],
  [ShiftDataService, ['attendance:View']],
  [HolidayDataService, ['attendance:View']],
  [AttendanceRecordDataService, ['attendance:View']],
  [LeaveTypeDataService, ['leave:View']],
  [LeavePolicyDataService, ['leave:View']],
  [LeaveApplicationDataService, ['leave:View']],
  [LeaveBalanceDataService, ['leave:View']],
  [LeaveCalendarDataService, ['leave:View']],
  [PayrollRunDataService, ['payroll:View']],
  [PayslipDataService, ['payroll:View']],
  [JobOpeningDataService, ['recruitment:View']],
  [CandidateDataService, ['recruitment:View']],
  [ApplicationDataService, ['recruitment:View']],
  [InterviewDataService, ['recruitment:View']],
];

/** A data contract whose every method answers 403, and counts the calls. */
function forbiddenService(calls: Map<Token, number>, token: Token): unknown {
  return new Proxy(
    {},
    {
      get: () => () => {
        calls.set(token, (calls.get(token) ?? 0) + 1);
        return throwError(() => FORBIDDEN);
      },
    },
  );
}

const SELF = MOCK_EMPLOYEES[1];

interface Sweep {
  fixture: ComponentFixture<unknown>;
  /** Calls made to contracts the role may not read (each answered 403). */
  forbiddenCalls: Map<Token, number>;
  errors: unknown[];
}

async function settle(fixture: ComponentFixture<unknown>) {
  for (let i = 0; i < 5; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
}

/**
 * Renders `component` for a role. Contracts the role cannot read answer 403; `alsoForbid` makes
 * readable ones fail too (to check the error states).
 */
async function render(
  component: Type<unknown>,
  keys: readonly string[],
  alsoForbid: Token[] = [],
  extra: Provider[] = [],
): Promise<Sweep> {
  const granted = new Set(keys);
  const forbiddenCalls = new Map<Token, number>();
  const overrides: Provider[] = READ_RULES.filter(
    ([token, needs]) => alsoForbid.includes(token) || !needs.some((k) => granted.has(k)),
  ).map(([token]) => ({ provide: token, useValue: forbiddenService(forbiddenCalls, token) }));
  const user: CurrentUser = {
    id: 99,
    employeeId: SELF.id,
    displayName: fullName(SELF),
    email: 'role.sweep@example.test',
    jobTitle: '',
    roles: ['Sweep'],
    permissions: [...keys],
  };
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user), initials: signal('RS') } },
      ...overrides,
      ...extra,
    ],
  });
  const errors: unknown[] = [];
  const original = console.error;
  console.error = (...args: unknown[]) => errors.push(args);
  try {
    const fixture = TestBed.createComponent(component);
    await settle(fixture);
    return { fixture, forbiddenCalls, errors };
  } finally {
    console.error = original;
  }
}

const text = (sweep: Sweep) => (sweep.fixture.nativeElement as HTMLElement).textContent ?? '';
const noValueErrors = (sweep: Sweep) =>
  expect(sweep.errors.map(String).join('\n')).not.toMatch(/ResourceValueError|resource.*value/i);
const instance = <T>(sweep: Sweep) => sweep.fixture.componentInstance as T;

describe('role access sweep (403 for every contract the role cannot read)', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('Employee: Check In renders and offers only the user themselves', async () => {
    const sweep = await render(CheckInComponent, ROLES.employee);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('Check in / check out');
    expect(sweep.forbiddenCalls.get(EmployeeDataService) ?? 0).toBe(0);
    const options = instance<{ employeeOptions(): { value: number; label: string }[] }>(
      sweep,
    ).employeeOptions();
    expect(options).toEqual([{ value: SELF.id, label: fullName(SELF) }]);
  });

  it('Employee: Leave Applications renders the list without reading employees', async () => {
    const sweep = await render(LeaveApplicationsPageComponent, ROLES.employee);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('Leave applications');
    expect(sweep.fixture.nativeElement.querySelector('app-data-table')).not.toBeNull();
    expect(sweep.forbiddenCalls.get(EmployeeDataService) ?? 0).toBe(0);
    expect(sweep.forbiddenCalls.get(EmployeeLookupDataService) ?? 0).toBe(0);
  });

  it('Employee: Leave Applications shows the error state when leave cannot be read', async () => {
    const sweep = await render(LeaveApplicationsPageComponent, ROLES.employee, [
      LeaveApplicationDataService,
      LeaveTypeDataService,
      HolidayDataService,
    ]);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('could not be loaded');
    expect(text(sweep)).toContain(FORBIDDEN.message);
  });

  it('Payroll Officer: Payslips list renders rows (no organization:View, no excel)', async () => {
    const sweep = await render(PayslipsListComponent, ROLES.payrollOfficer);
    noValueErrors(sweep);
    expect(sweep.fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(
      0,
    );
    expect(sweep.forbiddenCalls.get(ExcelModuleCatalogDataService) ?? 0).toBe(0);
    expect(sweep.forbiddenCalls.get(DepartmentDataService) ?? 0).toBe(0);
  });

  it('Payroll Officer: Payslips list still queries when the periods fail (no hang)', async () => {
    const sweep = await render(PayslipsListComponent, ROLES.payrollOfficer, [
      PayrollRunDataService,
    ]);
    noValueErrors(sweep);
    expect(instance<{ defaulted(): boolean }>(sweep).defaulted()).toBe(true);
    expect(sweep.fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(
      0,
    );
  });

  it('Payroll Officer: Payslips list shows its error state when payslips fail', async () => {
    const sweep = await render(PayslipsListComponent, ROLES.payrollOfficer, [PayslipDataService]);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('Payslips could not be loaded');
    expect(text(sweep)).toContain(FORBIDDEN.message);
  });

  it('Recruiter: Job Openings fills Department from the organization lookup', async () => {
    const sweep = await render(JobOpeningsPageComponent, ROLES.recruiter);
    noValueErrors(sweep);
    expect(sweep.forbiddenCalls.get(DepartmentDataService) ?? 0).toBe(0);
    const config = instance<{
      config(): {
        filters: { key: string; options: unknown[] }[];
        fields: { key: string; required?: boolean; options?: unknown }[];
      } | null;
    }>(sweep).config();
    expect(config).not.toBeNull();
    const filter = config!.filters.find((f) => f.key === 'departmentId');
    expect(filter?.options.length).toBeGreaterThan(0);
    const field = config!.fields.find((f) => f.key === 'departmentId');
    expect(field?.required).toBe(true);
    const fieldOptions =
      typeof field?.options === 'function'
        ? (field.options as (v: object, c: object) => unknown[])({}, { id: null })
        : (field?.options as unknown[]);
    expect(fieldOptions.length).toBeGreaterThan(0);
    expect(sweep.fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(
      0,
    );
  });

  it('Department Manager: Departments renders without Excel and without asking for it', async () => {
    const sweep = await render(DepartmentsPageComponent, ROLES.departmentManager);
    noValueErrors(sweep);
    expect(sweep.fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(
      0,
    );
    expect(sweep.forbiddenCalls.get(ExcelModuleCatalogDataService) ?? 0).toBe(0);
    expect(text(sweep)).not.toContain('Export');
  });

  it('Department Manager: Departments shows the error state when the list fails', async () => {
    const sweep = await render(DepartmentsPageComponent, ROLES.departmentManager, [
      DepartmentDataService,
    ]);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('Departments could not be loaded');
    expect(text(sweep)).toContain(FORBIDDEN.message);
  });

  it('Department Manager: KPI Reports fills the category filter from the picker, not the setup list', async () => {
    // Categories: the CRUD reads need kpi-setup:View (403 here); the picker needs kpi:View only.
    const calls = { crud: 0, options: 0 };
    const categories = {
      getAll: () => (calls.crud++, throwError(() => FORBIDDEN)),
      query: () => (calls.crud++, throwError(() => FORBIDDEN)),
      getById: () => (calls.crud++, throwError(() => FORBIDDEN)),
      options: () => (
        calls.options++,
        of([{ id: 7, code: 'QUALITY', name: 'Quality', status: 'Active' }])
      ),
    };
    const sweep = await render(
      KpiReportsComponent,
      ROLES.departmentManager,
      [],
      [{ provide: KpiCategoryDataService, useValue: categories }],
    );
    noValueErrors(sweep);
    expect(calls.crud).toBe(0);
    expect(calls.options).toBeGreaterThan(0);
    const filters = instance<{ filterOptions(): { categoryId: { label: string }[] } }>(sweep);
    expect(filters.filterOptions().categoryId.map((o) => o.label)).toEqual(['Quality']);
  });

  it('HR Officer: Leave Balances renders without Excel', async () => {
    const sweep = await render(LeaveBalancesComponent, ROLES.hrOfficer);
    noValueErrors(sweep);
    expect(sweep.forbiddenCalls.get(ExcelModuleCatalogDataService) ?? 0).toBe(0);
    expect(sweep.fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(
      0,
    );
  });

  it('HR Officer: Leave Balances shows the error state when balances fail', async () => {
    const sweep = await render(LeaveBalancesComponent, ROLES.hrOfficer, [LeaveBalanceDataService]);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('could not be loaded');
  });
});

describe('custom role with organization:View but no employees:View', () => {
  const ORG_ONLY = ['dashboard:View', 'organization:View', 'organization:Edit'];
  afterEach(() => TestBed.resetTestingModule());

  const headers = (sweep: Sweep) =>
    [...(sweep.fixture.nativeElement as HTMLElement).querySelectorAll('th')].map((th) =>
      th.textContent?.trim(),
    );

  it('Positions: hides the Filled column and reads no employees', async () => {
    const sweep = await render(PositionsPageComponent, ORG_ONLY);
    noValueErrors(sweep);
    expect(headers(sweep)).not.toContain('Filled');
    expect(sweep.forbiddenCalls.get(EmployeeDataService) ?? 0).toBe(0);
  });

  it('Reporting structure: explains that employee access is needed', async () => {
    const sweep = await render(ReportingStructureComponent, ORG_ONLY);
    noValueErrors(sweep);
    expect(text(sweep)).toContain('Employee access is needed');
    expect(sweep.forbiddenCalls.get(EmployeeDataService) ?? 0).toBe(0);
  });

  it('Departments: no Head column; the head field is read-only and never shows #id', async () => {
    const sweep = await render(DepartmentsPageComponent, ORG_ONLY);
    noValueErrors(sweep);
    expect(headers(sweep)).not.toContain('Head');
    const config = instance<{
      config(): {
        fields: {
          key: string;
          options?: (v: Record<string, unknown>, c: object) => { label: string }[];
          force?: (v: Record<string, unknown>) => { value: unknown } | null;
        }[];
      } | null;
    }>(sweep).config()!;
    const head = config.fields.find((f) => f.key === 'headEmployeeId')!;
    expect(head.force?.({ headEmployeeId: 5 })).toEqual({ value: 5 });
    const labels = head.options!({ headEmployeeId: 5 }, { id: 1 }).map((o) => o.label);
    expect(labels).toEqual([UNNAMED_PERSON]);
    expect(labels.join()).not.toContain('#');
  });
});
