import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import {
  EmployeeDocumentDataService,
  EmployeeSalaryDataService,
  EmploymentHistoryDataService,
} from '../../core/services/data/employee-extras-data.services';
import {
  LeaveApplicationDataService,
  LeaveTypeDataService,
} from '../../core/services/data/leave-data.services';
import { computeLeaveBalances } from '../../core/utilities/leave-balance';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { formatTenure } from '../../shared/utilities/date.util';
import { toEmployeeModel, toFormValues } from './employee-form/employee-form.mapper';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('employee data', () => {
  let employees: EmployeeDataService;
  let documents: EmployeeDocumentDataService;
  let history: EmploymentHistoryDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    employees = TestBed.inject(EmployeeDataService);
    documents = TestBed.inject(EmployeeDocumentDataService);
    history = TestBed.inject(EmploymentHistoryDataService);
  });

  const historyFor = async (employeeId: number) =>
    (await firstValueFrom(history.query({ pageIndex: 0, pageSize: 100, filters: { employeeId } })))
      .items;

  it('suggests the next employee code', async () => {
    expect(await firstValueFrom(employees.nextEmployeeCode())).toBe('EMP061');
  });

  it('round-trips an employee through the form mapper without losing data', () => {
    for (const employee of MOCK_EMPLOYEES) {
      const { id, ...expected } = employee;
      const model = toEmployeeModel(toFormValues(employee, null), employee.photoUrl);
      expect(model, `employee ${id}`).toEqual(expected);
    }
  });

  it('records history when position, status or type change', async () => {
    const before = (await historyFor(15)).length;
    await firstValueFrom(employees.update(15, { employmentStatus: 'Suspended' }));
    await firstValueFrom(employees.update(15, { employmentType: 'Contract' }));
    // Position 11 (Senior Frontend Engineer) is a higher grade than 15's current role: a promotion.
    await firstValueFrom(employees.update(15, { positionId: 11, jobGradeId: 4 }));

    const entries = await historyFor(15);
    expect(entries.length).toBe(before + 3);
    const types = entries.slice(-3).map((e) => e.type);
    expect(types).toEqual(['Status Change', 'Contract Change', 'Promotion']);
    expect(entries.at(-1)?.title).toContain('→');
  });

  it('adds a Joined entry when an employee is created and cascades deletes', async () => {
    const template = await firstValueFrom(employees.getById(60));
    const { id: _id, ...model } = template;
    void _id;
    const created = await firstValueFrom(
      employees.create({
        ...model,
        employeeCode: 'EMP061',
        workEmail: 'new.person@apexhorizon.example',
        managerId: null,
      }),
    );
    expect((await historyFor(created.id)).map((h) => h.type)).toEqual(['Joined']);

    await firstValueFrom(
      documents.create({
        employeeId: created.id,
        type: 'Resume',
        title: 'CV',
        fileName: 'cv.pdf',
        uploadedOn: '2026-01-01',
        expiryDate: null,
      }),
    );
    const docsOf = async (employeeId: number) =>
      (
        await firstValueFrom(
          documents.query({ pageIndex: 0, pageSize: 50, filters: { employeeId } }),
        )
      ).items;
    expect(await docsOf(created.id)).toHaveLength(1);

    await firstValueFrom(employees.delete(created.id));
    expect(await docsOf(created.id)).toHaveLength(0);
    expect(await historyFor(created.id)).toHaveLength(0);
  });

  it('rejects documents for unknown employees and filters by employee', async () => {
    const error = await expectError(
      firstValueFrom(
        documents.create({
          employeeId: 999,
          type: 'Other',
          title: 'x',
          fileName: 'x.pdf',
          uploadedOn: '2026-01-01',
          expiryDate: null,
        }),
      ),
      'Validation',
    );
    expect(error.fieldErrors?.['employeeId']).toBeTruthy();

    const own = await firstValueFrom(
      documents.query({ pageIndex: 0, pageSize: 50, filters: { employeeId: 5 } }),
    );
    expect(own.items.every((d) => d.employeeId === 5)).toBe(true);
    expect(own.items.some((d) => d.type === 'Work Permit')).toBe(true);
  });

  it('builds a consistent sample salary summary', async () => {
    const salary = TestBed.inject(EmployeeSalaryDataService);
    const s = await firstValueFrom(salary.getSummary(3));
    expect(s.gross).toBe(s.basic + s.allowances);
    expect(s.net).toBe(s.gross - s.deductions);
    expect(s.basic).toBeGreaterThanOrEqual(s.bandMin);
    expect(s.basic).toBeLessThanOrEqual(s.bandMax);
    expect(s.bandPosition).toBeGreaterThanOrEqual(0);
    expect(s.bandPosition).toBeLessThanOrEqual(100);
    expect(s.assumptions.join(' ')).toMatch(/not real tax/i);
    await expectError(firstValueFrom(salary.getSummary(999)), 'NotFound');
  });

  it('computes leave balances with gender eligibility', async () => {
    const types = await firstValueFrom(TestBed.inject(LeaveTypeDataService).getAll());
    const apps = (
      await firstValueFrom(
        TestBed.inject(LeaveApplicationDataService).query({
          pageIndex: 0,
          pageSize: 100,
          filters: { employeeId: 35 },
        }),
      )
    ).items;

    const female = computeLeaveBalances(types, apps, 'Female', new Date().getFullYear());
    const male = computeLeaveBalances(types, [], 'Male', new Date().getFullYear());
    expect(female.map((b) => b.code)).toContain('MAT');
    expect(female.map((b) => b.code)).not.toContain('PAT');
    expect(male.map((b) => b.code)).toContain('PAT');
    expect(male.map((b) => b.code)).not.toContain('MAT');

    const maternity = female.find((b) => b.code === 'MAT')!;
    expect(maternity.taken).toBeGreaterThan(0);
    for (const b of female) {
      expect(b.available).toBe(Math.max(0, b.entitled - b.taken - b.pending));
    }
  });

  it('formats tenure', () => {
    expect(formatTenure('2023-01-15', '2026-03-20')).toBe('3 yrs 2 mos');
    expect(formatTenure('2026-02-01', '2026-11-03')).toBe('9 mos');
    expect(formatTenure('2026-10-20', '2026-11-03')).toBe('14 days');
    expect(formatTenure('2025-11-03', '2026-11-03')).toBe('1 yr');
  });
});
