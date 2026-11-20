import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { OrganizationLookup } from '../../core/models/organization.models';
import { OrganizationLookupDataService } from '../../core/services/data/organization-data.services';
import { MOCK_DEPARTMENTS } from '../../mock-data/organization/departments.mock';
import { MOCK_JOB_GRADES } from '../../mock-data/organization/job-grades.mock';
import { OrganizationLookupMockService } from '../../mock-services/organization/organization-lookup-mock.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { ApiDataOptions, provideApiDataServices } from '../provide-api-data-services';
import { OrganizationLookupHttpService } from './organization-lookup-http.service';

const lookup: OrganizationLookup = {
  companies: [{ id: 1, code: 'APX', name: 'Apex Demo Co', status: 'Active' }],
  branches: [{ id: 2, companyId: 1, code: 'HQ', name: 'Head office', status: 'Active' }],
  departments: [
    { id: 3, companyId: 1, branchId: 2, code: 'ENG', name: 'Engineering', status: 'Inactive' },
  ],
  sections: [{ id: 4, departmentId: 3, code: 'WEB', name: 'Web', status: 'Active' }],
  positions: [
    {
      id: 5,
      departmentId: 3,
      sectionId: 4,
      jobGradeId: 6,
      code: 'DEV',
      title: 'Developer',
      status: 'Active',
    },
  ],
  jobGrades: [{ id: 6, code: 'G3', name: 'Officer', level: 3, status: 'Active' }],
};

function setup(options: ApiDataOptions = { enabled: true }) {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices(options),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  if (options.enabled) {
    TestBed.inject(HttpTestingController)
      .match((r) => r.url.endsWith('/auth/me'))
      .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  }
  return {
    http: TestBed.inject(HttpTestingController),
    api: TestBed.inject(OrganizationLookupDataService),
  };
}

describe('organization lookup HTTP adapter', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('binds the lookup contract to HTTP only when enabled', () => {
    setup();
    expect(TestBed.inject(OrganizationLookupDataService)).toBeInstanceOf(
      OrganizationLookupHttpService,
    );
  });

  it('keeps the mock when the switch is off', () => {
    setup({ enabled: false });
    expect(TestBed.inject(OrganizationLookupDataService)).toBeInstanceOf(
      OrganizationLookupMockService,
    );
  });

  it('getLookup calls GET /api/organization/lookup and returns the six lists', async () => {
    const { http, api } = setup();
    const result = firstValueFrom(api.getLookup());
    const req = http.expectOne('/api/organization/lookup');
    expect(req.request.method).toBe('GET');
    req.flush(lookup);
    expect(await result).toEqual(lookup);
  });

  it('uses the configured base URL', async () => {
    const { http, api } = setup({ enabled: true, baseUrl: 'https://hr.example/api' });
    const result = firstValueFrom(api.getLookup());
    http.expectOne('https://hr.example/api/organization/lookup').flush(lookup);
    await result;
  });

  it('maps a 403 problem to Forbidden', async () => {
    const { http, api } = setup();
    const result = firstValueFrom(api.getLookup()).catch((e: AppError) => e);
    http
      .expectOne('/api/organization/lookup')
      .flush(
        { detail: 'You do not have permission to do that.', code: 'Forbidden' },
        { status: 403, statusText: 'Forbidden' },
      );
    const error = (await result) as AppError;
    expect(error.code).toBe('Forbidden');
  });
});

describe('organization lookup mock', () => {
  it('returns every row with only the lookup fields', async () => {
    const { api } = setup({ enabled: false });
    const result = await firstValueFrom(api.getLookup());
    expect(result.departments).toHaveLength(MOCK_DEPARTMENTS.length);
    expect(Object.keys(result.departments[0]).sort()).toEqual(
      ['branchId', 'code', 'companyId', 'id', 'name', 'status'].sort(),
    );
    expect(result.jobGrades).toHaveLength(MOCK_JOB_GRADES.length);
    expect(result.jobGrades[0]).not.toHaveProperty('minSalary');
  });
});
