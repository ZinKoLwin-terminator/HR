import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import {
  ColumnMapping,
  ExcelFile,
  ExcelModuleInfo,
  FileInspection,
  ImportBatchDetail,
  ImportBatchSummary,
  ImportIssue,
  ImportMode,
  ImportPreview,
  ImportPreviewRow,
  ImportResult,
  ImportRowStatus,
  ImportStrategy,
  ImportValidateRequest,
} from '../../core/models/excel.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { ExcelImportDataService } from '../../core/services/data/excel-data.services';
import { ActivityMockService } from '../activity/activity-mock.service';
import { mockError } from '../mock-response';
import { deferred } from './deferred';
import { ParsedRow, coerceCell, matchHeaders, matchKeyOf, mappingProblems } from './excel-coercion';
import { ExcelFileService, ParsedSheet } from './excel-file.service';
import { EXCEL_MODULES, ExcelModuleRegistration } from './excel-module';

type IssueWithRow = ImportIssue & { rowNumber: number };
type ImportRegistration = ExcelModuleRegistration & {
  importAdapter: NonNullable<ExcelModuleRegistration['importAdapter']>;
};

interface StoredUpload {
  moduleCode: string;
  fileName: string;
  fileSize: number;
  sheet: ParsedSheet;
  createdAt: number;
}

interface StoredBatch {
  summary: ImportBatchSummary;
  preview: ImportPreview;
  /** Typed values per worksheet row (kept server-side only). */
  parsed: Map<number, ParsedRow>;
  issues: IssueWithRow[];
}

const UPLOAD_TTL_MS = 30 * 60 * 1000;
const MAX_UPLOADS = 20;
const MAX_STORED_ISSUES = 2000;
const MAX_VALUE_LENGTH = 60;
const MASKED = '••••';

let sequence = 0;
const newId = (prefix: string): string =>
  `${prefix}-${Date.now().toString(36).toUpperCase()}${(++sequence).toString(36).toUpperCase()}`;

/**
 * Mock implementation of the Excel import contract. All spreadsheet work happens in the browser;
 * an API implementation would send the same requests to the server.
 */
@Injectable({ providedIn: 'root' })
export class ExcelImportMockService implements ExcelImportDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly files = inject(ExcelFileService);
  private readonly activity = inject(ActivityMockService);
  private readonly user = inject(CurrentUserService);
  private readonly modules = new Map(
    (inject(EXCEL_MODULES, { optional: true }) ?? []).map((m) => [m.info.code, m]),
  );

  private readonly uploads = new Map<string, StoredUpload>();
  private readonly batches = new Map<string, StoredBatch>();
  private readonly history = signal<ImportBatchSummary[]>([]);

  private registration(code: string): ImportRegistration {
    const registration = this.modules.get(code);
    if (!registration || !registration.info.canImport || !registration.importAdapter) {
      throw mockError('NotFound', `Import is not available for "${code}".`);
    }
    return registration as ImportRegistration;
  }

  // ---- Template ---------------------------------------------------------------------------

  getTemplate(moduleCode: string): Observable<ExcelFile> {
    return deferred(
      async () => {
        const { info } = this.registration(moduleCode);
        return {
          fileName: `${info.code}-import-template.xlsx`,
          blob: await this.files.buildTemplate(info),
        };
      },
      this.latency,
      false,
    );
  }

  // ---- Upload and inspection --------------------------------------------------------------

  inspect(moduleCode: string, file: File): Observable<FileInspection> {
    return deferred(async () => {
      const { info } = this.registration(moduleCode);
      const sheet = await this.files.read(file, info.sheetName);
      if (sheet.rows.length === 0) {
        throw mockError(
          'Validation',
          `The worksheet "${sheet.name}" has a header row but no data rows.`,
        );
      }
      const match = matchHeaders(sheet.headers.filter(Boolean), info.columns);
      this.pruneUploads();
      const uploadId = newId('UPL');
      this.uploads.set(uploadId, {
        moduleCode,
        fileName: file.name,
        fileSize: file.size,
        sheet,
        createdAt: Date.now(),
      });
      return {
        uploadId,
        moduleCode,
        fileName: file.name,
        fileSize: file.size,
        sheetName: sheet.name,
        sheetCount: sheet.sheetCount,
        rowCount: sheet.rows.length,
        headers: sheet.headers.filter(Boolean),
        mapping: match.mapping,
        missingRequired: match.missingRequired,
        unknownHeaders: match.unknownHeaders,
        ambiguousHeaders: match.ambiguousHeaders,
        needsMapping: match.missingRequired.length > 0 || match.ambiguousHeaders.length > 0,
      } satisfies FileInspection;
    }, this.latency);
  }

  private pruneUploads(): void {
    const now = Date.now();
    for (const [id, upload] of this.uploads) {
      if (now - upload.createdAt > UPLOAD_TTL_MS) this.uploads.delete(id);
    }
    while (this.uploads.size >= MAX_UPLOADS) {
      const oldest = this.uploads.keys().next().value;
      if (oldest === undefined) break;
      this.uploads.delete(oldest);
    }
  }

  // ---- Validation ---------------------------------------------------------------------------

  validate(request: ImportValidateRequest): Observable<ImportPreview> {
    return deferred(() => {
      const upload = this.uploads.get(request.uploadId);
      if (!upload) {
        throw mockError(
          'NotFound',
          'The uploaded file is no longer available. Please upload it again.',
        );
      }
      const registration = this.registration(upload.moduleCode);
      const problems = mappingProblems(request.mapping, registration.info.columns);
      if (problems.length) {
        throw mockError('Validation', problems.join(' '));
      }
      return this.buildBatch(upload, registration, request.strategy, request.mapping);
    }, this.latency);
  }

  private buildBatch(
    upload: StoredUpload,
    registration: ImportRegistration,
    strategy: ImportStrategy,
    mapping: ColumnMapping,
  ): ImportPreview {
    const { info, importAdapter: adapter } = registration;
    const columnIndex = new Map<string, number>();
    upload.sheet.headers.forEach((header, i) => {
      const key = mapping[header];
      if (key) columnIndex.set(key, i);
    });

    const rows: ImportPreviewRow[] = [];
    const parsed = new Map<number, ParsedRow>();
    const issues: IssueWithRow[] = [];
    const firstRowOfKey = new Map<string, number>();

    for (const sheetRow of upload.sheet.rows) {
      const values: ParsedRow = {};
      const texts: Record<string, string> = {};
      const rowIssues: ImportIssue[] = [];

      for (const column of info.columns) {
        const index = columnIndex.get(column.key);
        const cell = index === undefined ? { value: null, formula: false } : sheetRow.cells[index];
        const coerced = coerceCell(column, cell);
        values[column.key] = coerced.value;
        // Show what will be imported (normalised) when the value is valid, otherwise the original text.
        const shown =
          coerced.error || coerced.value === null ? coerced.text : String(coerced.value);
        texts[column.key] = column.sensitive && shown ? MASKED : shown;
        if (coerced.error) {
          rowIssues.push({
            field: column.header,
            value: this.shorten(texts[column.key]),
            message: coerced.error,
            suggestion: coerced.suggestion,
            severity: 'Error',
          });
        }
      }

      let status: ImportRowStatus;
      let existingId: number | null = null;
      const keyColumnsOk = !info.matchKey.some((key) =>
        rowIssues.some((i) => i.field === info.columns.find((c) => c.key === key)?.header),
      );
      const key = matchKeyOf(values, info.matchKey);
      const earlier = key ? firstRowOfKey.get(key) : undefined;
      if (key && earlier === undefined) firstRowOfKey.set(key, sheetRow.rowNumber);

      if (earlier !== undefined) {
        status = 'Duplicate';
        rowIssues.push({
          field: info.matchKeyLabel,
          value: this.shorten(info.matchKey.map((k) => texts[k]).join(' / ')),
          message: `Duplicate of row ${earlier} in this file (same ${info.matchKeyLabel}).`,
          suggestion: 'Remove one of the duplicate rows.',
          severity: 'Error',
        });
      } else if (rowIssues.length > 0) {
        status = 'Invalid';
      } else {
        existingId = keyColumnsOk ? adapter.findExisting(values) : null;
        const blocked = this.strategyIssue(strategy, existingId, info, texts);
        if (blocked) {
          status = blocked.status;
          rowIssues.push(blocked.issue);
        } else {
          const built = adapter.toModel(values, existingId);
          rowIssues.push(...built.issues);
          let rejected = built.model === null || built.issues.some((i) => i.severity === 'Error');
          if (!rejected && built.model) {
            const error = adapter.dryRun(built.model, existingId);
            if (error) {
              rejected = true;
              rowIssues.push(...this.issuesFromError(error, info, texts));
            }
          }
          status = rejected
            ? 'Invalid'
            : rowIssues.some((i) => i.severity === 'Warning')
              ? 'Warning'
              : 'Valid';
        }
      }

      const importable = status === 'Valid' || status === 'Warning';
      rows.push({
        rowNumber: sheetRow.rowNumber,
        values: texts,
        status,
        action: importable ? (existingId === null ? 'Insert' : 'Update') : 'None',
        issues: rowIssues,
      });
      parsed.set(sheetRow.rowNumber, values);
      for (const issue of rowIssues) {
        if (issues.length < MAX_STORED_ISSUES)
          issues.push({ ...issue, rowNumber: sheetRow.rowNumber });
      }
    }

    const count = (s: ImportRowStatus) => rows.filter((r) => r.status === s).length;
    const batchId = newId('IMP');
    const preview: ImportPreview = {
      batchId,
      moduleCode: info.code,
      fileName: upload.fileName,
      sheetName: upload.sheet.name,
      strategy,
      totalRows: rows.length,
      validRows: count('Valid') + count('Warning'),
      invalidRows: count('Invalid'),
      duplicateRows: count('Duplicate'),
      warningRows: count('Warning'),
      rows,
    };
    const summary: ImportBatchSummary = {
      batchId,
      moduleCode: info.code,
      moduleLabel: info.label,
      fileName: upload.fileName,
      strategy,
      mode: null,
      status: 'Validated',
      uploadedBy: this.user.current().displayName,
      uploadedAt: new Date().toISOString(),
      startedAt: null,
      completedAt: null,
      totalRows: rows.length,
      validRows: preview.validRows,
      invalidRows: preview.invalidRows + preview.duplicateRows,
      successRows: 0,
      failedRows: 0,
      skippedRows: 0,
      errorSummary: '',
    };
    this.batches.set(batchId, { summary, preview, parsed, issues });
    this.history.update((list) => [summary, ...list]);
    return preview;
  }

  private strategyIssue(
    strategy: ImportStrategy,
    existingId: number | null,
    info: ExcelModuleInfo,
    texts: Record<string, string>,
  ): { status: ImportRowStatus; issue: ImportIssue } | null {
    const keyText = this.shorten(info.matchKey.map((k) => texts[k]).join(' / '));
    if (strategy === 'InsertOnly' && existingId !== null) {
      return {
        status: 'Duplicate',
        issue: {
          field: info.matchKeyLabel,
          value: keyText,
          message: `A record with this ${info.matchKeyLabel} already exists.`,
          suggestion: 'Use "Insert or update" to update existing records.',
          severity: 'Error',
        },
      };
    }
    if (strategy === 'UpdateOnly' && existingId === null) {
      return {
        status: 'Invalid',
        issue: {
          field: info.matchKeyLabel,
          value: keyText,
          message: `No existing record has this ${info.matchKeyLabel}, so it cannot be updated.`,
          suggestion: 'Use "Insert or update" to create new records.',
          severity: 'Error',
        },
      };
    }
    return null;
  }

  private issuesFromError(
    error: AppError,
    info: ExcelModuleInfo,
    texts: Record<string, string>,
  ): ImportIssue[] {
    const byKey = (key: string) => info.columns.find((c) => c.key === key);
    const fields = Object.entries(error.fieldErrors ?? {});
    if (fields.length === 0) {
      return [
        {
          field: info.matchKeyLabel,
          value: this.shorten(info.matchKey.map((k) => texts[k]).join(' / ')),
          message: error.message,
          severity: 'Error',
        },
      ];
    }
    return fields.map(([key, message]) => {
      const column = byKey(key);
      return {
        field: column?.header ?? key,
        value: column ? this.shorten(texts[column.key] ?? '') : '',
        message,
        severity: 'Error' as const,
      };
    });
  }

  private shorten(value: string): string {
    return value.length > MAX_VALUE_LENGTH ? `${value.slice(0, MAX_VALUE_LENGTH)}…` : value;
  }

  // ---- Execution ----------------------------------------------------------------------------

  execute(batchId: string, mode: ImportMode): Observable<ImportResult> {
    return deferred(() => this.run(batchId, mode), this.latency);
  }

  private run(batchId: string, mode: ImportMode): ImportResult {
    const batch = this.batches.get(batchId);
    if (!batch) {
      throw mockError('NotFound', 'This import was not found.');
    }
    if (batch.summary.status !== 'Validated') {
      throw mockError(
        'Conflict',
        'This import has already been processed. Upload the file again to import it again.',
      );
    }
    const { importAdapter: adapter, info } = this.registration(batch.summary.moduleCode);
    const rows = batch.preview.rows;
    const importable = rows.filter((r) => r.status === 'Valid' || r.status === 'Warning');
    const blocked = rows.length - importable.length;

    if (mode === 'AllOrNothing' && blocked > 0) {
      throw mockError(
        'Conflict',
        `${blocked} row(s) have problems, so an all-or-nothing import cannot run. Fix the file, or import only the valid rows.`,
      );
    }
    if (importable.length === 0) {
      throw mockError('Conflict', 'There are no valid rows to import.');
    }

    batch.summary.startedAt = new Date().toISOString();
    const restore = mode === 'AllOrNothing' ? adapter.snapshot() : null;
    let inserted = 0;
    let updated = 0;
    const failures: IssueWithRow[] = [];

    for (const row of importable) {
      const values = batch.parsed.get(row.rowNumber) as ParsedRow;
      const message = this.applyRow(adapter, batch.preview.strategy, values);
      if (message === null) {
        if (row.action === 'Update') updated++;
        else inserted++;
      } else {
        failures.push({
          rowNumber: row.rowNumber,
          field: info.matchKeyLabel,
          value: '',
          message,
          severity: 'Error',
        });
      }
    }

    const rolledBack = mode === 'AllOrNothing' && failures.length > 0;
    if (rolledBack) {
      restore?.();
      inserted = 0;
      updated = 0;
    }
    const failed = failures.length;
    const skipped = mode === 'Partial' ? blocked : 0;
    const status = rolledBack
      ? 'Failed'
      : failed > 0 || skipped > 0
        ? 'CompletedWithErrors'
        : 'Completed';
    const message = rolledBack
      ? `Nothing was imported: ${failed} row(s) failed, so the whole import was rolled back.`
      : `${inserted} inserted, ${updated} updated${skipped ? `, ${skipped} skipped` : ''}${failed ? `, ${failed} failed` : ''}.`;

    batch.issues.push(...failures);
    Object.assign(batch.summary, {
      mode,
      status,
      completedAt: new Date().toISOString(),
      successRows: inserted + updated,
      failedRows: failed,
      skippedRows: skipped,
      errorSummary: failed || skipped ? message : '',
    });
    this.history.update((list) =>
      list.map((s) => (s.batchId === batchId ? { ...batch.summary } : s)),
    );
    this.activity.record(
      'system',
      `Excel import of ${info.label} (${batch.summary.fileName}): ${message}`,
    );
    return { batchId, status, mode, inserted, updated, skipped, failed, rolledBack, message };
  }

  /** Saves one row; returns an error message, or null on success. */
  private applyRow(
    adapter: NonNullable<ExcelModuleRegistration['importAdapter']>,
    strategy: ImportStrategy,
    values: ParsedRow,
  ): string | null {
    try {
      const existingId = adapter.findExisting(values);
      if (strategy === 'InsertOnly' && existingId !== null)
        return 'The record already exists (it was created after validation).';
      if (strategy === 'UpdateOnly' && existingId === null) return 'The record no longer exists.';
      const built = adapter.toModel(values, existingId);
      if (!built.model) return built.issues[0]?.message ?? 'The row could not be converted.';
      adapter.apply(built.model, existingId);
      return null;
    } catch (error) {
      return (error as AppError)?.message ?? 'The row could not be saved.';
    }
  }

  // ---- History and reports ------------------------------------------------------------------

  getHistory(): Observable<ImportBatchSummary[]> {
    return deferred(() => this.history(), this.latency);
  }

  getBatch(batchId: string): Observable<ImportBatchDetail> {
    return deferred(() => {
      const batch = this.batches.get(batchId);
      if (!batch) throw mockError('NotFound', 'This import was not found.');
      return { ...batch.summary, issues: batch.issues };
    }, this.latency);
  }

  getErrorReport(batchId: string): Observable<ExcelFile> {
    return deferred(
      async () => {
        const batch = this.batches.get(batchId);
        if (!batch) throw mockError('NotFound', 'This import was not found.');
        const blob = await this.files.buildErrorReport(batch.summary.moduleLabel, batch.issues);
        return { fileName: `${batch.summary.moduleCode}-import-report-${batchId}.xlsx`, blob };
      },
      this.latency,
      false,
    );
  }
}
