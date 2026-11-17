import { Injector } from '@angular/core';
import { ExcelColumn, ExcelModuleInfo } from '../../../core/models/excel.models';
import { KpiExportModuleCode, KpiInsightMockService } from '../../kpi/kpi-insight-mock.service';
import { ExcelModuleRegistration } from '../excel-module';
import { outColumn } from './excel-columns';

/**
 * The KPI exports of the API (`kpi-scorecards`, `kpi-ranking`, `kpi-target-vs-actual`), mirrored for
 * the demo build. Export only: scorecards change through the planning and review workflow, never by
 * upload. The rows are the scorecards the signed-in user may see, with the report filters, the
 * search and the export's date range (periods that overlap it); never a comment, a manager rating or
 * an evidence file, computed by the KPI insight mock. This file is a lazy chunk (registered through
 * `LAZY_EXCEL_MODULES`), so neither the definitions nor the mock are in the initial bundle.
 */

function kpiModule(
  code: KpiExportModuleCode,
  label: string,
  description: string,
  exportColumns: readonly ExcelColumn[],
): ExcelModuleInfo {
  return {
    code,
    label,
    sheetName: label,
    description,
    columns: [],
    exportColumns,
    matchKey: [],
    matchKeyLabel: '',
    canImport: false,
    canExport: true,
    exportDateLabel: 'Period dates',
  };
}

const person = [
  outColumn('employeeCode', 'Employee Code', 'text', 14),
  outColumn('employeeName', 'Employee Name', 'text', 26),
  outColumn('department', 'Department', 'text', 24),
  outColumn('period', 'Period', 'text', 20),
];

export const KPI_SCORECARD_MODULE_INFO = kpiModule(
  'kpi-scorecards',
  'KPI scorecards',
  'Export the KPI scorecards you are allowed to see (your own, your team’s, or everyone’s, as on the KPI screens), with the same filters as the KPI reports. Comments and evidence files are never exported. Import is not available.',
  [
    ...person,
    outColumn('status', 'Status', 'text', 14),
    outColumn('itemCount', 'KPIs', 'integer'),
    outColumn('totalWeight', 'Total Weight', 'decimal'),
    outColumn('overallScore', 'Overall Score', 'decimal'),
    outColumn('ratingLabel', 'Rating', 'text', 22),
    outColumn('reviewer', 'Reviewer', 'text', 26),
    outColumn('planFinalizedOn', 'Plan Finalized On', 'date'),
    outColumn('submittedOn', 'Submitted On', 'date'),
    outColumn('submissionDueDate', 'Submission Due', 'date'),
    outColumn('finalizedOn', 'Finalized On', 'date'),
  ],
);

export const KPI_RANKING_MODULE_INFO = kpiModule(
  'kpi-ranking',
  'KPI ranking',
  'Export the ranking of the finalized KPI scorecards you are allowed to see, highest overall score first; equal scores share a rank. Use the period filter for a ranking within one period. Import is not available.',
  [
    outColumn('rank', 'Rank', 'integer'),
    ...person,
    outColumn('overallScore', 'Overall Score', 'decimal'),
    outColumn('ratingLabel', 'Rating', 'text', 22),
  ],
);

export const KPI_TARGET_VS_ACTUAL_MODULE_INFO = kpiModule(
  'kpi-target-vs-actual',
  'KPI target vs actual',
  'Export each KPI of the scorecards you are allowed to see with its target, actual result, achievement, weight and weighted score. Comments and evidence files are never exported. Import is not available.',
  [
    ...person,
    outColumn('status', 'Status', 'text', 14),
    outColumn('kpiCode', 'KPI Code', 'text', 14),
    outColumn('kpiName', 'KPI', 'text', 30),
    outColumn('category', 'Category', 'text', 20),
    outColumn('unit', 'Unit', 'text', 10),
    outColumn('target', 'Target', 'decimal'),
    outColumn('actual', 'Actual', 'decimal'),
    outColumn('achievementPct', 'Achievement %', 'decimal'),
    outColumn('weight', 'Weight', 'decimal'),
    outColumn('weightedScore', 'Weighted Score', 'decimal'),
  ],
);

/** The three registrations, built with the injector of their provider. */
export function kpiExcelRegistrations(injector: Injector): ExcelModuleRegistration[] {
  return [KPI_SCORECARD_MODULE_INFO, KPI_RANKING_MODULE_INFO, KPI_TARGET_VS_ACTUAL_MODULE_INFO].map(
    (info) => ({
      info,
      exportAdapter: {
        rows: (request, limit) =>
          injector
            .get(KpiInsightMockService)
            .exportRows(info.code as KpiExportModuleCode, request, limit),
      },
    }),
  );
}
