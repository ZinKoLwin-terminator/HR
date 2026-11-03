/**
 * Types shared by the Excel import/export feature.
 *
 * Everything here is plain data: no spreadsheet library is referenced, so the same shapes can be
 * produced by a future API.
 */

export type ExcelColumnType =
  'text' | 'integer' | 'decimal' | 'date' | 'time' | 'email' | 'phone' | 'enum' | 'boolean';

/** One column of a module's import/export sheet, described for business users. */
export interface ExcelColumn {
  /** Stable internal key (never shown to users). */
  key: string;
  /** Header shown in the workbook, e.g. "Employee Code". */
  header: string;
  /** Other headers that are recognised for this column (case and punctuation are ignored). */
  aliases?: readonly string[];
  description: string;
  required: boolean;
  type: ExcelColumnType;
  maxLength?: number;
  min?: number;
  max?: number;
  /** Maximum decimal places for `decimal` columns. */
  decimals?: number;
  /** Allowed values for `enum` columns. */
  allowed?: readonly string[];
  /** Example value shown in the template's field guide. */
  example: string | number | boolean | null;
  width?: number;
  /** Excel number format for exports, e.g. `#,##0.00` or `"$"#,##0.00` (dates default to yyyy-mm-dd). */
  numberFormat?: string;
  /** Personal/financial data: excluded from exports unless explicitly included. */
  sensitive?: boolean;
}

export type ImportStrategy = 'InsertOnly' | 'UpdateOnly' | 'InsertOrUpdate';
export const IMPORT_STRATEGIES: readonly ImportStrategy[] = [
  'InsertOnly',
  'UpdateOnly',
  'InsertOrUpdate',
];
export const IMPORT_STRATEGY_LABELS: Record<ImportStrategy, string> = {
  InsertOnly: 'Insert only',
  UpdateOnly: 'Update only',
  InsertOrUpdate: 'Insert or update',
};

export type ImportMode = 'AllOrNothing' | 'Partial';
export const IMPORT_MODE_LABELS: Record<ImportMode, string> = {
  AllOrNothing: 'All or nothing',
  Partial: 'Import valid rows only',
};

/** What a module offers, as shown to users. */
export interface ExcelModuleInfo {
  code: string;
  label: string;
  /** Name of the data worksheet. */
  sheetName: string;
  description: string;
  columns: readonly ExcelColumn[];
  /** Column keys that identify an existing record (the import matching key). */
  matchKey: readonly string[];
  /** Human description of the matching key, e.g. "Employee Code". */
  matchKeyLabel: string;
  canImport: boolean;
  canExport: boolean;
  /** The export supports a date range (and on which field). */
  exportDateLabel?: string;
}

/** Limits applied to uploads and exports (configurable in `APP_CONFIG.excel`). */
export interface ExcelLimits {
  maxFileBytes: number;
  maxRows: number;
  maxSheets: number;
  maxExportRows: number;
}

export interface ExcelFile {
  fileName: string;
  blob: Blob;
}

// ---------------------------------------------------------------------------------------------
// Import
// ---------------------------------------------------------------------------------------------

/** Maps a header found in the uploaded file to a system column (or null = ignore). */
export type ColumnMapping = Record<string, string | null>;

export interface FileInspection {
  /** Handle for the parsed upload (the API would keep the file in temporary storage). */
  uploadId: string;
  moduleCode: string;
  fileName: string;
  fileSize: number;
  sheetName: string;
  sheetCount: number;
  /** Data rows (blank rows are not counted). */
  rowCount: number;
  headers: string[];
  /** Suggested mapping, header → column key. */
  mapping: ColumnMapping;
  /** Keys of required columns that no header maps to. */
  missingRequired: string[];
  /** Headers that match no column (they will be ignored). */
  unknownHeaders: string[];
  /** Headers that match the same column as another header. */
  ambiguousHeaders: string[];
  /** True when the user has to review the mapping before validation. */
  needsMapping: boolean;
}

export type ImportRowStatus = 'Valid' | 'Invalid' | 'Warning' | 'Duplicate';

export interface ImportIssue {
  /** Column header (or model field) the issue refers to. */
  field: string;
  /** The original value as text (truncated). */
  value: string;
  message: string;
  suggestion?: string;
  severity: 'Error' | 'Warning';
}

export interface ImportPreviewRow {
  /** Row number in the worksheet (1 = header). */
  rowNumber: number;
  /** Values by column key, as text. */
  values: Record<string, string>;
  status: ImportRowStatus;
  /** What import will do with the row. */
  action: 'Insert' | 'Update' | 'None';
  issues: ImportIssue[];
}

export interface ImportPreview {
  batchId: string;
  moduleCode: string;
  fileName: string;
  sheetName: string;
  strategy: ImportStrategy;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  duplicateRows: number;
  warningRows: number;
  rows: ImportPreviewRow[];
}

export interface ImportValidateRequest {
  uploadId: string;
  strategy: ImportStrategy;
  mapping: ColumnMapping;
}

export type ImportBatchStatus = 'Validated' | 'Completed' | 'CompletedWithErrors' | 'Failed';

export interface ImportResult {
  batchId: string;
  status: ImportBatchStatus;
  mode: ImportMode;
  inserted: number;
  updated: number;
  /** Rows not imported because they were invalid or duplicates (partial mode). */
  skipped: number;
  /** Rows that failed while importing. */
  failed: number;
  /** True when an all-or-nothing import was rolled back. */
  rolledBack: boolean;
  message: string;
}

export interface ImportBatchSummary {
  batchId: string;
  moduleCode: string;
  moduleLabel: string;
  fileName: string;
  strategy: ImportStrategy;
  mode: ImportMode | null;
  status: ImportBatchStatus;
  uploadedBy: string;
  uploadedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  successRows: number;
  failedRows: number;
  skippedRows: number;
  errorSummary: string;
}

export interface ImportBatchDetail extends ImportBatchSummary {
  /** Issues recorded for the batch (validation and execution). Original values are truncated. */
  issues: (ImportIssue & { rowNumber: number })[];
}

// ---------------------------------------------------------------------------------------------
// Export
// ---------------------------------------------------------------------------------------------

export type ExportScope = 'filtered' | 'all' | 'selected';

export interface ExportRequest {
  moduleCode: string;
  scope: ExportScope;
  /** Search text and filters of the list the export was started from (scope `filtered`). */
  search?: string;
  filters?: Record<string, unknown>;
  /** Ids of the selected rows (scope `selected`). */
  selectedIds?: number[];
  dateFrom?: string | null;
  dateTo?: string | null;
  /** Column keys to include (all non-sensitive columns when omitted). */
  columnKeys?: string[];
  includeSensitive?: boolean;
}
