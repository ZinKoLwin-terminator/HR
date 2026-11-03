import { InjectionToken } from '@angular/core';

/**
 * Centralized, environment-independent application configuration.
 * Values here are safe to ship to the browser (no secrets).
 */
export interface AppConfig {
  readonly appName: string;
  readonly appShortName: string;
  readonly organizationName: string;
  /** ISO 4217 code used to format all sample payroll figures. */
  readonly currency: string;
  readonly locale: string;
  readonly dateFormat: string;
  readonly defaultPageSize: number;
  readonly pageSizeOptions: readonly number[];
  /** Simulated latency (ms) for mock data operations. */
  readonly mockLatencyMs: number;
  /** Weekdays considered non-working for leave-day calculations (0 = Sunday). */
  readonly weekendDays: readonly number[];
}

export const DEFAULT_APP_CONFIG: AppConfig = {
  appName: 'Apex Horizon HRMS',
  appShortName: 'HRMS',
  organizationName: 'Apex Horizon Group',
  currency: 'USD',
  locale: 'en-US',
  dateFormat: 'dd MMM yyyy',
  defaultPageSize: 10,
  pageSizeOptions: [5, 10, 25, 50],
  mockLatencyMs: 250,
  weekendDays: [0, 6],
};

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG', {
  providedIn: 'root',
  factory: () => DEFAULT_APP_CONFIG,
});
