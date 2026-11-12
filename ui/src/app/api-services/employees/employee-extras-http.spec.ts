import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AppError } from '../../core/models/common.models';
import {
  EmployeeDocumentDataService,
  EmployeeSalaryDataService,
} from '../../core/services/data/employee-extras-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { EmployeeDocumentHttpService } from './employee-document-http.service';
import { EmployeeSalaryHttpService } from './employee-salary-http.service';

const doc = {
  id: 7,
  employeeId: 5,
  type: 'Passport',
  title: 'Passport copy',
  fileName: 'passport.pdf',
  uploadedOn: '2026-01-02',
  expiryDate: null,
  rowVersion: 'AAAAV1',
};

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      // After the adapter: the testing backend must replace the real one it registers.
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  TestBed.inject(HttpTestingController)
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return {
    http: TestBed.inject(HttpTestingController),
    docs: TestBed.inject(EmployeeDocumentDataService),
    salary: TestBed.inject(EmployeeSalaryDataService),
  };
}

describe('employee documents and salary summary HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('binds both contracts to HTTP', () => {
    const { docs, salary } = setup();
    expect(docs).toBeInstanceOf(EmployeeDocumentHttpService);
    expect(salary).toBeInstanceOf(EmployeeSalaryHttpService);
  });

  it('queries one employee through /query with the filters', async () => {
    const { http, docs } = setup();
    const result = firstValueFrom(
      docs.query({
        pageIndex: 0,
        pageSize: 10,
        sort: { active: 'uploadedOn', direction: 'desc' },
        search: ' pass ',
        filters: { employeeId: 5, type: 'Passport' },
      }),
    );
    const req = http.expectOne((r) => r.url === '/api/employee-documents/query');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('employeeId')).toBe('5');
    expect(req.request.params.get('type')).toBe('Passport');
    expect(req.request.params.get('search')).toBe('pass');
    expect(req.request.params.get('sort')).toBe('uploadedOn:desc');
    req.flush({ items: [doc], total: 1, pageIndex: 0, pageSize: 10 });
    expect((await result).items[0].title).toBe('Passport copy');
  });

  it('creates with metadata only and no rowVersion or id', async () => {
    const { http, docs } = setup();
    const result = firstValueFrom(
      docs.create({
        employeeId: 5,
        type: 'Passport',
        title: 'Passport copy',
        fileName: 'passport.pdf',
        uploadedOn: '2026-01-02',
        expiryDate: '',
      }),
    );
    const req = http.expectOne('/api/employee-documents');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      employeeId: 5,
      type: 'Passport',
      title: 'Passport copy',
      fileName: 'passport.pdf',
      uploadedOn: '2026-01-02',
      expiryDate: null,
    });
    req.flush(doc);
    expect((await result).id).toBe(7);
  });

  it('updates with the rowVersion and deletes with it in the body', async () => {
    const { http, docs } = setup();
    const read = firstValueFrom(docs.getById(7));
    http.expectOne('/api/employee-documents/7').flush(doc);
    await read;

    const update = firstValueFrom(docs.update(7, { title: 'Renewed', expiryDate: '2030-01-01' }));
    const put = http.expectOne('/api/employee-documents/7');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      employeeId: 5,
      type: 'Passport',
      title: 'Renewed',
      fileName: 'passport.pdf',
      uploadedOn: '2026-01-02',
      expiryDate: '2030-01-01',
      rowVersion: 'AAAAV1',
    });
    put.flush({ ...doc, title: 'Renewed', rowVersion: 'AAAAV2' });
    await update;

    const removal = firstValueFrom(docs.delete(7));
    const del = http.expectOne('/api/employee-documents/7');
    expect(del.request.method).toBe('DELETE');
    expect(del.request.body).toEqual({ rowVersion: 'AAAAV2' });
    del.flush(null);
    await removal;
  });

  it('reads the salary summary of one employee', async () => {
    const { http, salary } = setup();
    const result = firstValueFrom(salary.getSummary(5));
    const req = http.expectOne('/api/employees/5/salary-summary');
    expect(req.request.method).toBe('GET');
    req.flush({ employeeId: 5, basic: 1000, net: 900 });
    expect((await result).net).toBe(900);
  });

  it('maps a 404 to NotFound with the server message', async () => {
    const { http, salary } = setup();
    const result = firstValueFrom(salary.getSummary(5));
    http
      .expectOne('/api/employees/5/salary-summary')
      .flush(
        { detail: 'Aye Chan has no salary assignment yet.' },
        { status: 404, statusText: 'Not Found' },
      );
    const error = await result.catch((e: AppError) => e);
    expect(error).toMatchObject({
      code: 'NotFound',
      message: 'Aye Chan has no salary assignment yet.',
    });
  });
});
