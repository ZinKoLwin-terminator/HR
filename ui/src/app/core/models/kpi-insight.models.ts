import { EvaluationPeriodStatus, KpiFrequency, ScorecardStatus } from './kpi.models';
import { PagedResult } from './common.models';

/**
 * KPI dashboards and reports, typed to the API contract (`api/README.md`, "HTTP contract (KPI
 * dashboards, reports and export)"). Read only: every figure is computed by the server over the
 * scorecards the caller may see, and the screens only display it. Dates are `yyyy-MM-dd`, numbers
 * are plain numbers. Only the employee code and name identify a person; no comment appears except
 * the employee's own manager feedback on their dashboard.
 *
 * Metric definitions (one each, the API's `KpiFigures`; the demo mock repeats them):
 *  - average score: the mean overall score of the Finalized scorecards only, 2 decimals rounded
 *    half away from zero; null (never 0) when there is no Finalized scorecard;
 *  - completion rate: Finalized ÷ (every scorecard past Planning) × 100, 2 decimals; 0 when nothing
 *    is past Planning;
 *  - ranking: Finalized scorecards with a score, standard competition ranking (90, 90, 80 is
 *    1, 1, 3), ties listed by employee code;
 *  - attention threshold: the minimum score of the lowest Active rating band whose minimum is at
 *    least 60, else 60; a Finalized scorecard below it needs attention;
 *  - default period: the most recent Active period among the candidates, else the most recent one.
 */

/** The period a dashboard or report is about. */
export interface KpiPeriodRef {
  id: number;
  name: string;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  status: EvaluationPeriodStatus;
  submissionDueDate: string | null;
}

/** How many scorecards have one status. Dashboards list all nine, in workflow order, zeros included. */
export interface KpiStatusCount {
  status: ScorecardStatus;
  count: number;
}

/** One employee's scorecard in a comparison, a top list or the attention list. */
export interface KpiEmployeeScore {
  scorecardId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
}

// ---- Employee dashboard ------------------------------------------------------------------------

export interface KpiCurrentScorecard {
  id: number;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
  reviewerEmployeeId: number | null;
  reviewerName: string | null;
  submittedOn: string | null;
  finalizedOn: string | null;
}

/** One KPI of the current scorecard, by sort order. */
export interface KpiDashboardItem {
  id: number;
  kpiCode: string;
  kpiName: string;
  unit: string | null;
  target: number;
  actualValue: number | null;
  achievementPct: number | null;
  weight: number;
  weightedScore: number | null;
}

/**
 * `isPending`: the employee can still act on the scorecard: Draft or Returned and its period
 * Active, or Returned and its period Completed (the period rule of results and submit).
 * `isOverdue`: pending and today (business time zone) is after `dueDate`.
 */
export interface KpiPendingSubmission {
  isPending: boolean;
  dueDate: string | null;
  isOverdue: boolean;
}

/** The manager's overall rating (1 to 5) and comment, as stored on the scorecard. */
export interface KpiManagerFeedback {
  managerRating: number | null;
  managerComment: string | null;
}

/** A scorecard of an earlier period (newest first, at most 12). */
export interface KpiHistoryEntry {
  scorecardId: number;
  periodId: number;
  periodName: string;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
}

/**
 * `GET /api/kpi/dashboard/employee`: the caller's own scorecard of the period (Planning is never
 * shown) and their history. Every part is empty when the caller has no linked employee or no
 * scorecard.
 */
export interface KpiEmployeeDashboard {
  period: KpiPeriodRef | null;
  scorecard: KpiCurrentScorecard | null;
  items: KpiDashboardItem[];
  pending: KpiPendingSubmission;
  feedback: KpiManagerFeedback | null;
  history: KpiHistoryEntry[];
}

// ---- Manager dashboard -------------------------------------------------------------------------

/**
 * `GET /api/kpi/dashboard/manager`: the scorecards of the period of the caller's direct reports and
 * those naming the caller as reviewer, never the caller's own. `employees`: by employee code (then
 * scorecard id), at most 200 rows (`KPI_MANAGER_EMPLOYEE_LIMIT`); `employeeCount` says how many
 * there are in all.
 */
export interface KpiManagerDashboard {
  period: KpiPeriodRef | null;
  teamSize: number;
  byStatus: KpiStatusCount[];
  /** Submitted, Resubmitted or Under Review, naming the caller as reviewer. */
  pendingReviews: number;
  /** Approved plus Finalized. */
  approvedOrFinalized: number;
  averageScore: number | null;
  employees: KpiEmployeeScore[];
  /** Every team scorecard of the period, listed or not (the same number as `teamSize`). */
  employeeCount: number;
  /** True when `employees` stops short of `employeeCount` (the list is capped). */
  employeesTruncated: boolean;
}

// ---- HR dashboard ------------------------------------------------------------------------------

/** Per department copied onto the scorecards. `averageScore` counts Finalized scorecards only. */
export interface KpiDepartmentPerformance {
  departmentId: number;
  departmentName: string;
  scorecards: number;
  finalized: number;
  averageScore: number | null;
}

/** Finalized scorecards per rating label; `ratingLabel` is null for a score below every band. */
export interface KpiRatingCount {
  ratingLabel: string | null;
  count: number;
}

/** `GET /api/kpi/dashboard/hr` (HR tier only; anyone else 403): every scorecard of the period. */
export interface KpiHrDashboard {
  period: KpiPeriodRef | null;
  companyId: number | null;
  /** Scorecards with at least one KPI (one per employee). */
  employeesWithKpis: number;
  /** The KPIs (scorecard items) of the period. */
  totalAssignments: number;
  /** Finalized. */
  completedEvaluations: number;
  /** Draft or Returned. */
  pendingSubmissions: number;
  /** Submitted, Resubmitted, Under Review or Approved. */
  pendingApprovals: number;
  averageScore: number | null;
  completionRatePct: number;
  byStatus: KpiStatusCount[];
  /** By department name. */
  departments: KpiDepartmentPerformance[];
  /** Finalized, the 5 highest scores, ties by employee code. */
  topPerformers: KpiEmployeeScore[];
  attentionThreshold: number;
  /** Every Finalized scorecard below the threshold (the list holds at most 50). */
  requiringAttentionCount: number;
  /** Lowest score first. */
  requiringAttention: KpiEmployeeScore[];
  /** By the band's minimum score, highest first; labels of no current band after them. */
  ratingDistribution: KpiRatingCount[];
}

// ---- Reports -----------------------------------------------------------------------------------

/** `Annual` is another name of `Yearly` for the period type filter. */
export type KpiReportPeriodType = KpiFrequency | 'Annual';

/**
 * Filters shared by the reports and the KPI Excel exports (combined with AND). The placement
 * filters are the ones copied onto the scorecard; `year` is the year the period starts in;
 * `categoryId` keeps scorecards with a KPI of the category (and, in the item reports, only those KPIs).
 */
export interface KpiReportFilter {
  companyId: number;
  branchId: number;
  departmentId: number;
  positionId: number;
  employeeId: number;
  categoryId: number;
  periodId: number;
  status: ScorecardStatus;
  periodType: KpiReportPeriodType;
  year: number;
}

/** The query-string names of {@link KpiReportFilter}, in the order the API lists them. */
export const KPI_REPORT_FILTER_KEYS: readonly (keyof KpiReportFilter)[] = [
  'companyId',
  'branchId',
  'departmentId',
  'positionId',
  'employeeId',
  'categoryId',
  'periodId',
  'status',
  'periodType',
  'year',
];

/** One period of the summary report. `scorecards` counts Planning too; the scores count Finalized only. */
export interface KpiPeriodSummary {
  periodId: number;
  periodName: string;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  periodStatus: EvaluationPeriodStatus;
  scorecards: number;
  inPlanning: number;
  finalized: number;
  rejected: number;
  averageScore: number | null;
  minScore: number | null;
  maxScore: number | null;
  completionRatePct: number;
}

/** One KPI of a scorecard in a report. Never a comment. */
export interface KpiReportItem {
  id: number;
  kpiCode: string;
  kpiName: string;
  categoryName: string;
  unit: string | null;
  target: number;
  actualValue: number | null;
  achievementPct: number | null;
  weight: number;
  weightedScore: number | null;
}

/** One scorecard of the employee report, with its KPIs. */
export interface KpiEmployeeScorecard {
  scorecardId: number;
  periodId: number;
  periodName: string;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  departmentId: number;
  departmentName: string;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
  items: KpiReportItem[];
}

/** `finalized` and `averageScore` cover every matching scorecard, not only the page. */
export interface KpiEmployeeReport {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  finalized: number;
  averageScore: number | null;
  scorecards: PagedResult<KpiEmployeeScorecard>;
}

/** A scorecard row of the department report. */
export interface KpiReportScorecardRow {
  scorecardId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  periodId: number;
  periodName: string;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
}

/**
 * The scorecards whose copied department is this one (an employee who moved later still counts
 * here), for one period. The totals cover every matching scorecard, not only the page.
 */
export interface KpiDepartmentReport {
  departmentId: number;
  departmentName: string;
  period: KpiPeriodRef | null;
  scorecards: number;
  finalized: number;
  averageScore: number | null;
  rows: PagedResult<KpiReportScorecardRow>;
}

/** Finalized scorecards only. Equal scores share a rank (1, 1, 3). */
export interface KpiRankingRow {
  rank: number;
  scorecardId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  periodId: number;
  periodName: string;
  overallScore: number;
  ratingLabel: string | null;
}

/** `assigned`: every assigned KPI; `counted` and the figures: those of Finalized scorecards with an achievement. */
export interface KpiAchievementRow {
  templateId: number;
  kpiCode: string;
  kpiName: string;
  categoryId: number;
  categoryName: string;
  assigned: number;
  counted: number;
  averageAchievementPct: number | null;
  minAchievementPct: number | null;
  maxAchievementPct: number | null;
}

/** One KPI of a scorecard, whatever the scorecard's status (shown). */
export interface KpiTargetVsActualRow {
  itemId: number;
  scorecardId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  periodId: number;
  periodName: string;
  status: ScorecardStatus;
  kpiCode: string;
  kpiName: string;
  categoryName: string;
  unit: string | null;
  target: number;
  actualValue: number | null;
  achievementPct: number | null;
  weight: number;
  weightedScore: number | null;
}

/**
 * The `sort` fields each report accepts (anything else is a 400 on `sort`). The ranking only
 * accepts `rank:asc`, which is also its fixed order, so nothing is sent for it.
 */
export const KPI_REPORT_SORT_KEYS = {
  summary: ['periodStart', 'periodName', 'periodType'],
  employee: ['periodStart', 'periodName', 'overallScore', 'status'],
  department: ['employeeCode', 'employeeName', 'overallScore', 'ratingLabel', 'status'],
  ranking: [],
  kpiAchievement: [
    'kpiCode',
    'kpiName',
    'categoryName',
    'assigned',
    'counted',
    'averageAchievement',
  ],
  targetVsActual: [
    'periodStart',
    'periodName',
    'employeeCode',
    'employeeName',
    'departmentName',
    'kpiCode',
    'kpiName',
    'categoryName',
    'achievementPct',
    'weightedScore',
    'status',
  ],
} as const satisfies Record<string, readonly string[]>;
