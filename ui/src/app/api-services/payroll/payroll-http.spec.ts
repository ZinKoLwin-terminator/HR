import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  PayrollRunDataService,
  PayslipDataService,
  SalaryAssignmentDataService,
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../../core/services/data/payroll-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { PayrollRunHttpService } from './payroll-run-http.service';
import {
  SalaryComponentHttpService,
  SalaryStructureHttpService,
} from './payroll-setup-http.services';
import { PayslipHttpService } from './payslip-http.service';
import { SalaryAssignmentHttpService } from './salary-assignment-http.service';

const MASKED = '•••• 7890';

const component = {
  id: 1,
  code: 'HRA',
  name: 'Housing allowance',
  type: 'Earning',
  calculation: 'PercentOfBasic',
  value: 10,
  description: null,
  status: 'Active',
  rowVersion: 'CMPV1',
};
const structure = {
  id: 2,
  code: 'STAFF',
  name: 'Staff',
  description: null,
  componentIds: [3, 1],
  status: 'Active',
  rowVersion: 'STRV1',
};
const assignment = (id: number) => ({
  id,
  employeeId: 5,
  structureId: 2,
  basicSalary: 1000,
  effectiveFrom: '2026-01-01',
  remarks: null,
  rowVersion: `ASGV${id}`,
});
const run = {
  id: 9,
  periodName: 'October 2026',
  year: 2026,
  month: 10,
  startDate: '2026-10-01',
  endDate: '2026-10-31',
  payDate: '2026-10-31',
  status: 'Draft',
  employeeCount: 0,
  totalBasic: 0,
  totalAllowances: 0,
  totalGross: 0,
  totalDeductions: 0,
  totalNet: 0,
  warnings: null,
  processedOn: null,
  processedBy: null,
  approvedBy: null,
  approvedOn: null,
  paidOn: null,
  rowVersion: 'RUNV1',
};
const payslip = {
  id: 40,
  runId: 9,
  periodName: 'October 2026',
  employeeId: 5,
  employeeCode: 'EMP-0005',
  employeeName: 'Ada Sample',
  departmentId: 2,
  departmentName: 'Engineering',
  positionTitle: 'Engineer',
  companyName: 'Example Co',
  bankName: 'Sample Bank',
  accountMasked: MASKED,
  basicSalary: 1000,
  earnings: [{ code: 'HRA', name: 'Housing', amount: 100, basis: '10% of basic' }],
  deductions: [],
  grossSalary: 1100,
  totalDeductions: 0,
  netSalary: 1100,
  periodDays: 31,
  daysEmployed: 31,
  workingDays: 22,
  unpaidLeaveDays: 0,
  overtimeMinutes: 0,
  generatedOn: '2026-11-01',
};

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  http
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return {
    http,
    components: TestBed.inject(SalaryComponentDataService),
    structures: TestBed.inject(SalaryStructureDataService),
    assignments: TestBed.inject(SalaryAssignmentDataService),
    runs: TestBed.inject(PayrollRunDataService),
    payslips: TestBed.inject(PayslipDataService),
  };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

const noContent = { status: 204, statusText: 'No Content' };

describe('Payroll HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mock payroll services in API mode', () => {
    setup();
    expect(TestBed.inject(SalaryComponentDataService)).toBeInstanceOf(SalaryComponentHttpService);
    expect(TestBed.inject(SalaryStructureDataService)).toBeInstanceOf(SalaryStructureHttpService);
    expect(TestBed.inject(SalaryAssignmentDataService)).toBeInstanceOf(SalaryAssignmentHttpService);
    expect(TestBed.inject(PayrollRunDataService)).toBeInstanceOf(PayrollRunHttpService);
    expect(TestBed.inject(PayslipDataService)).toBeInstanceOf(PayslipHttpService);
  });

  describe('salary components and structures', () => {
    it('lists, creates with the status, updates with rowVersion and deletes with the body', async () => {
      const { http, components } = setup();
      const all = firstValueFrom(components.getAll());
      http.expectOne('/api/salary-components').flush([component]);
      expect((await all)[0].description).toBe('');

      const created = firstValueFrom(
        components.create({
          code: 'TRN',
          name: 'Transport',
          type: 'Earning',
          calculation: 'Fixed',
          value: 50,
          description: '',
          status: 'Active',
        }),
      );
      const post = http.expectOne('/api/salary-components');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({
        code: 'TRN',
        name: 'Transport',
        type: 'Earning',
        calculation: 'Fixed',
        value: 50,
        description: '',
        status: 'Active',
      });
      post.flush({ ...component, id: 7 });
      await created;

      const updated = firstValueFrom(
        components.update(1, { ...component, name: 'Housing', status: 'Active' } as never),
      );
      const put = http.expectOne('/api/salary-components/1');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body.rowVersion).toBe('CMPV1');
      expect(put.request.body.status).toBeUndefined();
      put.flush({ ...component, name: 'Housing', rowVersion: 'CMPV2' });
      await updated;

      const removed = firstValueFrom(components.delete(1));
      const del = http.expectOne('/api/salary-components/1');
      expect(del.request.method).toBe('DELETE');
      expect(del.request.body).toEqual({ rowVersion: 'CMPV2' });
      del.flush(null, noContent);
      await removed;
    });

    it('maps a status-only update to activate and deactivate commands', async () => {
      const { http, components } = setup();
      const all = firstValueFrom(components.getAll());
      http.expectOne('/api/salary-components').flush([component]);
      await all;
      const off = firstValueFrom(components.update(1, { status: 'Inactive' }));
      const req = http.expectOne('/api/salary-components/1/deactivate');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'CMPV1' });
      req.flush({ ...component, status: 'Inactive', rowVersion: 'CMPV2' });
      await off;
      const on = firstValueFrom(components.update(1, { status: 'Active' }));
      const again = http.expectOne('/api/salary-components/1/activate');
      expect(again.request.body).toEqual({ rowVersion: 'CMPV2' });
      again.flush(component);
      await on;
    });

    it('keeps componentIds in order and sends them on create and update', async () => {
      const { http, structures } = setup();
      const created = firstValueFrom(
        structures.create({
          code: 'STAFF',
          name: 'Staff',
          description: 'x',
          componentIds: [3, 1],
          status: 'Active',
        }),
      );
      const post = http.expectOne('/api/salary-structures');
      expect(post.request.body.componentIds).toEqual([3, 1]);
      post.flush(structure);
      const result = await created;
      expect(result.description).toBe('');

      const updated = firstValueFrom(structures.update(2, { name: 'Staff 2', componentIds: [1] }));
      const put = http.expectOne('/api/salary-structures/2');
      expect(put.request.body).toEqual({
        name: 'Staff 2',
        componentIds: [1],
        rowVersion: 'STRV1',
      });
      put.flush(structure);
      await updated;
    });

    it('queries with the filters and sort keys the API accepts', async () => {
      const { http, components } = setup();
      const promise = firstValueFrom(
        components.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'type', direction: 'asc' },
          filters: { type: 'Earning', calculation: 'all', bogus: 1 } as never,
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/salary-components/query');
      expect(req.request.params.get('sort')).toBe('type:asc');
      expect(req.request.params.get('type')).toBe('Earning');
      expect(req.request.params.has('calculation')).toBe(false);
      expect(req.request.params.has('bogus')).toBe(false);
      req.flush({ items: [component], total: 1, pageIndex: 0, pageSize: 10 });
      await promise;
    });
  });

  describe('salary assignments', () => {
    it('getAll pages through /query until every row is read', async () => {
      const { http, assignments } = setup();
      const promise = firstValueFrom(assignments.getAll());
      const first = http.expectOne((r) => r.url === '/api/salary-assignments/query');
      expect(first.request.params.get('pageIndex')).toBe('0');
      expect(first.request.params.get('pageSize')).toBe('200');
      first.flush({
        items: Array.from({ length: 200 }, (_, i) => assignment(i + 1)),
        total: 201,
        pageIndex: 0,
        pageSize: 200,
      });
      const second = http.expectOne((r) => r.url === '/api/salary-assignments/query');
      expect(second.request.params.get('pageIndex')).toBe('1');
      second.flush({ items: [assignment(201)], total: 201, pageIndex: 1, pageSize: 200 });
      const all = await promise;
      expect(all).toHaveLength(201);
      expect(all[0].remarks).toBe('');
    });

    it('queries by employee and structure, creates, updates and deletes with rowVersion', async () => {
      const { http, assignments } = setup();
      const page = firstValueFrom(
        assignments.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'effectiveFrom', direction: 'desc' },
          filters: { employeeId: 5, structureId: 2 },
        }),
      );
      const q = http.expectOne((r) => r.url === '/api/salary-assignments/query');
      expect(q.request.params.get('employeeId')).toBe('5');
      expect(q.request.params.get('structureId')).toBe('2');
      expect(q.request.params.get('sort')).toBe('effectiveFrom:desc');
      q.flush({ items: [assignment(1)], total: 1, pageIndex: 0, pageSize: 10 });
      await page;

      const created = firstValueFrom(
        assignments.create({
          employeeId: 5,
          structureId: 2,
          basicSalary: 1200,
          effectiveFrom: '2026-06-01',
          remarks: '',
        }),
      );
      const post = http.expectOne('/api/salary-assignments');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({
        employeeId: 5,
        structureId: 2,
        basicSalary: 1200,
        effectiveFrom: '2026-06-01',
        remarks: null,
      });
      post.flush(assignment(2));
      await created;

      const updated = firstValueFrom(assignments.update(1, { basicSalary: 1500 }));
      const put = http.expectOne('/api/salary-assignments/1');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        employeeId: 5,
        structureId: 2,
        basicSalary: 1500,
        effectiveFrom: '2026-01-01',
        remarks: null,
        rowVersion: 'ASGV1',
      });
      put.flush(assignment(1));
      await updated;

      const removed = firstValueFrom(assignments.delete(1));
      const del = http.expectOne('/api/salary-assignments/1');
      expect(del.request.method).toBe('DELETE');
      expect(del.request.body).toEqual({ rowVersion: 'ASGV1' });
      del.flush(null, noContent);
      await removed;
    });

    it('surfaces the server message of the last-assignment 409 unchanged', async () => {
      const { http, assignments } = setup();
      const failed = rejection(firstValueFrom(assignments.delete(1)));
      http.expectOne('/api/salary-assignments/1').flush(assignment(1));
      const del = http.expectOne('/api/salary-assignments/1');
      expect(del.request.method).toBe('DELETE');
      del.flush(
        {
          detail: 'An employee needs at least one salary assignment. Add a new one instead.',
          code: 'Conflict',
        },
        { status: 409, statusText: 'Conflict' },
      );
      const error = await failed;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe(
        'An employee needs at least one salary assignment. Add a new one instead.',
      );
    });
  });

  describe('payroll runs', () => {
    it('lists, filters by status and year, and normalises null warnings', async () => {
      const { http, runs } = setup();
      const all = firstValueFrom(runs.getAll());
      http.expectOne('/api/payroll-runs').flush([run]);
      const rows = await all;
      expect(rows[0].warnings).toEqual([]);

      const page = firstValueFrom(
        runs.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'startDate', direction: 'desc' },
          filters: { status: 'Pending Approval', year: 2026 },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/payroll-runs/query');
      expect(req.request.params.get('status')).toBe('Pending Approval');
      expect(req.request.params.get('year')).toBe('2026');
      expect(req.request.params.get('sort')).toBe('startDate:desc');
      req.flush({ items: [run], total: 1, pageIndex: 0, pageSize: 10 });
      await page;
    });

    it('creates and updates with year, month and pay date only', async () => {
      const { http, runs } = setup();
      const created = firstValueFrom(
        runs.create({
          year: 2026,
          month: 10,
          payDate: '',
          status: 'Draft',
          totalNet: 0,
        } as never),
      );
      const post = http.expectOne('/api/payroll-runs');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({ year: 2026, month: 10, payDate: null });
      post.flush(run);
      await created;

      const updated = firstValueFrom(runs.update(9, { payDate: '2026-11-05' }));
      const put = http.expectOne('/api/payroll-runs/9');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        year: 2026,
        month: 10,
        payDate: '2026-11-05',
        rowVersion: 'RUNV1',
      });
      put.flush({ ...run, rowVersion: 'RUNV2' });
      await updated;

      const removed = firstValueFrom(runs.delete(9));
      const del = http.expectOne('/api/payroll-runs/9');
      expect(del.request.body).toEqual({ rowVersion: 'RUNV2' });
      del.flush(null, noContent);
      await removed;
    });

    it('maps the workflow to its commands with the latest rowVersion', async () => {
      const { http, runs } = setup();
      const seeded = firstValueFrom(runs.getAll());
      http.expectOne('/api/payroll-runs').flush([run]);
      await seeded;

      const steps: [() => Promise<unknown>, string, string, string][] = [
        [() => firstValueFrom(runs.process(9)), 'process', 'RUNV1', 'RUNV2'],
        [() => firstValueFrom(runs.approve(9)), 'approve', 'RUNV2', 'RUNV3'],
        [() => firstValueFrom(runs.markPaid(9)), 'mark-paid', 'RUNV3', 'RUNV4'],
      ];
      for (const [call, command, sent, next] of steps) {
        const promise = call();
        const req = http.expectOne(`/api/payroll-runs/9/${command}`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual({ rowVersion: sent });
        req.flush({ ...run, rowVersion: next });
        await promise;
      }
    });

    it('sends the return-to-draft reason, leaves a blank one out and rejects one over 200', async () => {
      const { http, runs } = setup();
      const seeded = firstValueFrom(runs.getAll());
      http.expectOne('/api/payroll-runs').flush([run]);
      await seeded;

      const withReason = firstValueFrom(runs.returnToDraft(9, '  wrong overtime  '));
      const a = http.expectOne('/api/payroll-runs/9/return-to-draft');
      expect(a.request.body).toEqual({ rowVersion: 'RUNV1', reason: 'wrong overtime' });
      a.flush({ ...run, rowVersion: 'RUNV2' });
      await withReason;

      const blank = firstValueFrom(runs.returnToDraft(9, '   '));
      const b = http.expectOne('/api/payroll-runs/9/return-to-draft');
      expect(b.request.body).toEqual({ rowVersion: 'RUNV2' });
      b.flush(run);
      await blank;

      const tooLong = await rejection(firstValueFrom(runs.returnToDraft(9, 'x'.repeat(201))));
      expect(tooLong.code).toBe('Validation');
    });

    it('maps a 409 on approve to Conflict with the server message', async () => {
      const { http, runs } = setup();
      const seeded = firstValueFrom(runs.getAll());
      http.expectOne('/api/payroll-runs').flush([run]);
      await seeded;
      const failed = rejection(firstValueFrom(runs.approve(9)));
      http
        .expectOne('/api/payroll-runs/9/approve')
        .flush(
          { detail: 'You processed this period, so someone else must approve it.' },
          { status: 409, statusText: 'Conflict' },
        );
      const error = await failed;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('You processed this period, so someone else must approve it.');
    });

    it('reads a run it has never seen before sending a command', async () => {
      const { http, runs } = setup();
      const promise = firstValueFrom(runs.process(9));
      http.expectOne('/api/payroll-runs/9').flush(run);
      const req = http.expectOne('/api/payroll-runs/9/process');
      expect(req.request.body).toEqual({ rowVersion: 'RUNV1' });
      req.flush({ ...run, status: 'Pending Approval', warnings: ['EMP-1 has no bank account.'] });
      const result = await promise;
      expect(result.warnings).toEqual(['EMP-1 has no bank account.']);
    });
  });

  describe('payslips', () => {
    it('queries by run, employee and department and keeps accountMasked', async () => {
      const { http, payslips } = setup();
      const promise = firstValueFrom(
        payslips.query({
          pageIndex: 2,
          pageSize: 50,
          sort: { active: 'employeeCode', direction: 'asc' },
          search: 'ada',
          filters: { runId: 9, employeeId: null, departmentId: 2 },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/payslips/query');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('runId')).toBe('9');
      expect(req.request.params.has('employeeId')).toBe(false);
      expect(req.request.params.get('departmentId')).toBe('2');
      expect(req.request.params.get('sort')).toBe('employeeCode:asc');
      expect(req.request.params.get('search')).toBe('ada');
      req.flush({ items: [payslip], total: 1, pageIndex: 2, pageSize: 50 });
      const page = await promise;
      expect(page.items[0].accountMasked).toBe(MASKED);
      expect(page.items[0].earnings[0].basis).toBe('10% of basic');
    });

    it('reads one payslip and maps errors', async () => {
      const { http, payslips } = setup();
      const one = firstValueFrom(payslips.getById(40));
      http.expectOne('/api/payslips/40').flush(payslip);
      expect((await one).accountMasked).toBe(MASKED);

      const missing = rejection(firstValueFrom(payslips.getById(41)));
      http
        .expectOne('/api/payslips/41')
        .flush({ detail: 'Payslip not found.' }, { status: 404, statusText: 'Not Found' });
      const error = await missing;
      expect(error.code).toBe('NotFound');
      expect(error.message).toBe('Payslip not found.');
      expect(error).not.toBeInstanceOf(HttpErrorResponse);
    });
  });
});
