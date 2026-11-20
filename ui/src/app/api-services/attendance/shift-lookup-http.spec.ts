import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { ShiftRef } from '../../core/models/attendance.models';
import { ShiftLookupDataService } from '../../core/services/data/attendance-data.services';
import { MOCK_SHIFTS } from '../../mock-data/attendance/shifts.mock';
import { ShiftLookupMockService } from '../../mock-services/attendance/shift-lookup-mock.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { ApiDataOptions, provideApiDataServices } from '../provide-api-data-services';
import { ShiftLookupHttpService } from './shift-lookup-http.service';

const shifts: ShiftRef[] = [
  {
    id: 2,
    code: 'NIGHT',
    name: 'Night Shift',
    startTime: '22:00',
    endTime: '06:00',
    workDays: [0, 3, 6],
    isNightShift: true,
    status: 'Inactive',
  },
];

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
    api: TestBed.inject(ShiftLookupDataService),
  };
}

describe('shift lookup HTTP adapter', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('binds the lookup contract to HTTP only when enabled', () => {
    setup();
    expect(TestBed.inject(ShiftLookupDataService)).toBeInstanceOf(ShiftLookupHttpService);
    TestBed.resetTestingModule();
    setup({ enabled: false });
    expect(TestBed.inject(ShiftLookupDataService)).toBeInstanceOf(ShiftLookupMockService);
  });

  it('getLookup calls GET /api/shifts/lookup and returns the array', async () => {
    const { http, api } = setup();
    const result = firstValueFrom(api.getLookup());
    const req = http.expectOne('/api/shifts/lookup');
    expect(req.request.method).toBe('GET');
    req.flush(shifts);
    expect(await result).toEqual(shifts);
  });

  it('maps an error to an AppError', async () => {
    const { http, api } = setup();
    const result = firstValueFrom(api.getLookup()).catch((e: AppError) => e);
    http
      .expectOne('/api/shifts/lookup')
      .flush({ detail: 'Sign in again.' }, { status: 401, statusText: 'Unauthorized' });
    expect(((await result) as AppError).code).toBeTruthy();
  });
});

describe('shift lookup mock', () => {
  it('returns every shift by name with only the lookup fields', async () => {
    const { api } = setup({ enabled: false });
    const result = await firstValueFrom(api.getLookup());
    expect(result).toHaveLength(MOCK_SHIFTS.length);
    expect(Object.keys(result[0]).sort()).toEqual(
      ['code', 'endTime', 'id', 'isNightShift', 'name', 'startTime', 'status', 'workDays'].sort(),
    );
    const names = result.map((s) => s.name);
    expect([...names].sort((a, b) => a.localeCompare(b))).toEqual(names);
  });
});
