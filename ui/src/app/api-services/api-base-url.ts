import { InjectionToken } from '@angular/core';

/**
 * Root URL of the .NET API. `/api` works with the dev proxy (`proxy.conf.json`), which forwards
 * it to the local API. Override it with `provideApiDataServices({ enabled: true, baseUrl })`.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '/api',
});
