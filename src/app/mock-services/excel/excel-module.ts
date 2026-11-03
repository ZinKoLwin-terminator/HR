import { InjectionToken } from '@angular/core';
import { AppError } from '../../core/models/common.models';
import {
  ExcelColumn,
  ExcelModuleInfo,
  ExportRequest,
  ImportIssue,
} from '../../core/models/excel.models';
import { ParsedRow } from './excel-coercion';

/**
 * Per-module pieces plugged into the shared Excel engine. A module contributes a declarative
 * definition (`info`) plus small adapters that connect rows to the module's data service.
 * No spreadsheet code lives in a module.
 */

export interface ImportModelResult {
  /** Model to save; null when the row cannot be turned into one (see `issues`). */
  model: Record<string, unknown> | null;
  issues: ImportIssue[];
}

export interface ExcelImportAdapter {
  /** Id of the existing record identified by the module's matching key, or null. */
  findExisting(row: ParsedRow): number | null;
  /** Resolves references (business codes → ids) and builds the model. */
  toModel(row: ParsedRow, existingId: number | null): ImportModelResult;
  /** Runs the module's own create/update rules without saving. */
  dryRun(model: Record<string, unknown>, existingId: number | null): AppError | null;
  /** Saves the record with the module's own rules (throws an AppError when rejected). */
  apply(model: Record<string, unknown>, existingId: number | null): void;
  /** Captures the data so an all-or-nothing import can be rolled back. */
  snapshot(): () => void;
}

export type ExportValue = string | number | boolean | Date | null;
export type ExportRecord = Record<string, ExportValue>;

export interface ExportSheet {
  name: string;
  columns: readonly ExcelColumn[];
  rows: readonly ExportRecord[];
}

export interface ExcelExportAdapter {
  /**
   * The records to export (already filtered by the request, with every column value keyed by the
   * column key). Return at most `limit + 1` rows so the engine can detect an over-limit export.
   */
  rows(request: ExportRequest, limit: number): ExportRecord[];
  /** Optional extra worksheets (summaries) built from the exported rows. */
  extraSheets?(request: ExportRequest, rows: readonly ExportRecord[]): ExportSheet[];
}

export interface ExcelModuleRegistration {
  info: ExcelModuleInfo;
  importAdapter?: ExcelImportAdapter;
  exportAdapter?: ExcelExportAdapter;
}

/** Multi-provider token: each module registers itself here. */
export const EXCEL_MODULES = new InjectionToken<readonly ExcelModuleRegistration[]>(
  'EXCEL_MODULES',
);
