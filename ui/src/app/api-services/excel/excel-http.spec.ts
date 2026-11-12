import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../core/services/data/excel-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import {
  ExcelCatalogHttpService,
  ExcelExportHttpService,
  ExcelImportHttpService,
  fileNameFromDisposition,
} from './excel-http.service';

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
    catalog: TestBed.inject(ExcelModuleCatalogDataService),
    imports: TestBed.inject(ExcelImportDataService),
    exports: TestBed.inject(ExcelExportDataService),
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

const tick = () => new Promise<void>((resolve) => setTimeout(resolve, 10));

const XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
const summary = (batchId: string) => ({ batchId, moduleCode: 'employees', fileName: 'a.xlsx' });
const inspection = {
  uploadId: 'up1',
  moduleCode: 'employees',
  fileName: 'a.xlsx',
  fileSize: 3,
  sheetName: 'Employees',
  sheetCount: 1,
  rowCount: 1,
  headers: [],
  mapping: {},
  missingRequired: [],
  unknownHeaders: [],
  ambiguousHeaders: [],
  needsMapping: false,
};
const disposition = (name: string) => ({
  'Content-Disposition': `attachment; filename="${name}"; filename*=UTF-8''${name}`,
});

describe('Excel HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mock Excel services only when API mode is on', () => {
    const { catalog, imports, exports } = setup();
    expect(catalog).toBeInstanceOf(ExcelCatalogHttpService);
    expect(imports).toBeInstanceOf(ExcelImportHttpService);
    expect(exports).toBeInstanceOf(ExcelExportHttpService);
  });

  it('getModules returns the modules and ignores the unavailable list', async () => {
    const { http, catalog } = setup();
    const promise = firstValueFrom(catalog.getModules());
    http
      .expectOne((r) => r.method === 'GET' && r.url.endsWith('/excel/modules'))
      .flush({
        modules: [{ code: 'employees', label: 'Employees' }],
        unavailable: [{ code: 'payroll', label: 'Payroll', reason: 'Not available' }],
      });
    const modules = await promise;
    expect(modules.map((m) => m.code)).toEqual(['employees']);
  });

  it('inspect sends multipart with exactly one file part', async () => {
    const { http, imports } = setup();
    const promise = firstValueFrom(
      imports.inspect('employees', new File(['x'], 'a.xlsx', { type: XLSX })),
    );
    const req = http.expectOne(
      (r) => r.method === 'POST' && r.url.endsWith('/imports/employees/inspect'),
    );
    const body = req.request.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect(Array.from(body.keys())).toEqual(['file']);
    expect((body.get('file') as File).name).toBe('a.xlsx');
    req.flush(inspection);
    expect((await promise).uploadId).toBe('up1');
  });

  it('validate and execute send the exact enum text', async () => {
    const { http, imports } = setup();
    const inspected = firstValueFrom(imports.inspect('employees', new File(['x'], 'a.xlsx')));
    http.expectOne((r) => r.url.endsWith('/imports/employees/inspect')).flush(inspection);
    await inspected;

    const validated = firstValueFrom(
      imports.validate({ uploadId: 'up1', strategy: 'InsertOrUpdate', mapping: { A: 'code' } }),
    );
    const validate = http.expectOne((r) => r.url.endsWith('/imports/employees/validate'));
    expect(validate.request.body).toEqual({
      uploadId: 'up1',
      strategy: 'InsertOrUpdate',
      mapping: { A: 'code' },
    });
    validate.flush({ batchId: 'b1', rows: [] });
    await validated;

    const executed = firstValueFrom(imports.execute('b1', 'AllOrNothing'));
    const execute = http.expectOne((r) => r.url.endsWith('/imports/b1/execute'));
    expect(execute.request.body).toEqual({ mode: 'AllOrNothing' });
    execute.flush({ batchId: 'b1', status: 'Completed' });
    await executed;
  });

  it('validate for an unknown upload fails without a request', async () => {
    const { imports } = setup();
    const error = await rejection(
      firstValueFrom(imports.validate({ uploadId: 'gone', strategy: 'InsertOnly', mapping: {} })),
    );
    expect(error.code).toBe('NotFound');
  });

  it('history maps the paged result to a list', async () => {
    const { http, imports } = setup();
    const promise = firstValueFrom(imports.getHistory());
    const req = http.expectOne((r) => r.url.endsWith('/imports/history'));
    expect(req.request.params.get('pageIndex')).toBe('0');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ items: [summary('b2'), summary('b1')], total: 2, pageIndex: 0, pageSize: 100 });
    expect((await promise).map((b) => b.batchId)).toEqual(['b2', 'b1']);
  });

  it('history reads further pages until the total is reached', async () => {
    const { http, imports } = setup();
    const promise = firstValueFrom(imports.getHistory());
    const items = (n: number) => Array.from({ length: n }, (_, i) => summary(`b${i}`));
    http
      .expectOne((r) => r.url.endsWith('/imports/history') && r.params.get('pageIndex') === '0')
      .flush({ items: items(100), total: 101, pageIndex: 0, pageSize: 100 });
    await tick();
    http
      .expectOne((r) => r.url.endsWith('/imports/history') && r.params.get('pageIndex') === '1')
      .flush({ items: items(1), total: 101, pageIndex: 1, pageSize: 100 });
    expect(await promise).toHaveLength(101);
  });

  it('getBatch reads the detail', async () => {
    const { http, imports } = setup();
    const promise = firstValueFrom(imports.getBatch('b1'));
    http
      .expectOne((r) => r.url.endsWith('/imports/b1'))
      .flush({ ...summary('b1'), issues: [{ rowNumber: 2, field: 'Code', message: 'x' }] });
    expect((await promise).issues[0].rowNumber).toBe(2);
  });

  it('template, error report and export request blobs and keep the file name', async () => {
    const { http, imports, exports } = setup();

    const template = firstValueFrom(imports.getTemplate('employees'));
    const t = http.expectOne((r) => r.url.endsWith('/templates/employees'));
    expect(t.request.responseType).toBe('blob');
    t.flush(new Blob(['x']), { headers: disposition('employees-template.xlsx') });
    expect((await template).fileName).toBe('employees-template.xlsx');

    const report = firstValueFrom(imports.getErrorReport('b1'));
    const r = http.expectOne((q) => q.url.endsWith('/imports/b1/error-report'));
    expect(r.request.responseType).toBe('blob');
    r.flush(new Blob(['x']), { headers: disposition('report-b1.xlsx') });
    expect((await report).fileName).toBe('report-b1.xlsx');

    const exported = firstValueFrom(
      exports.export({ moduleCode: 'employees', scope: 'selected', selectedIds: [1, 2] }),
    );
    const e = http.expectOne((q) => q.method === 'POST' && q.url.endsWith('/exports/employees'));
    expect(e.request.responseType).toBe('blob');
    expect(e.request.body).toEqual({ scope: 'Selected', selectedIds: [1, 2] });
    e.flush(new Blob(['x']), { headers: disposition('employees-export.xlsx') });
    const file = await exported;
    expect(file.fileName).toBe('employees-export.xlsx');
    expect(file.blob).toBeInstanceOf(Blob);
  });

  it('falls back to a default file name without Content-Disposition', async () => {
    const { http, exports } = setup();
    const promise = firstValueFrom(exports.export({ moduleCode: 'employees', scope: 'all' }));
    http.expectOne((q) => q.url.endsWith('/exports/employees')).flush(new Blob(['x']));
    expect((await promise).fileName).toBe('employees-export.xlsx');
  });

  it('maps a JSON error on a blob request to a readable AppError', async () => {
    const { http, exports } = setup();
    const promise = rejection(
      firstValueFrom(exports.export({ moduleCode: 'employees', scope: 'all' })),
    );
    http
      .expectOne((q) => q.url.endsWith('/exports/employees'))
      .flush(
        new Blob([JSON.stringify({ code: 'Validation', detail: 'Too many rows to export.' })], {
          type: 'application/problem+json',
        }),
        { status: 400, statusText: 'Bad Request' },
      );
    const error = await promise;
    expect(error.code).toBe('Validation');
    expect(error.message).toBe('Too many rows to export.');
  });

  it('maps a non-JSON error on a blob request by status', async () => {
    const { http, imports } = setup();
    const promise = rejection(firstValueFrom(imports.getErrorReport('b9')));
    http
      .expectOne((q) => q.url.endsWith('/imports/b9/error-report'))
      .flush(new Blob(['nope']), { status: 404, statusText: 'Not Found' });
    expect((await promise).code).toBe('NotFound');
  });

  it('shows the file error of a 400 on inspect', async () => {
    const { http, imports } = setup();
    const promise = rejection(
      firstValueFrom(imports.inspect('employees', new File(['x'], 'a.txt'))),
    );
    http
      .expectOne((q) => q.url.endsWith('/inspect'))
      .flush(
        { title: 'Validation failed', errors: { file: ['Only .xlsx files are accepted.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    expect((await promise).message).toBe('Only .xlsx files are accepted.');
  });

  it('maps a 409 on execute to Conflict', async () => {
    const { http, imports } = setup();
    const promise = rejection(firstValueFrom(imports.execute('b1', 'Partial')));
    http
      .expectOne((q) => q.url.endsWith('/imports/b1/execute'))
      .flush({ detail: 'This import already ran.' }, { status: 409, statusText: 'Conflict' });
    const error = await promise;
    expect(error.code).toBe('Conflict');
    expect(error.message).toBe('This import already ran.');
  });

  it('refuses a module code that is not a plain token', async () => {
    const { imports } = setup();
    const error = await rejection(firstValueFrom(imports.getTemplate('../x')));
    expect(error.code).toBe('NotFound');
  });
});

describe('fileNameFromDisposition', () => {
  it('reads the file name from Content-Disposition forms', () => {
    expect(fileNameFromDisposition('attachment; filename=a.xlsx', 'd')).toBe('a.xlsx');
    expect(fileNameFromDisposition(null, 'd')).toBe('d');
    expect(fileNameFromDisposition("attachment; filename*=UTF-8''a%20b.xlsx", 'd')).toBe(
      'a b.xlsx',
    );
  });
});
