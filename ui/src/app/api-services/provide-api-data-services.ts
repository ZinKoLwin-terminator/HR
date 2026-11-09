import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import {
  EnvironmentProviders,
  Provider,
  Type,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { AuthService, SERVER_SESSION } from '../core/services/auth.service';
import { AuthDataService } from '../core/services/data/auth-data.service';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../core/services/data/organization-data.services';
import { API_BASE_URL } from './api-base-url';
import { apiAuthInterceptor } from './auth/api-auth.interceptor';
import { AuthHttpService } from './auth/auth-http.service';
import {
  BranchHttpService,
  CompanyHttpService,
  DepartmentHttpService,
  JobGradeHttpService,
  PositionHttpService,
  SectionHttpService,
} from './organization/organization-http.services';

export interface ApiDataOptions {
  /** Master switch. When false nothing is registered and the mocks stay in charge. */
  enabled: boolean;
  /** API root. Defaults to `/api` (dev proxy). */
  baseUrl?: string;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AbstractType<T> = abstract new (...args: any[]) => T;

function bind<T>(contract: AbstractType<T>, implementation: Type<T>): Provider {
  return { provide: contract, useExisting: implementation };
}

/**
 * Binds ONLY the Organization contracts to HTTP. Every other contract stays on its mock
 * (the adapter is per dependency cluster). Register it AFTER `provideMockDataServices()` so
 * these bindings win; `app.config.ts` does that and the switch defaults to off.
 */
export function provideApiDataServices(options: ApiDataOptions): EnvironmentProviders {
  if (!options.enabled) {
    return makeEnvironmentProviders([]);
  }
  return makeEnvironmentProviders([
    provideHttpClient(
      withInterceptors([apiAuthInterceptor]),
      // The API sets the XSRF-TOKEN cookie (GET auth/antiforgery); Angular echoes it as X-XSRF-TOKEN.
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    { provide: API_BASE_URL, useValue: options.baseUrl ?? '/api' },
    bind(CompanyDataService, CompanyHttpService),
    bind(BranchDataService, BranchHttpService),
    bind(DepartmentDataService, DepartmentHttpService),
    bind(SectionDataService, SectionHttpService),
    bind(JobGradeDataService, JobGradeHttpService),
    bind(PositionDataService, PositionHttpService),
    // Real sign-in: cookie session owned by the server, kept in memory only.
    bind(AuthDataService, AuthHttpService),
    { provide: SERVER_SESSION, useValue: true },
    provideAppInitializer(() => inject(AuthService).initialize()),
  ]);
}
