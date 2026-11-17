import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { KpiInsightDataService } from '../../core/services/data/kpi-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { KpiInsightHttpService } from './kpi-insight-http.service';

const KPI = '/api/kpi';
const page = <T>(items: T[]) => ({ items, total: items.length, pageIndex: 0, pageSize: 10 });

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
  return { http, api: TestBed.inject(KpiInsightHttpService) };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

/** The query string of a request, as plain pairs. */
const query = (params: { keys(): string[]; get(k: string): string | null }) =>
  Object.fromEntries(params.keys().map((k) => [k, params.get(k)]));

describe('KPI insight HTTP adapter', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('binds the contract to HTTP in API mode, loading the adapter on first use', async () => {
    const { http } = setup();
    const promise = firstValueFrom(TestBed.inject(KpiInsightDataService).managerDashboard(3));
    // The adapter is a lazy chunk: the request goes out once it has loaded.
    let request = http.match((r) => r.url === `${KPI}/dashboard/manager`);
    for (let i = 0; i < 100 && request.length === 0; i++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      request = http.match((r) => r.url === `${KPI}/dashboard/manager`);
    }
    expect(request).toHaveLength(1);
    expect(request[0].request.params.get('periodId')).toBe('3');
    request[0].flush({ period: null, teamSize: 0, byStatus: [], pendingReviews: 0 });
    expect((await promise).teamSize).toBe(0);
  });

  it('reads the three dashboards, sending only the ids that are set', async () => {
    const { http, api } = setup();
    const employee = firstValueFrom(api.employeeDashboard());
    const own = http.expectOne((r) => r.url === `${KPI}/dashboard/employee`);
    expect(own.request.method).toBe('GET');
    expect(own.request.params.keys()).toEqual([]);
    own.flush({ period: null, items: [], history: [] });
    expect((await employee).period).toBeNull();

    const hr = firstValueFrom(api.hrDashboard(7, 1));
    const req = http.expectOne((r) => r.url === `${KPI}/dashboard/hr`);
    expect(query(req.request.params)).toEqual({ periodId: '7', companyId: '1' });
    req.flush({ attentionThreshold: 60 });
    expect((await hr).attentionThreshold).toBe(60);

    void firstValueFrom(api.hrDashboard(null, null));
    expect(http.expectOne(`${KPI}/dashboard/hr`).request.params.keys()).toEqual([]);
  });

  it('maps a 403 of the HR dashboard to a Forbidden error with the server message', async () => {
    const { http, api } = setup();
    const promise = rejection(firstValueFrom(api.hrDashboard()));
    http
      .expectOne(`${KPI}/dashboard/hr`)
      .flush(
        { title: 'Forbidden', status: 403, detail: 'The HR dashboard needs HR rights.' },
        { status: 403, statusText: 'Forbidden' },
      );
    const error = await promise;
    expect(error.code).toBe('Forbidden');
  });

  it('sends paging, search, the report filters and a whitelisted sort', async () => {
    const { http, api } = setup();
    void firstValueFrom(
      api.summaryReport({
        pageIndex: 1,
        pageSize: 25,
        search: '  EMP0  ',
        sort: { active: 'periodName', direction: 'desc' },
        filters: {
          companyId: 1,
          branchId: 2,
          departmentId: 3,
          positionId: 4,
          employeeId: 46,
          categoryId: 5,
          periodId: 6,
          status: 'Finalized',
          periodType: 'Annual',
          year: 2026,
        },
      }),
    );
    const req = http.expectOne((r) => r.url === `${KPI}/reports/summary`);
    expect(query(req.request.params)).toEqual({
      pageIndex: '1',
      pageSize: '25',
      sort: 'periodName:desc',
      search: 'EMP0',
      companyId: '1',
      branchId: '2',
      departmentId: '3',
      positionId: '4',
      employeeId: '46',
      categoryId: '5',
      periodId: '6',
      status: 'Finalized',
      periodType: 'Annual',
      year: '2026',
    });
    req.flush(page([]));
  });

  it('drops a sort field outside the report’s whitelist, and never sorts the ranking', async () => {
    const { http, api } = setup();
    void firstValueFrom(
      api.targetVsActualReport({
        pageIndex: 0,
        pageSize: 10,
        sort: { active: 'overallScore', direction: 'asc' },
      }),
    );
    expect(
      http.expectOne((r) => r.url === `${KPI}/reports/target-vs-actual`).request.params.has('sort'),
    ).toBe(false);

    void firstValueFrom(
      api.kpiAchievementReport({
        pageIndex: 0,
        pageSize: 10,
        sort: { active: 'averageAchievement', direction: 'desc' },
      }),
    );
    expect(
      http.expectOne((r) => r.url === `${KPI}/reports/kpi-achievement`).request.params.get('sort'),
    ).toBe('averageAchievement:desc');

    void firstValueFrom(
      api.rankingReport({
        pageIndex: 2,
        pageSize: 10,
        sort: { active: 'rank', direction: 'asc' },
        filters: { periodId: 6 },
      }),
    );
    const ranking = http.expectOne((r) => r.url === `${KPI}/reports/ranking`);
    expect(query(ranking.request.params)).toEqual({
      pageIndex: '2',
      pageSize: '10',
      periodId: '6',
    });
  });

  it('puts the employee and department in the route, not in the filters', async () => {
    const { http, api } = setup();
    void firstValueFrom(
      api.employeeReport(46, {
        pageIndex: 0,
        pageSize: 10,
        search: 'ignored',
        sort: { active: 'overallScore', direction: 'desc' },
        filters: { employeeId: 99, status: 'Finalized' },
      }),
    );
    const employee = http.expectOne((r) => r.url === `${KPI}/reports/employee/46`);
    expect(query(employee.request.params)).toEqual({
      pageIndex: '0',
      pageSize: '10',
      sort: 'overallScore:desc',
      status: 'Finalized',
    });
    employee.flush({ employeeId: 46, scorecards: page([]) });

    void firstValueFrom(
      api.departmentReport(3, {
        pageIndex: 0,
        pageSize: 10,
        search: 'Hein',
        filters: { departmentId: 8, periodId: 6 },
      }),
    );
    const department = http.expectOne((r) => r.url === `${KPI}/reports/department/3`);
    expect(query(department.request.params)).toEqual({
      pageIndex: '0',
      pageSize: '10',
      search: 'Hein',
      periodId: '6',
    });
    department.flush({ departmentId: 3, rows: page([]) });
  });

  it('maps a 400 on a filter to a validation error on that field', async () => {
    const { http, api } = setup();
    const promise = rejection(
      firstValueFrom(api.summaryReport({ pageIndex: 0, pageSize: 10, filters: { year: 1800 } })),
    );
    http
      .expectOne((r) => r.url === `${KPI}/reports/summary`)
      .flush(
        {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { year: ['Year must be between 1900 and 9998.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
    const error = await promise;
    expect(error.code).toBe('Validation');
    expect(error.fieldErrors?.['year']).toBe('Year must be between 1900 and 9998.');
  });
});
