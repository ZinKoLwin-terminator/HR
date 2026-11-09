import { Observable } from 'rxjs';
import {
  ExcelFile,
  ExcelModuleInfo,
  ExportRequest,
  FileInspection,
  ImportBatchDetail,
  ImportBatchSummary,
  ImportMode,
  ImportPreview,
  ImportResult,
  ImportValidateRequest,
} from '../../models/excel.models';

/** The modules that can be imported and/or exported. */
export abstract class ExcelModuleCatalogDataService {
  abstract getModules(): Observable<ExcelModuleInfo[]>;
}

/**
 * Excel import. The screens only use this contract: the mock implementation parses the workbook in
 * the browser, an API implementation would upload it and keep the parsed batch on the server.
 *
 * Flow: `getTemplate` → `inspect` (upload) → `validate` (preview) → `execute`.
 */
export abstract class ExcelImportDataService {
  abstract getTemplate(moduleCode: string): Observable<ExcelFile>;
  /** Reads the file and reports headers, the suggested mapping and file-level problems. */
  abstract inspect(moduleCode: string, file: File): Observable<FileInspection>;
  /** Validates every row (no data is changed) and keeps the result as an import batch. */
  abstract validate(request: ImportValidateRequest): Observable<ImportPreview>;
  /** Imports a validated batch. A batch can be executed once. */
  abstract execute(batchId: string, mode: ImportMode): Observable<ImportResult>;
  abstract getHistory(): Observable<ImportBatchSummary[]>;
  abstract getBatch(batchId: string): Observable<ImportBatchDetail>;
  /** Excel report listing every validation or import problem of a batch. */
  abstract getErrorReport(batchId: string): Observable<ExcelFile>;
}

export abstract class ExcelExportDataService {
  /** Server-side filtered export (never limited to the rows visible on screen). */
  abstract export(request: ExportRequest): Observable<ExcelFile>;
}
