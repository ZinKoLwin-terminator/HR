import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { ExcelFile, ExcelModuleInfo, ExportRequest } from '../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelModuleCatalogDataService,
} from '../../core/services/data/excel-data.services';
import { ActivityMockService } from '../activity/activity-mock.service';
import { mockError } from '../mock-response';
import { deferred } from './deferred';
import { ExcelFileService } from './excel-file.service';
import { EXCEL_MODULES } from './excel-module';

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** The modules that offer Excel import and/or export. */
@Injectable({ providedIn: 'root' })
export class ExcelCatalogMockService implements ExcelModuleCatalogDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly registrations = inject(EXCEL_MODULES, { optional: true }) ?? [];

  getModules(): Observable<ExcelModuleInfo[]> {
    return deferred(() => this.registrations.map((r) => r.info), this.latency);
  }
}

/**
 * Mock export: the data comes from the module's adapter (server-side filtering in a real API), is
 * limited by configuration and written as a formatted workbook in the browser.
 */
@Injectable({ providedIn: 'root' })
export class ExcelExportMockService implements ExcelExportDataService {
  private readonly config = inject(APP_CONFIG);
  private readonly files = inject(ExcelFileService);
  private readonly activity = inject(ActivityMockService);
  private readonly modules = new Map(
    (inject(EXCEL_MODULES, { optional: true }) ?? []).map((m) => [m.info.code, m]),
  );

  export(request: ExportRequest): Observable<ExcelFile> {
    return deferred(
      async () => {
        const registration = this.modules.get(request.moduleCode);
        const adapter = registration?.exportAdapter;
        if (!registration || !registration.info.canExport || !adapter) {
          throw mockError('NotFound', `Export is not available for "${request.moduleCode}".`);
        }
        const { info } = registration;
        if (request.scope === 'selected' && !request.selectedIds?.length) {
          throw mockError('Validation', 'Select at least one record to export.');
        }
        if (request.dateFrom && request.dateTo && request.dateFrom > request.dateTo) {
          throw mockError('Validation', 'The start date must not be after the end date.');
        }

        // Sensitive columns are left out unless explicitly requested.
        const wanted = request.columnKeys?.length ? new Set(request.columnKeys) : null;
        const columns = info.columns.filter(
          (c) =>
            (!wanted || wanted.has(c.key)) && (!c.sensitive || request.includeSensitive === true),
        );
        if (columns.length === 0) {
          throw mockError('Validation', 'Choose at least one column to export.');
        }

        const limit = this.config.excel.maxExportRows;
        const records = adapter.rows(request, limit);
        if (records.length > limit) {
          throw mockError(
            'Validation',
            `The export would contain more than ${limit.toLocaleString('en-US')} rows. Narrow the filters or the date range.`,
          );
        }
        const extra = adapter.extraSheets?.(request, records) ?? [];
        const blob = await this.files.buildExport(
          `${info.label} export`,
          { name: info.sheetName, columns, rows: records },
          extra,
        );

        const now = new Date();
        const stamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}`;
        this.activity.record(
          'system',
          `Exported ${records.length} ${info.label} record(s) to Excel`,
        );
        return { fileName: `${info.code}-export-${stamp}.xlsx`, blob };
      },
      this.config.mockLatencyMs,
      false,
    );
  }
}
