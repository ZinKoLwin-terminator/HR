import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError, CreateModel } from '../../core/models/common.models';
import { Employee } from '../../core/models/employee.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { EmploymentHistoryDataService } from '../../core/services/data/employee-extras-data.services';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { EmployeeHttpService } from './employee-http.service';
import { EmploymentHistoryHttpService } from './employment-history-http.service';

const BASE = '/api/employees';

const listItem = {
  id: 5,
  employeeCode: 'EMP005',
  fullName: 'Aye Chan',
  firstName: 'Aye',
  lastName: 'Chan',
  workEmail: 'aye.chan@example.test',
  photoUrl: '',
  gender: 'Female',
  companyId: 1,
  branchId: 2,
  departmentId: 3,
  positionId: 4,
  employmentType: 'Full-time',
  employmentStatus: 'Active',
  joiningDate: '2024-01-15',
  shiftId: 9,
  rowVersion: 'AAAAV1',
};

const record = {
  ...listItem,
  sectionId: null,
  jobGradeId: 6,
  managerId: null,
  probationEndDate: null,
  exitDate: null,
  bankAccountMasked: '******7890',
};

const personal = {
  dateOfBirth: '1992-03-04',
  maritalStatus: 'Single',
  nationality: 'Myanmar',
  nationalId: '12/AAA(N)000000',
  personalEmail: '',
  phone: '+95 9 000 000 000',
  alternatePhone: '',
  address: {
    line1: '1 Test Road',
    line2: '',
    city: 'Yangon',
    state: '',
    postalCode: '',
    country: 'Myanmar',
  },
  emergencyContact: { name: 'Sample Person', relationship: 'Sibling', phone: '+95 9 111 111 111' },
  rowVersion: 'AAAAP1',
};

const bank = {
  employeeId: 5,
  bankName: 'Sample Bank',
  branchName: '',
  accountName: 'Aye Chan',
  accountNumber: '12345677890',
  rowVersion: 'AAAAB1',
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
  return { http, service: TestBed.inject(EmployeeDataService) };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

/** Lets pending subscriptions issue their next request. */
const tick = () => new Promise<void>((resolve) => setTimeout(resolve));

function answerLookups(http: HttpTestingController): void {
  http.expectOne('/api/branches').flush([{ id: 2, name: 'Head Office' }]);
  http.expectOne('/api/departments').flush([{ id: 3, name: 'Finance' }]);
  http.expectOne('/api/positions').flush([{ id: 4, title: 'Accountant' }]);
}

/** Loads employee 5 completely: GET record, personal, bank. */
async function loadEmployee(http: HttpTestingController, service: EmployeeDataService) {
  const promise = firstValueFrom(service.getById(5));
  http.expectOne(`${BASE}/5`).flush(record);
  await tick();
  http.expectOne(`${BASE}/5/personal`).flush(personal);
  http.expectOne(`${BASE}/5/bank-account`).flush(bank);
  return promise;
}

describe('EmployeeHttpService', () => {
  it('replaces the mock employee service only when API mode is on', () => {
    const { service } = setup();
    expect(service).toBeInstanceOf(EmployeeHttpService);
    expect(TestBed.inject(EmploymentHistoryDataService)).toBeInstanceOf(
      EmploymentHistoryHttpService,
    );
  });

  describe('list', () => {
    it('builds the query string from paging, sort, search and filters', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(
        service.query({
          pageIndex: 2,
          pageSize: 25,
          sort: { active: 'departmentName', direction: 'desc' },
          search: '  aye ',
          filters: {
            departmentId: 3,
            branchId: null,
            employmentStatus: 'Active',
            employmentType: null,
            joiningFrom: '2024-01-01',
          },
        }),
      );
      const req = http.expectOne((r) => r.url === `${BASE}/query`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('sort')).toBe('departmentId:desc');
      expect(req.request.params.get('search')).toBe('aye');
      expect(req.request.params.get('departmentId')).toBe('3');
      expect(req.request.params.get('employmentStatus')).toBe('Active');
      expect(req.request.params.has('branchId')).toBe(false);
      expect(req.request.params.has('employmentType')).toBe(false);
      expect(req.request.params.get('joiningFrom')).toBe('2024-01-01');
      req.flush({ items: [], total: 0, pageIndex: 2, pageSize: 25 });
      answerLookups(http);
      expect((await promise).total).toBe(0);
      http.verify();
    });

    it('never calls the sensitive endpoints, resolves names and carries no personal fields', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.query({ pageIndex: 0, pageSize: 10 }));
      // A server that wrongly sent personal data must not reach a table row.
      http
        .expectOne((r) => r.url === `${BASE}/query`)
        .flush({
          items: [{ ...listItem, nationalId: 'LEAK', dateOfBirth: '1990-01-01', bankName: 'LEAK' }],
          total: 1,
          pageIndex: 0,
          pageSize: 10,
        });
      answerLookups(http);
      const page = await promise;
      const row = page.items[0] as unknown as Record<string, unknown>;
      expect(row['branchName']).toBe('Head Office');
      expect(row['departmentName']).toBe('Finance');
      expect(row['positionTitle']).toBe('Accountant');
      expect(row['shiftName']).toBe('Shift #9');
      for (const key of ['nationalId', 'dateOfBirth', 'phone', 'address', 'bank', 'bankName']) {
        expect(key in row).toBe(false);
      }
      http.verify(); // no /personal or /bank-account request
    });

    it('getAll pages through the query until every row is read', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.getAll());
      const first = http.expectOne((r) => r.url === `${BASE}/query`);
      expect(first.request.params.get('pageIndex')).toBe('0');
      first.flush({ items: [listItem], total: 2, pageIndex: 0, pageSize: 200 });
      await tick();
      const second = http.expectOne((r) => r.url === `${BASE}/query`);
      expect(second.request.params.get('pageIndex')).toBe('1');
      second.flush({ items: [{ ...listItem, id: 6 }], total: 2, pageIndex: 1, pageSize: 200 });
      const all = await promise;
      expect(all.map((e) => e.id)).toEqual([5, 6]);
      expect(all[0].nationalId).toBe('');
      http.verify();
    });
  });

  describe('detail', () => {
    it('assembles the employee from the record, personal data and bank account', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      expect(employee.employeeCode).toBe('EMP005');
      expect(employee.nationalId).toBe('12/AAA(N)000000');
      expect(employee.address.city).toBe('Yangon');
      expect(employee.bank.bankName).toBe('Sample Bank');
      // The profile only ever receives the masked number.
      expect(employee.bank.accountNumber).toBe('******7890');
      expect(JSON.stringify(employee)).not.toContain('12345677890');
      expect(employee.photoUrl).toBeNull();
      http.verify();
    });

    it('answers a 403 on the sensitive endpoints with placeholder text, not an error', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.getById(5));
      http.expectOne(`${BASE}/5`).flush(record);
      await tick();
      const forbidden = { status: 403, statusText: 'Forbidden' };
      http.expectOne(`${BASE}/5/personal`).flush({ code: 'Forbidden' }, forbidden);
      http.expectOne(`${BASE}/5/bank-account`).flush({ code: 'Forbidden' }, forbidden);
      const employee = await promise;
      expect(employee.nationalId).toBe('Not permitted');
      expect(employee.bank.bankName).toBe('Not permitted');
      expect(employee.bank.accountNumber).toBe('******7890');
    });

    it('treats a missing bank account as "no account yet"', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.getById(5));
      http.expectOne(`${BASE}/5`).flush({ ...record, bankAccountMasked: null });
      await tick();
      http.expectOne(`${BASE}/5/personal`).flush(personal);
      http.expectOne(`${BASE}/5/bank-account`).flush({}, { status: 404, statusText: 'Not Found' });
      expect((await promise).bank.bankName).toBe('');
    });
  });

  describe('update', () => {
    it('sends each changed section to its own endpoint with its own rowVersion', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const promise = firstValueFrom(
        service.update(5, {
          ...employee,
          firstName: 'Aye Aye',
          phone: '+95 9 222 222 222',
          bank: { ...employee.bank, bankName: 'Other Bank', accountNumber: '99887766554' },
        }),
      );

      const put = http.expectOne({ method: 'PUT', url: `${BASE}/5` });
      expect(put.request.body.rowVersion).toBe('AAAAV1');
      expect(put.request.body.firstName).toBe('Aye Aye');
      expect('phone' in put.request.body).toBe(false);
      expect('nationalId' in put.request.body).toBe(false);
      expect('employmentStatus' in put.request.body).toBe(false);
      expect('bank' in put.request.body).toBe(false);
      put.flush({ ...record, firstName: 'Aye Aye', rowVersion: 'AAAAV2' });
      await tick();

      const putPersonal = http.expectOne({ method: 'PUT', url: `${BASE}/5/personal` });
      expect(putPersonal.request.body.rowVersion).toBe('AAAAP1');
      expect(putPersonal.request.body.phone).toBe('+95 9 222 222 222');
      expect('firstName' in putPersonal.request.body).toBe(false);
      putPersonal.flush({ ...personal, phone: '+95 9 222 222 222', rowVersion: 'AAAAP2' });
      await tick();

      const putBank = http.expectOne({ method: 'PUT', url: `${BASE}/5/bank-account` });
      expect(putBank.request.body).toEqual({
        bankName: 'Other Bank',
        branchName: undefined,
        accountName: 'Aye Chan',
        accountNumber: '99887766554',
        rowVersion: 'AAAAB1',
      });
      putBank.flush({
        ...bank,
        bankName: 'Other Bank',
        accountNumber: '99887766554',
        rowVersion: 'AAAAB2',
      });

      const saved = await promise;
      expect(saved.firstName).toBe('Aye Aye');
      expect(saved.bank.accountNumber).toBe('******6554');
      http.verify();
    });

    it('sends nothing for sections that did not change', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      await firstValueFrom(service.update(5, { ...employee }));
      http.verify();
    });

    it('re-reads the full account number when only another bank field changed', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const promise = firstValueFrom(
        service.update(5, { bank: { ...employee.bank, bankName: 'Renamed Bank' } }),
      );
      await tick();
      http.expectOne({ method: 'GET', url: `${BASE}/5/bank-account` }).flush(bank);
      await tick();
      const put = http.expectOne({ method: 'PUT', url: `${BASE}/5/bank-account` });
      expect(put.request.body.accountNumber).toBe('12345677890');
      put.flush({ ...bank, bankName: 'Renamed Bank' });
      await promise;
    });

    it('a partial change (shift only) sends the whole employment record', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.update(5, { shiftId: 11 }));
      http.expectOne({ method: 'GET', url: `${BASE}/5` }).flush(record);
      await tick();
      const put = http.expectOne({ method: 'PUT', url: `${BASE}/5` });
      expect(put.request.body.shiftId).toBe(11);
      expect(put.request.body.firstName).toBe('Aye');
      expect(put.request.body.employeeCode).toBe('EMP005');
      put.flush({ ...record, shiftId: 11, rowVersion: 'AAAAV2' });
      await promise;
      http.verify();
    });

    it('names the section that failed', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const failure = rejection(
        firstValueFrom(
          service.update(5, {
            address: { ...employee.address, city: '' },
            emergencyContact: { ...employee.emergencyContact, name: '' },
          }),
        ),
      );
      await tick();
      http.expectOne({ method: 'PUT', url: `${BASE}/5/personal` }).flush(
        {
          code: 'Validation',
          detail: 'Invalid.',
          errors: { 'address.city': ['City is required.'], 'emergencyContact.name': ['Required.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      const error = await failure;
      expect(error.code).toBe('Validation');
      expect(error.message).toContain('Personal details not saved');
      expect(error.fieldErrors).toEqual({
        city: 'City is required.',
        emergencyName: 'Required.',
      });
    });

    it('refuses to write personal data it was not allowed to read', async () => {
      const { http, service } = setup();
      const promise = firstValueFrom(service.getById(5));
      http.expectOne(`${BASE}/5`).flush(record);
      await tick();
      const denied = { status: 403, statusText: 'Forbidden' };
      http.expectOne(`${BASE}/5/personal`).flush({}, denied);
      http.expectOne(`${BASE}/5/bank-account`).flush({}, denied);
      const employee = await promise;
      const error = await rejection(
        firstValueFrom(service.update(5, { phone: '+95 9 333 333 333' })),
      );
      expect(error.code).toBe('Forbidden');
      expect(error.message).toContain('not allowed');
      expect(employee.phone).toBe('Not permitted');
      http.verify(); // no PUT was sent
    });

    it('maps a 409 on save to the reload message', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const failure = rejection(
        firstValueFrom(service.update(5, { ...employee, lastName: 'Zaw' })),
      );
      http
        .expectOne({ method: 'PUT', url: `${BASE}/5` })
        .flush({ code: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      const error = await failure;
      expect(error.code).toBe('Conflict');
      expect(error.message).toContain('The record changed. Reload and try again.');
    });
  });

  describe('status', () => {
    it('uses the status command, never a PUT', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const promise = firstValueFrom(
        service.update(5, { ...employee, employmentStatus: 'On Leave' }),
      );
      const post = http.expectOne({ method: 'POST', url: `${BASE}/5/status` });
      expect(post.request.body).toEqual({ rowVersion: 'AAAAV1', status: 'On Leave' });
      post.flush({ ...record, employmentStatus: 'On Leave', rowVersion: 'AAAAV2' });
      expect((await promise).employmentStatus).toBe('On Leave');
      http.verify();
    });

    it('uses exit for Resigned and reinstate for coming back', async () => {
      const { http, service } = setup();
      const employee = await loadEmployee(http, service);
      const exit = firstValueFrom(
        service.update(5, { employmentStatus: 'Resigned', exitDate: '2025-06-30' }),
      );
      const post = http.expectOne({ method: 'POST', url: `${BASE}/5/exit` });
      expect(post.request.body).toEqual({
        rowVersion: 'AAAAV1',
        exitDate: '2025-06-30',
        status: 'Resigned',
      });
      post.flush({
        ...record,
        employmentStatus: 'Resigned',
        exitDate: '2025-06-30',
        rowVersion: 'AAAAV2',
      });
      expect((await exit).exitDate).toBe('2025-06-30');

      const back = firstValueFrom(service.update(employee.id, { employmentStatus: 'Active' }));
      const reinstate = http.expectOne({ method: 'POST', url: `${BASE}/5/reinstate` });
      expect(reinstate.request.body).toEqual({ rowVersion: 'AAAAV2', status: 'Active' });
      reinstate.flush({ ...record, rowVersion: 'AAAAV3' });
      await back;
    });
  });

  describe('delete', () => {
    it('sends the rowVersion in the body', async () => {
      const { http, service } = setup();
      await loadEmployee(http, service);
      const promise = firstValueFrom(service.delete(5));
      const req = http.expectOne({ method: 'DELETE', url: `${BASE}/5` });
      expect(req.request.body).toEqual({ rowVersion: 'AAAAV1' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      await promise;
    });

    it('maps a 409 (direct reports) to a conflict error', async () => {
      const { http, service } = setup();
      const failure = rejection(firstValueFrom(service.delete(5)));
      http.expectOne({ method: 'GET', url: `${BASE}/5` }).flush(record);
      await tick();
      http
        .expectOne({ method: 'DELETE', url: `${BASE}/5` })
        .flush(
          { code: 'Conflict', detail: 'Aye Chan manages 2 employees.' },
          { status: 409, statusText: 'Conflict' },
        );
      const error = await failure;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('Aye Chan manages 2 employees.');
    });
  });

  describe('create', () => {
    const base = MOCK_EMPLOYEES[0];
    const model: CreateModel<Employee> = {
      ...base,
      employeeCode: 'EMP061',
      employmentStatus: 'Probation',
      bank: {
        bankName: 'Sample Bank',
        branchName: '',
        accountName: 'Sample',
        accountNumber: '12345678',
      },
    };

    it('reads the next code, then posts personal fields at the top level and saves the bank', async () => {
      const { http, service } = setup();
      const codePromise = firstValueFrom(service.nextEmployeeCode());
      http.expectOne(`${BASE}/next-code`).flush({ code: 'EMP061' });
      expect(await codePromise).toBe('EMP061');

      const promise = firstValueFrom(service.create(model));
      const post = http.expectOne({ method: 'POST', url: BASE });
      expect(post.request.body.employeeCode).toBe('EMP061');
      expect(post.request.body.employmentStatus).toBe('Probation');
      expect(post.request.body.nationalId).toBe(base.nationalId);
      expect(post.request.body.address).toEqual(base.address);
      expect('bank' in post.request.body).toBe(false);
      expect('id' in post.request.body).toBe(false);
      post.flush({ ...record, id: 61, employeeCode: 'EMP061', employmentStatus: 'Probation' });
      await tick();
      const bankPut = http.expectOne({ method: 'PUT', url: `${BASE}/61/bank-account` });
      expect(bankPut.request.body.accountNumber).toBe('12345678');
      expect(bankPut.request.body.rowVersion).toBeUndefined();
      bankPut.flush({ ...bank, employeeId: 61, accountNumber: '12345678' });

      const created = await promise;
      expect(created.id).toBe(61);
      expect(created.bank.accountNumber).toBe('******5678');
      http.verify();
    });

    it('reports a bank failure after the employee was created', async () => {
      const { http, service } = setup();
      const failure = rejection(firstValueFrom(service.create(model)));
      http.expectOne({ method: 'POST', url: BASE }).flush({ ...record, id: 61 });
      await tick();
      http
        .expectOne({ method: 'PUT', url: `${BASE}/61/bank-account` })
        .flush({ code: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
      const error = await failure;
      expect(error.code).toBe('Forbidden');
      expect(error.message).toContain('was created');
      expect(error.message).toContain('bank account was not saved');
    });
  });

  describe('history', () => {
    it('reads the history of one employee, newest first, and refuses writes', async () => {
      setup();
      const history = TestBed.inject(EmploymentHistoryDataService);
      const http = TestBed.inject(HttpTestingController);
      const promise = firstValueFrom(
        history.query({
          pageIndex: 0,
          pageSize: 200,
          filters: { employeeId: 5 },
          sort: { active: 'date', direction: 'desc' },
        }),
      );
      http.expectOne(`${BASE}/5/history`).flush([
        {
          id: 1,
          employeeId: 5,
          date: '2024-01-15',
          type: 'Joined',
          title: 'Joined',
          details: '',
          recordedBy: 'System',
        },
        {
          id: 2,
          employeeId: 5,
          date: '2025-01-01',
          type: 'Promotion',
          title: 'Promoted',
          details: '',
          recordedBy: 'System',
        },
      ]);
      const page = await promise;
      expect(page.items.map((e) => e.id)).toEqual([2, 1]);
      expect((await rejection(firstValueFrom(history.delete(1)))).code).toBe('Forbidden');
    });
  });
});
