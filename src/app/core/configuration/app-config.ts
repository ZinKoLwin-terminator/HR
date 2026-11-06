import { InjectionToken } from '@angular/core';
import { ExcelLimits } from '../models/excel.models';

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
  /** Limits for Excel import/export (a server would enforce its own as well). */
  readonly excel: ExcelLimits;
}

export const DEFAULT_APP_CONFIG: AppConfig = {
  appName: 'APEX HRMS',
  appShortName: 'HRMS',
  organizationName: 'APEX Group',
  currency: 'MMK',
  locale: 'en-US',
  dateFormat: 'dd MMM yyyy',
  defaultPageSize: 10,
  pageSizeOptions: [5, 10, 25, 50],
  mockLatencyMs: 250,
  weekendDays: [0, 6],
  excel: {
    maxFileBytes: 5 * 1024 * 1024,
    maxRows: 5000,
    maxSheets: 5,
    maxExportRows: 50_000,
  },
};

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG', {
  providedIn: 'root',
  factory: () => DEFAULT_APP_CONFIG,
});
