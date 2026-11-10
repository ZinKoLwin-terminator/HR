import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { Company } from '../../core/models/organization.models';
import { ShiftDataService } from '../../core/services/data/attendance-data.services';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../../core/services/data/organization-data.services';
import { ShiftMockService } from '../../mock-services/attendance/attendance-mock.services';
import { MOCK_COMPANIES } from '../../mock-data/organization/companies.mock';
import { CompanyMockService } from '../../mock-services/organization/organization-mock.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { ApiDataOptions, provideApiDataServices } from '../provide-api-data-services';
import {
  BranchHttpService,
  CompanyHttpService,
  DepartmentHttpService,
  JobGradeHttpService,
  PositionHttpService,
  SectionHttpService,
} from './organization-http.services';

const company: Company = { ...MOCK_COMPANIES[0], id: 7, status: 'Active', rowVersion: 'AAAAAAAB' };

/** The editable fields of a company: no id, no rowVersion (what a form would hold). */
function fieldsOf(source: Company): Omit<Company, 'id' | 'rowVersion'> {
  const copy: Partial<Company> = { ...source };
  delete copy.id;
  delete copy.rowVersion;
  return copy as Omit<Company, 'id' | 'rowVersion'>;
}

function setup(options: ApiDataOptions = { enabled: true }) {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices(options),
      // After the adapter: the testing backend must replace the real one it registers.
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  // API mode checks the session at startup; answer it as signed out so verify() stays meaningful.
  TestBed.inject(HttpTestingController)
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return {
    http: TestBed.inject(HttpTestingController),
    companies: TestBed.inject(CompanyDataService),
  };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected a rejection');
}

describe('organization HTTP adapter', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  describe('binding', () => {
    it('binds the six organization contracts to HTTP only when enabled', () => {
      setup();
      expect(TestBed.inject(CompanyDataService)).toBeInstanceOf(CompanyHttpService);
      expect(TestBed.inject(BranchDataService)).toBeInstanceOf(BranchHttpService);
      expect(TestBed.inject(DepartmentDataService)).toBeInstanceOf(DepartmentHttpService);
      expect(TestBed.inject(SectionDataService)).toBeInstanceOf(SectionHttpService);
      expect(TestBed.inject(JobGradeDataService)).toBeInstanceOf(JobGradeHttpService);
      expect(TestBed.inject(PositionDataService)).toBeInstanceOf(PositionHttpService);
    });

    it('leaves every other contract on its mock', () => {
      setup();
      expect(TestBed.inject(ShiftDataService)).toBeInstanceOf(ShiftMockService);
    });

    it('keeps the mocks when the switch is off', () => {
      setup({ enabled: false });
      expect(TestBed.inject(CompanyDataService)).toBeInstanceOf(CompanyMockService);
    });
  });

  describe('reads', () => {
    it('getAll calls the unpaged route', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(companies.getAll());
      const req = http.expectOne('/api/companies');
      expect(req.request.method).toBe('GET');
      req.flush([company]);
      expect(await result).toEqual([company]);
    });

    it('builds the query string with paging, sort, search and filters', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(
        companies.query({
          pageIndex: 2,
          pageSize: 25,
          sort: { active: 'name', direction: 'desc' },
          search: '  apex  ',
          filters: { status: 'Active', ignored: 'x' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/companies/query');
      expect(req.request.params.get('pageIndex')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('sort')).toBe('name:desc');
      expect(req.request.params.get('search')).toBe('apex');
      expect(req.request.params.get('status')).toBe('Active');
      expect(req.request.params.has('ignored')).toBe(false);
      req.flush({ items: [company], total: 1, pageIndex: 2, pageSize: 25 });
      expect((await result).total).toBe(1);
    });

    it('omits blank and "all" filters, empty sort and empty search', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(
        companies.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'name', direction: '' },
          search: '   ',
          filters: { status: 'all' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/companies/query');
      expect(req.request.params.keys().sort()).toEqual(['pageIndex', 'pageSize']);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await result;
    });

    it('truncates search to the 100 characters the API allows', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(
        companies.query({ pageIndex: 0, pageSize: 10, search: 'x'.repeat(150) }),
      );
      const req = http.expectOne((r) => r.url === '/api/companies/query');
      expect(req.request.params.get('search')).toHaveLength(100);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await result;
    });

    it('sends each resource its own filters on its own route', async () => {
      const { http } = setup();
      const branches = TestBed.inject(BranchDataService);
      const result = firstValueFrom(
        branches.query({
          pageIndex: 0,
          pageSize: 10,
          filters: { companyId: 3, type: 'Branch', status: '' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/branches/query');
      expect(req.request.params.get('companyId')).toBe('3');
      expect(req.request.params.get('type')).toBe('Branch');
      expect(req.request.params.has('status')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await result;
    });

    it('uses the configured base URL and the hyphenated job-grades route', async () => {
      const { http } = setup({ enabled: true, baseUrl: 'https://hr.example/api' });
      const result = firstValueFrom(TestBed.inject(JobGradeDataService).getAll());
      http.expectOne('https://hr.example/api/job-grades').flush([]);
      await result;
    });
  });

  describe('writes', () => {
    it('create posts the fields including status and no rowVersion', async () => {
      const { http, companies } = setup();
      const model = fieldsOf(company);
      const result = firstValueFrom(companies.create({ ...model, rowVersion: 'ignored' }));
      const req = http.expectOne('/api/companies');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(model);
      expect(req.request.body).not.toHaveProperty('rowVersion');
      req.flush(company, { status: 201, statusText: 'Created' });
      expect((await result).rowVersion).toBe('AAAAAAAB');
    });

    it('full update sends PUT without status and with the remembered rowVersion', async () => {
      const { http, companies } = setup();
      const list = firstValueFrom(companies.getAll());
      http.expectOne('/api/companies').flush([company]);
      await list;

      const form = fieldsOf(company);
      const result = firstValueFrom(companies.update(7, { ...form, name: 'Renamed' }));
      const req = http.expectOne('/api/companies/7');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).not.toHaveProperty('status');
      expect(req.request.body).not.toHaveProperty('id');
      expect(req.request.body.rowVersion).toBe('AAAAAAAB');
      expect(req.request.body.name).toBe('Renamed');
      req.flush({ ...company, name: 'Renamed', rowVersion: 'AAAAAAAC' });
      expect((await result).rowVersion).toBe('AAAAAAAC');
    });

    it('fetches the record first when the rowVersion has never been seen', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(companies.update(7, { name: 'Renamed' }));
      http.expectOne('/api/companies/7').flush(company);
      await Promise.resolve();
      const put = http.expectOne('/api/companies/7');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ name: 'Renamed', rowVersion: 'AAAAAAAB' });
      put.flush({ ...company, name: 'Renamed' });
      await result;
    });

    it('a status-only patch calls deactivate with the current rowVersion, never PUT', async () => {
      const { http, companies } = setup();
      const list = firstValueFrom(companies.getAll());
      http.expectOne('/api/companies').flush([company]);
      await list;

      const result = firstValueFrom(companies.update(7, { status: 'Inactive' }));
      const req = http.expectOne('/api/companies/7/deactivate');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rowVersion: 'AAAAAAAB' });
      req.flush({ ...company, status: 'Inactive', rowVersion: 'AAAAAAAC' });
      expect((await result).status).toBe('Inactive');
    });

    it('a status-only patch calls activate and fetches the rowVersion when unknown', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(companies.update(7, { status: 'Active' }));
      http.expectOne('/api/companies/7').flush(company);
      await Promise.resolve();
      const req = http.expectOne('/api/companies/7/activate');
      expect(req.request.body).toEqual({ rowVersion: 'AAAAAAAB' });
      req.flush(company);
      await result;
    });

    it('a patch with fields and a different status updates then runs the command', async () => {
      const { http, companies } = setup();
      const result = firstValueFrom(
        companies.update(7, { name: 'Renamed', status: 'Inactive', rowVersion: 'AAAAAAAB' }),
      );
      const put = http.expectOne('/api/companies/7');
      expect(put.request.body).toEqual({ name: 'Renamed', rowVersion: 'AAAAAAAB' });
      put.flush({ ...company, name: 'Renamed', rowVersion: 'AAAAAAAC' });
      await Promise.resolve();
      const command = http.expectOne('/api/companies/7/deactivate');
      expect(command.request.body).toEqual({ rowVersion: 'AAAAAAAC' });
      command.flush({ ...company, name: 'Renamed', status: 'Inactive', rowVersion: 'AAAAAAAD' });
      expect((await result).status).toBe('Inactive');
    });

    it('delete sends the rowVersion in the request body', async () => {
      const { http, companies } = setup();
      const list = firstValueFrom(companies.getAll());
      http.expectOne('/api/companies').flush([company]);
      await list;

      const result = firstValueFrom(companies.delete(7));
      const req = http.expectOne('/api/companies/7');
      expect(req.request.method).toBe('DELETE');
      expect(req.request.body).toEqual({ rowVersion: 'AAAAAAAB' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBeUndefined();
    });
  });

  describe('errors', () => {
    it('maps a 400 problem to Validation with field errors and trace id', async () => {
      const { http, companies } = setup();
      const result = rejection(firstValueFrom(companies.getById(7)));
      http.expectOne('/api/companies/7').flush(
        {
          detail: 'Validation failed.',
          code: 'Validation',
          errors: { code: ['Code already exists.'] },
          traceId: 'abc',
        },
        { status: 400, statusText: 'Bad Request' },
      );
      expect(await result).toEqual({
        code: 'Validation',
        message: 'Validation failed.',
        fieldErrors: { code: 'Code already exists.' },
        traceId: 'abc',
      });
    });

    it('maps 404 to NotFound', async () => {
      const { http, companies } = setup();
      const result = rejection(firstValueFrom(companies.getById(99)));
      http
        .expectOne('/api/companies/99')
        .flush(
          { detail: 'Company 99 was not found.', code: 'NotFound' },
          { status: 404, statusText: 'Not Found' },
        );
      expect((await result).code).toBe('NotFound');
    });

    it('maps a 409 on delete to Conflict with the reload message', async () => {
      const { http, companies } = setup();
      const result = rejection(
        firstValueFrom(companies.update(7, { status: 'Inactive', rowVersion: 'AAAAAAAB' })),
      );
      http
        .expectOne('/api/companies/7/deactivate')
        .flush({ code: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      const error = await result;
      expect(error.code).toBe('Conflict');
      expect(error.message).toBe('The record changed. Reload and try again.');
    });

    it('maps 500 to Unknown', async () => {
      const { http, companies } = setup();
      const result = rejection(firstValueFrom(companies.query({ pageIndex: 0, pageSize: 10 })));
      http
        .expectOne((r) => r.url === '/api/companies/query')
        .flush(
          { detail: 'Unexpected error.', code: 'Unknown', traceId: 't' },
          { status: 500, statusText: 'Server Error' },
        );
      const error = await result;
      expect(error.code).toBe('Unknown');
      expect(error.traceId).toBe('t');
    });
  });
});
