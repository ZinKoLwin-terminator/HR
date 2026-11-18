import { Injector } from '@angular/core';
import { ExcelColumn, ExcelModuleInfo } from '../../../core/models/excel.models';
import { GrievanceInsightMockService } from '../../grievance/grievance-insight-mock.service';
import { ExcelModuleRegistration } from '../excel-module';
import { outColumn } from './excel-columns';

/**
 * The grievance case export of the API (`grievance-cases`, `grievance-management:View`), mirrored for
 * the demo build. Export only. Keys, headers, types and widths are `GrievanceExcelModule.cs`'s:
 * case number, category, status (code), priority, department ("Not disclosed" for an anonymous
 * case), dates, days, overdue (Yes/No), lead investigator and escalated (Yes/No). The Filtered scope
 * applies the report filters, the export's submission-date range and the case-number search. NEVER
 * the subject, the description, notes, messages or the submitter. Each export is
 * written to the grievance access log. A lazy chunk (registered through `LAZY_EXCEL_MODULES`), so
 * neither the definition nor the mock is in the initial bundle.
 */
export const GRIEVANCE_CASES_EXPORT_COLUMNS: readonly ExcelColumn[] = [
  outColumn('caseNo', 'Case No', 'text', 16),
  outColumn('category', 'Category', 'text', 26),
  outColumn('status', 'Status', 'text', 22),
  outColumn('priority', 'Priority', 'text', 10),
  outColumn('department', 'Department', 'text', 24),
  outColumn('submittedOn', 'Submitted', 'date'),
  outColumn('targetResolutionDate', 'Target', 'date'),
  outColumn('resolvedOn', 'Resolved', 'date'),
  outColumn('resolutionDays', 'Days', 'integer'),
  outColumn('isOverdue', 'Overdue', 'boolean'),
  outColumn('leadInvestigator', 'Lead Investigator', 'text', 26),
  outColumn('isEscalated', 'Escalated', 'boolean'),
];

export const GRIEVANCE_CASES_MODULE_INFO: ExcelModuleInfo = {
  code: 'grievance-cases',
  label: 'Grievance cases',
  sheetName: 'Grievance cases',
  description:
    'Export the grievance cases with the filters of the grievance report: case number, category, status, priority, department ("Not disclosed" for anonymous cases), dates, days to resolve, overdue, lead investigator and escalation. Subjects, descriptions, notes and submitters are never exported. Every export is logged. Import is not available.',
  columns: [],
  exportColumns: GRIEVANCE_CASES_EXPORT_COLUMNS,
  matchKey: [],
  matchKeyLabel: '',
  canImport: false,
  canExport: true,
  exportDateLabel: 'Submission dates',
};

/** The registration, built with the injector of its provider. */
export function grievanceExcelRegistrations(injector: Injector): ExcelModuleRegistration[] {
  return [
    {
      info: GRIEVANCE_CASES_MODULE_INFO,
      exportAdapter: {
        rows: (request, limit) => {
          // The export's own date range filters the submission date, like the report's.
          const filters = {
            ...(request.filters ?? {}),
            ...(request.dateFrom ? { submittedFrom: request.dateFrom } : {}),
            ...(request.dateTo ? { submittedTo: request.dateTo } : {}),
          };
          return injector
            .get(GrievanceInsightMockService)
            .exportRows({ ...request, filters }, limit);
        },
      },
    },
  ];
}
