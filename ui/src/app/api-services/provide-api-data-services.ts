import { provideHttpClient } from '@angular/common/http';
import { EnvironmentProviders, Provider, Type, makeEnvironmentProviders } from '@angular/core';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../core/services/data/organization-data.services';
import { API_BASE_URL } from './api-base-url';
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
    provideHttpClient(),
    { provide: API_BASE_URL, useValue: options.baseUrl ?? '/api' },
    bind(CompanyDataService, CompanyHttpService),
    bind(BranchDataService, BranchHttpService),
    bind(DepartmentDataService, DepartmentHttpService),
    bind(SectionDataService, SectionHttpService),
    bind(JobGradeDataService, JobGradeHttpService),
    bind(PositionDataService, PositionHttpService),
  ]);
}
