import { Entity } from '../../../core/models/common.models';
import {
  KPI_REPORT_SORT_KEYS,
  KpiAchievementRow,
  KpiEmployeeScorecard,
  KpiPeriodSummary,
  KpiRankingRow,
  KpiReportFilter,
  KpiReportPeriodType,
  KpiReportScorecardRow,
  KpiTargetVsActualRow,
} from '../../../core/models/kpi-insight.models';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { fixed2, scoreText, withUnit } from '../dashboard/kpi-dashboard-views';

/** The six KPI reports. */
export type KpiReportId =
  'summary' | 'employee' | 'department' | 'ranking' | 'kpiAchievement' | 'targetVsActual';

/** A table row: the server's row with the `id` the table tracks it by. */
export type KpiReportRow = Entity & Record<string, unknown>;

/** One entry of the report selector. The Monthly, Quarterly and Annual summaries preset `periodType`. */
export interface KpiReportChoice {
  id: string;
  report: KpiReportId;
  label: string;
  periodType?: KpiReportPeriodType;
}

export const KPI_REPORT_CHOICES: readonly KpiReportChoice[] = [
  { id: 'summary', report: 'summary', label: 'Summary by period' },
  { id: 'summary-monthly', report: 'summary', label: 'Monthly summary', periodType: 'Monthly' },
  {
    id: 'summary-quarterly',
    report: 'summary',
    label: 'Quarterly summary',
    periodType: 'Quarterly',
  },
  { id: 'summary-annual', report: 'summary', label: 'Annual summary', periodType: 'Yearly' },
  { id: 'employee', report: 'employee', label: 'Employee performance history' },
  { id: 'department', report: 'department', label: 'Department performance' },
  { id: 'ranking', report: 'ranking', label: 'Ranking' },
  { id: 'kpi-achievement', report: 'kpiAchievement', label: 'KPI achievement' },
  { id: 'target-vs-actual', report: 'targetVsActual', label: 'Target vs actual' },
];

/** What each report needs from the screen. */
export interface KpiReportDefinition {
  report: KpiReportId;
  title: string;
  description: string;
  /** The report filters the popup offers (the employee and department of their own reports are chosen apart). */
  filters: readonly (keyof KpiReportFilter)[];
  /** Whether `search` (employee code or name) applies. */
  search: boolean;
  /** Whether the optional employee filter is offered. */
  employeeFilter: boolean;
  columns: TableColumn<KpiReportRow>[];
  /** The Excel module of the export (export only), or null when the report has none. */
  exportModule: string | null;
  /** The `sort` fields the API accepts (`KPI_REPORT_SORT_KEYS`); columns outside them do not sort. */
  sortKeys: readonly string[];
}

const PLACEMENT: (keyof KpiReportFilter)[] = [
  'companyId',
  'branchId',
  'departmentId',
  'positionId',
];
const PERIOD: (keyof KpiReportFilter)[] = ['periodId', 'periodType', 'year'];

const str = (row: KpiReportRow, key: string) => row[key] as string | null;
const num = (row: KpiReportRow, key: string) => row[key] as number | null;
const score = (row: KpiReportRow) => scoreText(num(row, 'overallScore'), str(row, 'ratingLabel'));

/** Marks the columns whose key is a sort field of the report. */
function sortable(
  columns: TableColumn<KpiReportRow>[],
  keys: readonly string[],
): TableColumn<KpiReportRow>[] {
  return columns.map((c) => ({ ...c, sortable: keys.includes(c.key) }));
}

const SUMMARY_COLUMNS = sortable(
  [
    { key: 'periodName', header: 'Period', nowrap: true },
    { key: 'periodType', header: 'Type', hideBelow: 'sm' },
    {
      key: 'periodStart',
      header: 'Starts',
      type: 'date',
      nowrap: true,
      hideBelow: 'md',
      value: (r) => str(r, 'startDate'),
    },
    { key: 'periodStatus', header: 'Status', type: 'status', hideBelow: 'sm' },
    { key: 'scorecards', header: 'Scorecards', type: 'number', align: 'end' },
    { key: 'finalized', header: 'Finalized', type: 'number', align: 'end' },
    { key: 'rejected', header: 'Rejected', type: 'number', align: 'end', hideBelow: 'md' },
    {
      key: 'averageScore',
      header: 'Average',
      align: 'end',
      value: (r) => fixed2(num(r, 'averageScore')),
    },
    {
      key: 'minScore',
      header: 'Min',
      align: 'end',
      hideBelow: 'lg',
      value: (r) => fixed2(num(r, 'minScore')),
    },
    {
      key: 'maxScore',
      header: 'Max',
      align: 'end',
      hideBelow: 'lg',
      value: (r) => fixed2(num(r, 'maxScore')),
    },
    {
      key: 'completionRatePct',
      header: 'Completion %',
      align: 'end',
      hideBelow: 'sm',
      value: (r) => fixed2(num(r, 'completionRatePct')),
    },
  ],
  KPI_REPORT_SORT_KEYS.summary,
);

const EMPLOYEE_COLUMNS = sortable(
  [
    { key: 'periodName', header: 'Period', nowrap: true },
    { key: 'periodType', header: 'Type', hideBelow: 'md' },
    {
      key: 'periodStart',
      header: 'Starts',
      type: 'date',
      nowrap: true,
      hideBelow: 'md',
      value: (r) => str(r, 'startDate'),
    },
    { key: 'departmentName', header: 'Department', hideBelow: 'sm' },
    { key: 'status', header: 'Status', type: 'status' },
    { key: 'overallScore', header: 'Score', value: score },
    {
      key: 'kpis',
      header: 'KPIs',
      type: 'number',
      align: 'end',
      hideBelow: 'sm',
      value: (r) => (r['items'] as unknown[]).length,
    },
  ],
  KPI_REPORT_SORT_KEYS.employee,
);

const DEPARTMENT_COLUMNS = sortable(
  [
    { key: 'employeeCode', header: 'Code', nowrap: true },
    { key: 'employeeName', header: 'Employee' },
    { key: 'periodName', header: 'Period', nowrap: true, hideBelow: 'md' },
    { key: 'status', header: 'Status', type: 'status' },
    {
      key: 'overallScore',
      header: 'Score',
      value: (r) => fixed2(num(r, 'overallScore')),
      align: 'end',
    },
    { key: 'ratingLabel', header: 'Rating', hideBelow: 'sm' },
  ],
  KPI_REPORT_SORT_KEYS.department,
);

const RANKING_COLUMNS: TableColumn<KpiReportRow>[] = [
  { key: 'rank', header: 'Rank', type: 'number', align: 'end', width: '64px' },
  { key: 'employeeCode', header: 'Code', nowrap: true },
  { key: 'employeeName', header: 'Employee' },
  { key: 'departmentName', header: 'Department', hideBelow: 'sm' },
  { key: 'periodName', header: 'Period', nowrap: true, hideBelow: 'md' },
  {
    key: 'overallScore',
    header: 'Score',
    align: 'end',
    value: (r) => fixed2(num(r, 'overallScore')),
  },
  { key: 'ratingLabel', header: 'Rating', hideBelow: 'sm' },
];

const ACHIEVEMENT_COLUMNS = sortable(
  [
    { key: 'kpiCode', header: 'KPI code', nowrap: true },
    { key: 'kpiName', header: 'KPI', hideBelow: 'sm' },
    { key: 'categoryName', header: 'Category', hideBelow: 'md' },
    { key: 'assigned', header: 'Assigned', type: 'number', align: 'end' },
    { key: 'counted', header: 'Counted', type: 'number', align: 'end', hideBelow: 'sm' },
    {
      key: 'averageAchievement',
      header: 'Average %',
      align: 'end',
      value: (r) => fixed2(num(r, 'averageAchievementPct')),
    },
    {
      key: 'minAchievementPct',
      header: 'Min %',
      align: 'end',
      hideBelow: 'lg',
      value: (r) => fixed2(num(r, 'minAchievementPct')),
    },
    {
      key: 'maxAchievementPct',
      header: 'Max %',
      align: 'end',
      hideBelow: 'lg',
      value: (r) => fixed2(num(r, 'maxAchievementPct')),
    },
  ],
  KPI_REPORT_SORT_KEYS.kpiAchievement,
);

const TARGET_COLUMNS = sortable(
  [
    { key: 'employeeCode', header: 'Code', nowrap: true },
    { key: 'employeeName', header: 'Employee', hideBelow: 'md' },
    { key: 'periodName', header: 'Period', nowrap: true, hideBelow: 'md' },
    { key: 'kpiCode', header: 'KPI', nowrap: true },
    { key: 'status', header: 'Status', type: 'status', hideBelow: 'sm' },
    {
      key: 'target',
      header: 'Target',
      align: 'end',
      value: (r) => withUnit(num(r, 'target'), str(r, 'unit')),
    },
    {
      key: 'actual',
      header: 'Actual',
      align: 'end',
      value: (r) => withUnit(num(r, 'actualValue'), str(r, 'unit')),
    },
    {
      key: 'achievementPct',
      header: 'Achievement %',
      align: 'end',
      value: (r) => fixed2(num(r, 'achievementPct')),
    },
    {
      key: 'weight',
      header: 'Weight %',
      align: 'end',
      hideBelow: 'lg',
      value: (r) => fixed2(num(r, 'weight')),
    },
    {
      key: 'weightedScore',
      header: 'Weighted',
      align: 'end',
      hideBelow: 'sm',
      value: (r) => fixed2(num(r, 'weightedScore')),
    },
  ],
  KPI_REPORT_SORT_KEYS.targetVsActual,
);

export const KPI_REPORTS: Readonly<Record<KpiReportId, KpiReportDefinition>> = {
  summary: {
    report: 'summary',
    title: 'Summary by period',
    description:
      'One row per period: scorecards, finalized, rejected, the average, lowest and highest score (Finalized only) and the completion rate.',
    filters: [...PLACEMENT, 'categoryId', ...PERIOD, 'status'],
    search: true,
    employeeFilter: true,
    columns: SUMMARY_COLUMNS,
    exportModule: 'kpi-scorecards',
    sortKeys: KPI_REPORT_SORT_KEYS.summary,
  },
  employee: {
    report: 'employee',
    title: 'Employee performance history',
    description:
      'Every scorecard of one employee, newest period first, with its KPIs. The finalized count and average cover all matching scorecards.',
    filters: ['categoryId', ...PERIOD, 'status'],
    search: false,
    employeeFilter: false,
    columns: EMPLOYEE_COLUMNS,
    exportModule: 'kpi-scorecards',
    sortKeys: KPI_REPORT_SORT_KEYS.employee,
  },
  department: {
    report: 'department',
    title: 'Department performance',
    description:
      'The scorecards of one department for one period, counted in the department the employee was in during the period.',
    filters: ['branchId', 'positionId', 'categoryId', 'periodId', 'status'],
    search: true,
    employeeFilter: false,
    columns: DEPARTMENT_COLUMNS,
    exportModule: 'kpi-scorecards',
    sortKeys: KPI_REPORT_SORT_KEYS.department,
  },
  ranking: {
    report: 'ranking',
    title: 'Ranking',
    description:
      'Finalized scorecards by overall score. Equal scores share a rank (1, 1, 3), listed by employee code. Choose a period for a ranking within one period.',
    filters: [...PLACEMENT, 'categoryId', ...PERIOD],
    search: true,
    employeeFilter: true,
    columns: RANKING_COLUMNS,
    exportModule: 'kpi-ranking',
    sortKeys: KPI_REPORT_SORT_KEYS.ranking,
  },
  kpiAchievement: {
    report: 'kpiAchievement',
    title: 'KPI achievement',
    description:
      'One row per KPI template: how often it was assigned, and the average, lowest and highest achievement of Finalized scorecards.',
    filters: [...PLACEMENT, 'categoryId', ...PERIOD, 'status'],
    search: true,
    employeeFilter: true,
    columns: ACHIEVEMENT_COLUMNS,
    exportModule: null,
    sortKeys: KPI_REPORT_SORT_KEYS.kpiAchievement,
  },
  targetVsActual: {
    report: 'targetVsActual',
    title: 'Target vs actual',
    description:
      'Every KPI with its target, actual result, achievement, weight and weighted score, whatever the scorecard status.',
    filters: [...PLACEMENT, 'categoryId', ...PERIOD, 'status'],
    search: true,
    employeeFilter: true,
    columns: TARGET_COLUMNS,
    exportModule: 'kpi-target-vs-actual',
    sortKeys: KPI_REPORT_SORT_KEYS.targetVsActual,
  },
};

// ---- Rows for the table (the id the table tracks) ----------------------------------------------

export const summaryRow = (r: KpiPeriodSummary): KpiReportRow => ({ ...r, id: r.periodId });
export const employeeRow = (r: KpiEmployeeScorecard): KpiReportRow => ({ ...r, id: r.scorecardId });
export const departmentRow = (r: KpiReportScorecardRow): KpiReportRow => ({
  ...r,
  id: r.scorecardId,
});
export const rankingRow = (r: KpiRankingRow): KpiReportRow => ({ ...r, id: r.scorecardId });
export const achievementRow = (r: KpiAchievementRow): KpiReportRow => ({ ...r, id: r.templateId });
export const targetRow = (r: KpiTargetVsActualRow): KpiReportRow => ({ ...r, id: r.itemId });

/** Period type options: the API takes `Yearly` (or `Annual`), shown as Annual. */
export const PERIOD_TYPE_OPTIONS = [
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'Half-Yearly', label: 'Half-Yearly' },
  { value: 'Yearly', label: 'Annual' },
  { value: 'Custom', label: 'Custom' },
];

/** Labels of the report filters (the popup and the export dialog's summary). */
export const FILTER_LABELS: Readonly<Record<keyof KpiReportFilter, string>> = {
  companyId: 'Company',
  branchId: 'Branch',
  departmentId: 'Department',
  positionId: 'Position',
  employeeId: 'Employee',
  categoryId: 'Category',
  periodId: 'Period',
  status: 'Status',
  periodType: 'Period type',
  year: 'Year',
};
