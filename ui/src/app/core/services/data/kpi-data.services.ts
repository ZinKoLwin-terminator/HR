import { Observable } from 'rxjs';
import { PagedResult, PageRequest } from '../../models/common.models';
import {
  AssignKpiRequest,
  AssignKpiResult,
  BulkAssignKpiRequest,
  BulkAssignmentResult,
  EvaluationPeriod,
  EvaluationPeriodFilter,
  EvaluationPeriodOption,
  EvidenceDownload,
  EvidenceRemovalResult,
  EvidenceUploadResult,
  KpiCategory,
  KpiCategoryFilter,
  KpiCategoryOption,
  KpiTemplate,
  KpiTemplateFilter,
  RatingBand,
  RatingBandFilter,
  ReviewDecision,
  SaveResultsRequest,
  SaveReviewRequest,
  Scorecard,
  ScorecardDetail,
  ScorecardFilter,
  ScorecardItem,
  ScorecardItemUpdate,
} from '../../models/kpi.models';
import {
  KpiAchievementRow,
  KpiDepartmentReport,
  KpiEmployeeDashboard,
  KpiEmployeeReport,
  KpiHrDashboard,
  KpiManagerDashboard,
  KpiPeriodSummary,
  KpiRankingRow,
  KpiReportFilter,
  KpiTargetVsActualRow,
} from '../../models/kpi-insight.models';
import { CrudDataService } from './crud-data.service';

// Contracts of the KPI / performance module. Screens inject these abstract classes only.
// Errors are emitted as `AppError`. Activating or deactivating a category, template or rating
// band is `update(id, { status })`, as for every other CRUD contract.

export abstract class KpiCategoryDataService extends CrudDataService<
  KpiCategory,
  KpiCategory,
  KpiCategoryFilter
> {
  /**
   * The category picker for screens outside setup (KPI reports): every category, inactive ones
   * included, by name. Needs kpi:View only, unlike the CRUD reads (kpi-setup:View).
   */
  abstract options(): Observable<KpiCategoryOption[]>;
}

export abstract class KpiTemplateDataService extends CrudDataService<
  KpiTemplate,
  KpiTemplate,
  KpiTemplateFilter
> {}

export abstract class RatingBandDataService extends CrudDataService<
  RatingBand,
  RatingBand,
  RatingBandFilter
> {}

/**
 * Evaluation periods. `update` never changes the status (a `status` in the changes is ignored);
 * the status moves only through the three commands: Draft to Active to Completed to Closed.
 * `rowVersion` is optional on the commands: when left out, the last one seen for the period is used.
 */
export abstract class EvaluationPeriodDataService extends CrudDataService<
  EvaluationPeriod,
  EvaluationPeriod,
  EvaluationPeriodFilter
> {
  /** Draft to Active. */
  abstract activate(id: number, rowVersion?: string): Observable<EvaluationPeriod>;
  /** Active to Completed. */
  abstract complete(id: number, rowVersion?: string): Observable<EvaluationPeriod>;
  /**
   * Completed to Closed (final). A Conflict while any scorecard of the period is Submitted,
   * Resubmitted, Under Review, Approved or Returned (the message gives the count).
   */
  abstract close(id: number, rowVersion?: string): Observable<EvaluationPeriod>;
  /** The period picker: periods that are not Draft, newest start first. No setup permission needed. */
  abstract options(companyId?: number | null): Observable<EvaluationPeriodOption[]>;
}

/**
 * Scorecards: assignment and plan (HR), results and submission (the employee), review (reviewer or HR)
 * and evidence. One `rowVersion` guards the whole scorecard: every write takes the version you last
 * received and every answer carries the new one, so use the answer for the next write.
 */
export abstract class ScorecardDataService {
  // ---- Reads (scoped to the caller's rows) ----
  abstract query(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>>;
  abstract getById(id: number): Observable<ScorecardDetail>;
  /** The caller's own scorecards except Planning, newest period first. */
  abstract myScorecards(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>>;
  /**
   * Scorecards waiting for the caller's review, oldest submission first; only those whose period
   * is Active or Completed.
   */
  abstract pendingReviews(
    request: PageRequest<ScorecardFilter>,
  ): Observable<PagedResult<Scorecard>>;

  // ---- Assignment and plan (HR) ----
  abstract assign(request: AssignKpiRequest): Observable<AssignKpiResult>;
  abstract bulkAssign(request: BulkAssignKpiRequest): Observable<BulkAssignmentResult>;
  abstract updateItem(itemId: number, changes: ScorecardItemUpdate): Observable<ScorecardItem>;
  abstract deleteItem(itemId: number, rowVersion: string): Observable<void>;
  abstract deleteScorecard(id: number, rowVersion: string): Observable<void>;
  /** Planning to Draft: the weights must add up to exactly 100%. */
  abstract finalizePlan(id: number, rowVersion: string): Observable<ScorecardDetail>;

  // ---- Employee: period Active, or Completed for a Returned scorecard (results, submit, evidence) ----
  abstract saveResults(id: number, request: SaveResultsRequest): Observable<ScorecardDetail>;
  /** Draft to Submitted, Returned to Resubmitted. */
  abstract submit(id: number, rowVersion: string): Observable<ScorecardDetail>;

  // ---- Review ----
  abstract startReview(id: number, rowVersion: string): Observable<ScorecardDetail>;
  abstract saveReview(id: number, request: SaveReviewRequest): Observable<ScorecardDetail>;
  abstract approve(id: number, decision: ReviewDecision): Observable<ScorecardDetail>;
  /** Sends the scorecard back to the employee. The comment is required. (`return` is a reserved word.) */
  abstract returnForRevision(id: number, decision: ReviewDecision): Observable<ScorecardDetail>;
  /** Final. The comment is required. */
  abstract reject(id: number, decision: ReviewDecision): Observable<ScorecardDetail>;
  /** HR's final review: Approved to Finalized. */
  abstract finalize(id: number, decision: ReviewDecision): Observable<ScorecardDetail>;

  // ---- Evidence ----
  /** Adds a file to a KPI of the caller's own scorecard. `rowVersion` is the scorecard's. */
  abstract uploadEvidence(
    itemId: number,
    rowVersion: string,
    file: File,
  ): Observable<EvidenceUploadResult>;
  abstract downloadEvidence(attachmentId: number): Observable<EvidenceDownload>;
  abstract deleteEvidence(
    attachmentId: number,
    rowVersion: string,
  ): Observable<EvidenceRemovalResult>;
}

/**
 * KPI dashboards and reports (read only). Every figure is the server's, over the scorecards the
 * caller may see (own, team, or everyone for the HR tier); screens display it and never compute an
 * official figure. `periodId` left out means the default period (see `kpi-insight.models.ts`).
 * Paging is `pageIndex` and `pageSize` (1 to 200); `sort` takes only the fields of
 * `KPI_REPORT_SORT_KEYS` (the ranking none: its order is fixed).
 */
export abstract class KpiInsightDataService {
  abstract employeeDashboard(periodId?: number | null): Observable<KpiEmployeeDashboard>;
  /** Needs `kpi:Approve`. */
  abstract managerDashboard(periodId?: number | null): Observable<KpiManagerDashboard>;
  /** HR tier only (`kpi:Approve` with `employees:Edit`); anyone else gets a Forbidden error. */
  abstract hrDashboard(
    periodId?: number | null,
    companyId?: number | null,
  ): Observable<KpiHrDashboard>;

  /** One row per period with a matching scorecard. Monthly, Quarterly, Annual: filter `periodType` (and `year`). */
  abstract summaryReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiPeriodSummary>>;
  /** One employee's scorecards. An employee outside the caller's scope is NotFound. */
  abstract employeeReport(
    employeeId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiEmployeeReport>;
  /** One department for one period (`periodId` or the default period). */
  abstract departmentReport(
    departmentId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiDepartmentReport>;
  abstract rankingReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiRankingRow>>;
  abstract kpiAchievementReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiAchievementRow>>;
  abstract targetVsActualReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiTargetVsActualRow>>;
}

/**
 * Every method of {@link KpiInsightDataService}, once. `satisfies` makes the compiler refuse this
 * object when a method of the contract is missing (or a name is not one), so the lazy binding can
 * never forget a new method.
 */
const KPI_INSIGHT_METHOD_SET = {
  employeeDashboard: true,
  managerDashboard: true,
  hrDashboard: true,
  summaryReport: true,
  employeeReport: true,
  departmentReport: true,
  rankingReport: true,
  kpiAchievementReport: true,
  targetVsActualReport: true,
} as const satisfies Record<keyof KpiInsightDataService, true>;

/** The method names of {@link KpiInsightDataService} (for the lazy binding). */
export const KPI_INSIGHT_METHODS = Object.keys(
  KPI_INSIGHT_METHOD_SET,
) as readonly (keyof KpiInsightDataService)[];
