import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { AppError, PagedResult } from '../../core/models/common.models';
import { Employee, EmployeeLookupItem } from '../../core/models/employee.models';
import { OrganizationLookup } from '../../core/models/organization.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import {
  EmployeeDataService,
  EmployeeLookupDataService,
} from '../../core/services/data/employee-data.service';
import { OrganizationLookupDataService } from '../../core/services/data/organization-data.services';
import { PermissionService } from '../../core/services/permission.service';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { OrganizationLookupService, employeeOptions } from './organization-lookups';

const FORBIDDEN: AppError = {
  code: 'Forbidden',
  message: 'You do not have permission to do that.',
};

const LOOKUP: OrganizationLookup = {
  companies: [{ id: 1, code: 'APX', name: 'Apex Demo Co', status: 'Active' }],
  branches: [{ id: 2, companyId: 1, code: 'HQ', name: 'Head office', status: 'Active' }],
  departments: [
    { id: 3, companyId: 1, branchId: 2, code: 'ENG', name: 'Engineering', status: 'Active' },
  ],
  sections: [],
  positions: [],
  jobGrades: [{ id: 6, code: 'G3', name: 'Officer', level: 3, status: 'Active' }],
};

const PICKER: EmployeeLookupItem[] = [
  {
    id: 41,
    employeeCode: 'EMP041',
    displayName: 'Fictional Picker',
    jobTitle: 'Analyst',
    departmentName: 'Engineering',
  },
];

interface Calls {
  lookup: number;
  getAll: number;
  picker: number;
}

function setup(options: {
  keys: string[] | null;
  employeeId?: number | null;
  lookup?: () => Observable<OrganizationLookup>;
  getAll?: () => Observable<Employee[]>;
  picker?: () => Observable<PagedResult<EmployeeLookupItem>>;
}) {
  const calls: Calls = { lookup: 0, getAll: 0, picker: 0 };
  const granted = options.keys === null ? null : new Set(options.keys);
  const user: CurrentUser = {
    id: 9,
    employeeId: options.employeeId === undefined ? 12 : options.employeeId,
    displayName: 'Fictional Self',
    email: 'self@example.test',
    jobTitle: '',
    roles: ['Tester'],
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: CurrentUserService, useValue: { current: signal(user) } },
      {
        provide: PermissionService,
        useValue: { can: (key: string) => granted === null || granted.has(key) },
      },
      {
        provide: OrganizationLookupDataService,
        useValue: {
          getLookup: () => (calls.lookup++, (options.lookup ?? (() => of(LOOKUP)))()),
        },
      },
      {
        provide: EmployeeDataService,
        useValue: {
          getAll: () => (calls.getAll++, (options.getAll ?? (() => of(MOCK_EMPLOYEES)))()),
        },
      },
      {
        provide: EmployeeLookupDataService,
        useValue: {
          query: (r: { pageIndex: number; pageSize: number }) => (
            calls.picker++,
            (
              options.picker ??
              (() => of({ items: PICKER, total: 1, pageIndex: r.pageIndex, pageSize: r.pageSize }))
            )()
          ),
        },
      },
    ],
  });
  return { service: TestBed.inject(OrganizationLookupService), calls };
}

describe('OrganizationLookupService.load()', () => {
  it('reads the organization lookup, never the organization:View lists', async () => {
    const { service, calls } = setup({ keys: [] });
    const result = await firstValueFrom(service.load());
    expect(calls.lookup).toBe(1);
    expect(result.companies).toEqual(LOOKUP.companies);
    expect(result.departments).toEqual(LOOKUP.departments);
    expect(result.grades).toEqual(LOOKUP.jobGrades);
  });

  it('with employees:View reads every employee, with full records', async () => {
    const { service, calls } = setup({ keys: ['employees:View', 'recruitment:Edit'] });
    const result = await firstValueFrom(service.load());
    expect(calls.getAll).toBe(1);
    expect(calls.picker).toBe(0);
    expect(result.employees).toHaveLength(MOCK_EMPLOYEES.length);
    expect(result.employees[0]).toEqual({
      id: MOCK_EMPLOYEES[0].id,
      employeeCode: MOCK_EMPLOYEES[0].employeeCode,
      displayName: `${MOCK_EMPLOYEES[0].firstName} ${MOCK_EMPLOYEES[0].lastName}`,
    });
    expect(result.employeeRecords).toHaveLength(MOCK_EMPLOYEES.length);
  });

  it('allows everything in mock mode (no permission list)', async () => {
    const { service, calls } = setup({ keys: null });
    await firstValueFrom(service.load());
    expect(calls.getAll).toBe(1);
  });

  it('with recruitment:Edit but no employees:View uses the employee picker', async () => {
    const { service, calls } = setup({ keys: ['recruitment:Edit'] });
    const result = await firstValueFrom(service.load());
    expect(calls.getAll).toBe(0);
    expect(calls.picker).toBe(1);
    expect(result.employees).toEqual([
      { id: 41, employeeCode: 'EMP041', displayName: 'Fictional Picker' },
    ]);
    expect(result.employeeRecords).toEqual([]);
  });

  it('with neither returns only the signed-in user own employee, without asking', async () => {
    const { service, calls } = setup({ keys: ['leave:View', 'attendance:View'] });
    const result = await firstValueFrom(service.load());
    expect(calls.getAll + calls.picker).toBe(0);
    expect(result.employees).toEqual([{ id: 12, employeeCode: '', displayName: 'Fictional Self' }]);
    expect(employeeOptions(result.employees)).toEqual([{ value: 12, label: 'Fictional Self' }]);
  });

  it('returns no employees for an account not linked to an employee', async () => {
    const { service } = setup({ keys: [], employeeId: null });
    expect((await firstValueFrom(service.load())).employees).toEqual([]);
  });

  it('a failing organization lookup leaves empty lists and keeps the employees', async () => {
    const { service } = setup({
      keys: ['employees:View'],
      lookup: () => throwError(() => FORBIDDEN),
    });
    const result = await firstValueFrom(service.load());
    expect(result.companies).toEqual([]);
    expect(result.departments).toEqual([]);
    expect(result.employees).toHaveLength(MOCK_EMPLOYEES.length);
  });

  it('a failing employee list leaves no employees and keeps the organization', async () => {
    const { service } = setup({
      keys: ['employees:View'],
      getAll: () => throwError(() => FORBIDDEN),
    });
    const result = await firstValueFrom(service.load());
    expect(result.employees).toEqual([]);
    expect(result.employeeRecords).toEqual([]);
    expect(result.departments).toEqual(LOOKUP.departments);
  });

  it('a failing employee picker leaves no employees', async () => {
    const { service } = setup({
      keys: ['recruitment:Edit'],
      picker: () => throwError(() => FORBIDDEN),
    });
    const result = await firstValueFrom(service.load());
    expect(result.employees).toEqual([]);
    expect(result.branches).toEqual(LOOKUP.branches);
  });

  it('organization() never fails either', async () => {
    const { service, calls } = setup({ keys: [], lookup: () => throwError(() => FORBIDDEN) });
    const org = await firstValueFrom(service.organization());
    expect(org.departments).toEqual([]);
    expect(calls.getAll + calls.picker).toBe(0);
  });
});
