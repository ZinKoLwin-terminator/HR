import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../core/configuration/app-config';
import { AppError } from '../core/models/common.models';
import { Employee } from '../core/models/employee.models';
import { DashboardDataService } from '../core/services/data/dashboard-data.service';
import { EmployeeDataService } from '../core/services/data/employee-data.service';
import { CompanyDataService } from '../core/services/data/organization-data.services';
import { ActivityMockService } from './activity/activity-mock.service';
import { provideMockDataServices } from './provide-mock-data-services';

async function expectAppError(
  promise: Promise<unknown>,
  code: AppError['code'],
): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code} error`);
}

describe('mock data services', () => {
  let employees: EmployeeDataService;
  let companies: CompanyDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    employees = TestBed.inject(EmployeeDataService);
    companies = TestBed.inject(CompanyDataService);
  });

  it('queries with search, filters, sorting and paging', async () => {
    const page = await firstValueFrom(
      employees.query({
        pageIndex: 0,
        pageSize: 5,
        search: '',
        filters: { departmentId: 4 },
        sort: { active: 'fullName', direction: 'asc' },
      }),
    );
    expect(page.total).toBeGreaterThan(5);
    expect(page.items).toHaveLength(5);
    expect(page.items.every((e) => e.departmentName === 'Engineering')).toBe(true);
    const names = page.items.map((e) => e.fullName);
    expect([...names].sort((a, b) => a.localeCompare(b))).toEqual(names);

    const byCode = await firstValueFrom(
      employees.query({ pageIndex: 0, pageSize: 10, search: 'emp001' }),
    );
    expect(byCode.items.map((e) => e.fullName)).toEqual(['Thura Aung']);
  });

  it('filters by joining date range', async () => {
    const page = await firstValueFrom(
      employees.query({
        pageIndex: 0,
        pageSize: 100,
        filters: { joiningFrom: '2014-01-01', joiningTo: '2015-12-31' },
      }),
    );
    expect(page.items.map((e) => e.employeeCode).sort()).toEqual(['EMP001', 'EMP002', 'EMP003']);
  });

  it('creates, updates and deletes, returning clones', async () => {
    const original = await firstValueFrom(companies.getById(1));
    const { id: _ignored, ...rest } = original;
    void _ignored;
    const created = await firstValueFrom(
      companies.create({ ...rest, code: 'NEW', name: 'New Co' }),
    );
    expect(created.id).toBe(4);

    const updated = await firstValueFrom(companies.update(created.id, { status: 'Inactive' }));
    expect(updated.status).toBe('Inactive');

    // Mutating a returned object must not affect the store.
    updated.name = 'Mutated';
    expect((await firstValueFrom(companies.getById(created.id))).name).toBe('New Co');

    await firstValueFrom(companies.delete(created.id));
    await expectAppError(firstValueFrom(companies.getById(created.id)), 'NotFound');
  });

  it('enforces uniqueness and business rules', async () => {
    const existing = await firstValueFrom(employees.getById(10));
    const err = await expectAppError(
      firstValueFrom(employees.update(10, { employeeCode: 'EMP001' } as Partial<Employee>)),
      'Validation',
    );
    expect(err.fieldErrors?.['employeeCode']).toBeTruthy();
    expect(existing.employeeCode).toBe('EMP010');

    // The CEO manages others, so deletion is blocked.
    await expectAppError(firstValueFrom(employees.delete(1)), 'Conflict');
  });

  it('records activity for changes', async () => {
    const activity = TestBed.inject(ActivityMockService);
    await firstValueFrom(companies.update(2, { phone: '+95 2 555 0299' }));
    expect(activity.snapshot(1)[0].message).toContain('Apex Horizon Logistics');
  });

  it('builds a consistent dashboard summary', async () => {
    const summary = await firstValueFrom(TestBed.inject(DashboardDataService).getSummary());
    const h = summary.headcount;
    expect(h.total).toBe(58); // 60 minus one resigned and one terminated
    expect(h.active + h.probation).toBeLessThanOrEqual(h.total);
    const a = summary.attendanceToday;
    expect(a.present + a.late + a.absent + a.onLeave + a.halfDay).toBe(a.scheduled);
    expect(summary.departmentDistribution.reduce((s, d) => s + d.count, 0)).toBe(h.total);
    expect(summary.headcountGrowth).toHaveLength(12);
    expect(summary.pendingLeaveApprovals).toBe(7);
    expect(summary.payroll).not.toBeNull();
  });
});
