import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PagedResult, PageRequest, SortState } from '../../core/models/common.models';
import { fullName } from '../../core/models/employee.models';
import { ExportRequest } from '../../core/models/excel.models';
import {
  KpiAchievementRow,
  KpiDepartmentPerformance,
  KpiDepartmentReport,
  KpiEmployeeDashboard,
  KpiEmployeeReport,
  KpiEmployeeScore,
  KpiEmployeeScorecard,
  KpiHrDashboard,
  KpiManagerDashboard,
  KpiPeriodRef,
  KpiPeriodSummary,
  KpiRankingRow,
  KpiRatingCount,
  KpiReportFilter,
  KpiReportItem,
  KpiReportScorecardRow,
  KpiTargetVsActualRow,
} from '../../core/models/kpi-insight.models';
import {
  EvaluationPeriod,
  KPI_FREQUENCIES,
  KpiFrequency,
  SCORECARD_STATUSES,
  ScorecardItem,
  ScorecardStatus,
} from '../../core/models/kpi.models';
import { KpiInsightDataService } from '../../core/services/data/kpi-data.services';
import { sumKpiWeights } from '../../core/utilities/kpi-calculation';
import {
  KPI_ATTENTION_LIST_LIMIT,
  KPI_HISTORY_LIMIT,
  KPI_MANAGER_EMPLOYEE_LIMIT,
  KPI_TOP_PERFORMER_COUNT,
  byCodeThenId,
  byScoreThenCode,
  kpiAttentionThreshold,
  kpiAverage,
  kpiByStatus,
  kpiCompetitionRanks,
  kpiCompletionRate,
  kpiDefaultPeriod,
  ordinal,
} from '../../core/utilities/kpi-figures';
import { isSubmissionPending } from '../../core/utilities/kpi-period-rules';
import { reviewVisibilityForOwner } from '../../core/utilities/kpi-review-visibility';
import { ScorecardRecord } from '../../mock-data/kpi/kpi-records';
import { todayIso } from '../../shared/utilities/date.util';
import { compareValues, matchesSearch, paginate } from '../../shared/utilities/query.util';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { ExportRecord } from '../excel/excel-module';
import { mockError, mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import {
  EvaluationPeriodMockService,
  KpiCategoryMockService,
  KpiTemplateMockService,
  RatingBandMockService,
} from './kpi-setup-mock.services';
import { KpiMockActor, ScorecardMockService } from './scorecard-mock.service';

const MANAGER_STAGE: readonly ScorecardStatus[] = ['Submitted', 'Resubmitted', 'Under Review'];
const SEARCH_MAX = 100;
const FIRST_YEAR = 1900;
const LAST_YEAR = 9998;
const MAX_PAGE_SIZE = 200;

/** A scorecard joined to its employee, period and copied department, as the reports read it. */
interface CardRow {
  record: ScorecardRecord;
  scorecardId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  departmentId: number;
  departmentName: string;
  period: EvaluationPeriod;
  status: ScorecardStatus;
  overallScore: number | null;
  ratingLabel: string | null;
}

/** One KPI of a scorecard with the scorecard's names (target vs actual, the item export). */
interface ItemRow {
  card: CardRow;
  item: ScorecardItem;
}

/** The KPI Excel exports of the mock (export only, see `kpi-excel.modules.ts`). */
export type KpiExportModuleCode = 'kpi-scorecards' | 'kpi-ranking' | 'kpi-target-vs-actual';

const SORTS = {
  summary: ['periodStart', 'periodName', 'periodType'],
  employee: ['periodStart', 'periodName', 'overallScore', 'status'],
  department: ['employeeCode', 'employeeName', 'overallScore', 'ratingLabel', 'status'],
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
} as const;

/**
 * In-memory KPI dashboards, reports and exports, over the mock scorecards the signed-in user may see
 * (the scope of `ScorecardMockService`: HR everyone, a manager their own, their direct reports' and
 * the ones naming them reviewer, anyone else their own). Every figure follows the API's single
 * definition (`core/utilities/kpi-figures.ts`), and the filters, sort whitelists and messages are the
 * API's. Lazily loaded: the binding in `provideMockDataServices()` imports it on first use.
 *
 * DEMO ONLY, NOT AUTHORITATIVE: "HR" and "manager" come from the simulated session's role names, as
 * in the scorecard mock. With the API on, the server computes every figure.
 */
@Injectable({ providedIn: 'root' })
export class KpiInsightMockService implements KpiInsightDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly scorecards = inject(ScorecardMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly periods = inject(EvaluationPeriodMockService);
  private readonly templates = inject(KpiTemplateMockService);
  private readonly categories = inject(KpiCategoryMockService);
  private readonly bands = inject(RatingBandMockService);

  private respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  // ---- Rows -----------------------------------------------------------------------------------

  private row(record: ScorecardRecord): CardRow | null {
    const period = this.periods.findById(record.periodId);
    if (!period) return null;
    const employee = this.employees.findById(record.employeeId);
    return {
      record,
      scorecardId: record.id,
      employeeId: record.employeeId,
      employeeCode: employee?.employeeCode ?? `#${record.employeeId}`,
      employeeName: employee ? fullName(employee) : `Employee #${record.employeeId}`,
      departmentId: record.departmentId,
      departmentName: this.departments.findById(record.departmentId)?.name ?? '—',
      period,
      status: record.status,
      overallScore: record.overallScore,
      ratingLabel: record.ratingLabel,
    };
  }

  private rows(records: readonly ScorecardRecord[]): CardRow[] {
    return records.map((r) => this.row(r)).filter((r): r is CardRow => r !== null);
  }

  private scope(): { actor: KpiMockActor; visible: CardRow[] } {
    const { actor, visible } = this.scorecards.scopeNow();
    return { actor, visible: this.rows(visible) };
  }

  private categoryName(categoryId: number, fallback: string): string {
    return this.categories.findById(categoryId)?.name ?? fallback;
  }

  // ---- Filters, sorting and paging ------------------------------------------------------------

  private invalid(field: string, message: string): never {
    throw mockError('Validation', message, { [field]: message });
  }

  /**
   * The rows narrowed by the report filters, the search (employee code or name) and an optional
   * range of period dates (a period counts when it overlaps it). Unknown texts are a 400.
   */
  private filtered(
    rows: readonly CardRow[],
    filter: Partial<KpiReportFilter> | undefined,
    search?: string,
    dateFrom?: string | null,
    dateTo?: string | null,
  ): CardRow[] {
    const f = (filter ?? {}) as Partial<Record<keyof KpiReportFilter, unknown>>;
    const id = (key: keyof KpiReportFilter): number | null =>
      f[key] === null || f[key] === undefined || f[key] === '' ? null : Number(f[key]);
    const text = (key: keyof KpiReportFilter): string | null => {
      const value = f[key];
      return value === null || value === undefined || String(value).trim() === ''
        ? null
        : String(value).trim();
    };

    const status = text('status');
    if (status !== null && !SCORECARD_STATUSES.includes(status as ScorecardStatus)) {
      this.invalid('status', `Unknown status "${status}". Use ${SCORECARD_STATUSES.join(', ')}.`);
    }
    let periodType = text('periodType');
    if (periodType !== null && periodType.toLowerCase() === 'annual') periodType = 'Yearly';
    if (periodType !== null && !KPI_FREQUENCIES.includes(periodType as KpiFrequency)) {
      this.invalid(
        'periodType',
        `Unknown period type "${text('periodType')}". Use ${KPI_FREQUENCIES.join(', ')} (or Annual).`,
      );
    }
    const year = id('year');
    if (year !== null && !(Number.isInteger(year) && year >= FIRST_YEAR && year <= LAST_YEAR)) {
      this.invalid('year', `Year must be between ${FIRST_YEAR} and ${LAST_YEAR}.`);
    }
    if (search && search.length > SEARCH_MAX) {
      this.invalid('search', `Search must be at most ${SEARCH_MAX} characters.`);
    }

    const placement: [keyof KpiReportFilter, (r: CardRow) => number][] = [
      ['companyId', (r) => r.record.companyId],
      ['branchId', (r) => r.record.branchId],
      ['departmentId', (r) => r.record.departmentId],
      ['positionId', (r) => r.record.positionId],
      ['employeeId', (r) => r.record.employeeId],
      ['periodId', (r) => r.record.periodId],
    ];
    const categoryId = id('categoryId');
    return rows.filter(
      (r) =>
        placement.every(([key, of]) => id(key) === null || of(r) === id(key)) &&
        (status === null || r.status === status) &&
        (periodType === null || r.period.periodType === periodType) &&
        (year === null || Number(r.period.startDate.slice(0, 4)) === year) &&
        (!dateFrom || r.period.endDate >= dateFrom) &&
        (!dateTo || r.period.startDate <= dateTo) &&
        (categoryId === null || r.record.items.some((i) => i.categoryId === categoryId)) &&
        matchesSearch(r, search?.trim(), ['employeeCode', 'employeeName']),
    );
  }

  /** The requested sort when it names a whitelisted field (400 on `sort` otherwise); null for none. */
  private sortOf(
    sort: SortState | undefined,
    keys: readonly string[],
  ): { key: string; descending: boolean } | null {
    if (!sort?.active || !sort.direction) return null;
    const key = keys.find((k) => k.toLowerCase() === sort.active.toLowerCase());
    if (!key) {
      this.invalid('sort', `Cannot sort by "${sort.active}". Sortable fields: ${keys.join(', ')}.`);
    }
    return { key, descending: sort.direction === 'desc' };
  }

  /** Sorts by the chosen key (then the tie-breaker) or the default order. */
  private ordered<T>(
    rows: readonly T[],
    chosen: { key: string; descending: boolean } | null,
    keyOf: (row: T, key: string) => unknown,
    tieBreak: (a: T, b: T) => number,
    defaultOrder: (a: T, b: T) => number,
  ): T[] {
    if (!chosen) return [...rows].sort(defaultOrder);
    const factor = chosen.descending ? -1 : 1;
    return [...rows].sort(
      (a, b) =>
        factor * compareValues(keyOf(a, chosen.key), keyOf(b, chosen.key)) || tieBreak(a, b),
    );
  }

  private page<T>(rows: readonly T[], request: PageRequest<object>): PagedResult<T> {
    const size = Math.min(MAX_PAGE_SIZE, Math.max(1, request.pageSize));
    return paginate([...rows], request.pageIndex, size);
  }

  /** `periodId` when given (404 when it does not exist), else the default among the candidates. */
  private choosePeriod(
    periodId: number | null | undefined,
    candidates: readonly EvaluationPeriod[],
  ): EvaluationPeriod | null {
    if (periodId !== null && periodId !== undefined) {
      const named = this.periods.findById(periodId);
      if (!named) throw mockError('NotFound', `Evaluation period #${periodId} was not found.`);
      return named;
    }
    return kpiDefaultPeriod(candidates);
  }

  private periodsOf(rows: readonly CardRow[]): EvaluationPeriod[] {
    return [...new Map(rows.map((r) => [r.period.id, r.period])).values()];
  }

  private periodRef(period: EvaluationPeriod | null): KpiPeriodRef | null {
    return period
      ? {
          id: period.id,
          name: period.name,
          periodType: period.periodType,
          startDate: period.startDate,
          endDate: period.endDate,
          status: period.status,
          submissionDueDate: period.submissionDueDate,
        }
      : null;
  }

  private score(row: CardRow): KpiEmployeeScore {
    return {
      scorecardId: row.scorecardId,
      employeeId: row.employeeId,
      employeeCode: row.employeeCode,
      employeeName: row.employeeName,
      departmentId: row.departmentId,
      departmentName: row.departmentName,
      status: row.status,
      overallScore: row.overallScore,
      ratingLabel: row.ratingLabel,
    };
  }

  /** The Finalized scorecards with a score: the only ones averages, minimums, maximums and ranks count. */
  private scored(rows: readonly CardRow[]): (CardRow & { overallScore: number })[] {
    return rows.filter(
      (r): r is CardRow & { overallScore: number } =>
        r.status === 'Finalized' && r.overallScore !== null,
    );
  }

  private average(rows: readonly CardRow[]): number | null {
    return kpiAverage(this.scored(rows).map((r) => r.overallScore));
  }

  private items(rows: readonly CardRow[], categoryId: number | null | undefined): ItemRow[] {
    return rows.flatMap((card) =>
      [...card.record.items]
        .filter((item) => !categoryId || item.categoryId === categoryId)
        .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
        .map((item) => ({ card, item })),
    );
  }

  private reportItem(item: ScorecardItem): KpiReportItem {
    return {
      id: item.id,
      kpiCode: item.kpiCode,
      kpiName: item.kpiName,
      categoryName: this.categoryName(item.categoryId, item.categoryName),
      unit: item.unit,
      target: item.target,
      actualValue: item.actualValue,
      achievementPct: item.achievementPct,
      weight: item.weight,
      weightedScore: item.weightedScore,
    };
  }

  // ---- Dashboards -----------------------------------------------------------------------------

  employeeDashboard(periodId?: number | null): Observable<KpiEmployeeDashboard> {
    return this.respond(() => {
      const { actor, visible } = this.scope();
      const mine =
        actor.employeeId === null
          ? []
          : visible.filter((r) => r.employeeId === actor.employeeId && r.status !== 'Planning');
      const period = this.choosePeriod(periodId, this.periodsOf(mine));
      const empty: KpiEmployeeDashboard = {
        period: this.periodRef(period),
        scorecard: null,
        items: [],
        pending: { isPending: false, dueDate: null, isOverdue: false },
        feedback: null,
        history: [],
      };
      if (!period) return empty;

      const current = mine.find((r) => r.period.id === period.id);
      const history = mine
        .filter((r) => r.period.id !== period.id && r.period.startDate < period.startDate)
        .sort(
          (a, b) =>
            b.period.startDate.localeCompare(a.period.startDate) || b.period.id - a.period.id,
        )
        .slice(0, KPI_HISTORY_LIMIT)
        .map((r) => ({
          scorecardId: r.scorecardId,
          periodId: r.period.id,
          periodName: r.period.name,
          periodType: r.period.periodType,
          startDate: r.period.startDate,
          endDate: r.period.endDate,
          status: r.status,
          overallScore: r.overallScore,
          ratingLabel: r.ratingLabel,
        }));
      // Pending: the employee can still act on it (Active, or Completed for a Returned scorecard).
      const pending = !!current && isSubmissionPending(period.status, current.status);
      const due = period.submissionDueDate;
      if (!current) return { ...empty, pending: { ...empty.pending, dueDate: due }, history };

      const record = current.record;
      const own = reviewVisibilityForOwner(record.status);
      const reviewer =
        record.reviewerEmployeeId === null
          ? undefined
          : this.employees.findById(record.reviewerEmployeeId);
      return {
        period: this.periodRef(period),
        scorecard: {
          id: record.id,
          status: record.status,
          overallScore: record.overallScore,
          ratingLabel: record.ratingLabel,
          reviewerEmployeeId: record.reviewerEmployeeId,
          reviewerName: reviewer ? fullName(reviewer) : null,
          submittedOn: record.submittedOn,
          finalizedOn: record.finalizedOn,
        },
        items: [...record.items]
          .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
          .map((item) => ({
            id: item.id,
            kpiCode: item.kpiCode,
            kpiName: item.kpiName,
            unit: item.unit,
            target: item.target,
            actualValue: item.actualValue,
            achievementPct: item.achievementPct,
            weight: item.weight,
            weightedScore: item.weightedScore,
          })),
        pending: {
          isPending: pending,
          dueDate: due,
          isOverdue: pending && due !== null && todayIso() > due,
        },
        // Always the caller's own scorecard: the manager's review shows only once it is final.
        feedback: {
          managerRating: own.managerReview ? record.managerRating : null,
          managerComment: own.managerReview ? record.managerComment : null,
        },
        history,
      };
    });
  }

  managerDashboard(periodId?: number | null): Observable<KpiManagerDashboard> {
    return this.respond(() => {
      const { actor, visible } = this.scope();
      const me = actor.employeeId;
      const reports = new Set(
        me === null
          ? []
          : this.employees
              .records()
              .filter((e) => e.managerId === me)
              .map((e) => e.id),
      );
      const team =
        me === null
          ? []
          : visible.filter(
              (r) =>
                r.employeeId !== me &&
                (reports.has(r.employeeId) || r.record.reviewerEmployeeId === me),
            );
      const period = this.choosePeriod(periodId, this.periodsOf(team));
      const inPeriod = period ? team.filter((r) => r.period.id === period.id) : [];
      // At most KPI_MANAGER_EMPLOYEE_LIMIT rows, by employee code; the count covers them all.
      const listed = [...inPeriod].sort(byCodeThenId).slice(0, KPI_MANAGER_EMPLOYEE_LIMIT);
      return {
        period: this.periodRef(period),
        teamSize: inPeriod.length,
        byStatus: kpiByStatus(inPeriod.map((r) => r.status)),
        pendingReviews: inPeriod.filter(
          (r) => MANAGER_STAGE.includes(r.status) && r.record.reviewerEmployeeId === me,
        ).length,
        approvedOrFinalized: inPeriod.filter(
          (r) => r.status === 'Approved' || r.status === 'Finalized',
        ).length,
        averageScore: this.average(inPeriod),
        employees: listed.map((r) => this.score(r)),
        employeeCount: inPeriod.length,
        employeesTruncated: inPeriod.length > listed.length,
      };
    });
  }

  hrDashboard(periodId?: number | null, companyId?: number | null): Observable<KpiHrDashboard> {
    return this.respond(() => {
      const { actor, visible } = this.scope();
      if (!actor.hr) {
        throw mockError('Forbidden', 'The HR dashboard needs HR rights.');
      }
      const company = companyId ?? null;
      const candidates = this.periods
        .records()
        .filter((p) => p.status !== 'Draft' && (company === null || p.companyId === company));
      const period = this.choosePeriod(periodId, candidates);
      const threshold = kpiAttentionThreshold(
        this.bands
          .records()
          .filter((b) => b.status === 'Active')
          .map((b) => b.minScore),
      );
      const inPeriod = period
        ? visible.filter(
            (r) =>
              r.period.id === period.id && (company === null || r.record.companyId === company),
          )
        : [];
      const count = (...statuses: ScorecardStatus[]) =>
        inPeriod.filter((r) => statuses.includes(r.status)).length;
      const finalized = count('Finalized');
      const scored = this.scored(inPeriod);
      const low = scored
        .filter((r) => r.overallScore < threshold)
        .sort(
          (a, b) =>
            a.overallScore - b.overallScore ||
            ordinal(a.employeeCode, b.employeeCode) ||
            a.scorecardId - b.scorecardId,
        );
      return {
        period: this.periodRef(period),
        companyId: company,
        employeesWithKpis: inPeriod.filter((r) => r.record.items.length > 0).length,
        totalAssignments: inPeriod.reduce((sum, r) => sum + r.record.items.length, 0),
        completedEvaluations: finalized,
        pendingSubmissions: count('Draft', 'Returned'),
        pendingApprovals: count('Submitted', 'Resubmitted', 'Under Review', 'Approved'),
        averageScore: this.average(inPeriod),
        completionRatePct: kpiCompletionRate(finalized, inPeriod.length - count('Planning')),
        byStatus: kpiByStatus(inPeriod.map((r) => r.status)),
        departments: this.departmentPerformance(inPeriod),
        topPerformers: [...scored]
          .sort(byScoreThenCode)
          .slice(0, KPI_TOP_PERFORMER_COUNT)
          .map((r) => this.score(r)),
        attentionThreshold: threshold,
        requiringAttentionCount: low.length,
        requiringAttention: low.slice(0, KPI_ATTENTION_LIST_LIMIT).map((r) => this.score(r)),
        ratingDistribution: this.ratingDistribution(inPeriod),
      };
    });
  }

  private departmentPerformance(rows: readonly CardRow[]): KpiDepartmentPerformance[] {
    const groups = new Map<number, CardRow[]>();
    for (const row of rows)
      groups.set(row.departmentId, [...(groups.get(row.departmentId) ?? []), row]);
    return [...groups.entries()]
      .map(([departmentId, cards]) => ({
        departmentId,
        departmentName: cards[0].departmentName,
        scorecards: cards.length,
        finalized: cards.filter((c) => c.status === 'Finalized').length,
        averageScore: this.average(cards),
      }))
      .sort(
        (a, b) => ordinal(a.departmentName, b.departmentName) || a.departmentId - b.departmentId,
      );
  }

  /** Finalized per label, by the band's minimum score (highest first); labels of no current band after them. */
  private ratingDistribution(rows: readonly CardRow[]): KpiRatingCount[] {
    const minOf = new Map(
      this.bands
        .records()
        .filter((b) => b.status === 'Active')
        .map((b) => [b.label, b.minScore]),
    );
    const counts = new Map<string | null, number>();
    for (const row of rows.filter((r) => r.status === 'Finalized')) {
      counts.set(row.ratingLabel, (counts.get(row.ratingLabel) ?? 0) + 1);
    }
    const known = (label: string | null) => label !== null && minOf.has(label);
    return [...counts.entries()]
      .sort(
        ([a], [b]) =>
          (known(a) ? 0 : 1) - (known(b) ? 0 : 1) ||
          (known(b) ? minOf.get(b!)! : 0) - (known(a) ? minOf.get(a!)! : 0) ||
          (a === b ? 0 : a === null ? -1 : b === null ? 1 : ordinal(a, b)),
      )
      .map(([ratingLabel, count]) => ({ ratingLabel, count }));
  }

  // ---- Reports --------------------------------------------------------------------------------

  summaryReport(request: PageRequest<KpiReportFilter>): Observable<PagedResult<KpiPeriodSummary>> {
    return this.respond(() => {
      const chosen = this.sortOf(request.sort, SORTS.summary);
      const cards = this.filtered(this.scope().visible, request.filters, request.search);
      const periods = this.ordered(
        this.periodsOf(cards),
        chosen,
        (p, key) =>
          key === 'periodStart' ? p.startDate : key === 'periodName' ? p.name : p.periodType,
        (a, b) => a.id - b.id,
        (a, b) => b.startDate.localeCompare(a.startDate) || b.id - a.id,
      );
      const page = this.page(periods, request);
      return {
        ...page,
        items: page.items.map((period): KpiPeriodSummary => {
          const own = cards.filter((c) => c.period.id === period.id);
          const scores = this.scored(own).map((c) => c.overallScore);
          const planning = own.filter((c) => c.status === 'Planning').length;
          const finalized = own.filter((c) => c.status === 'Finalized').length;
          return {
            periodId: period.id,
            periodName: period.name,
            periodType: period.periodType,
            startDate: period.startDate,
            endDate: period.endDate,
            periodStatus: period.status,
            scorecards: own.length,
            inPlanning: planning,
            finalized,
            rejected: own.filter((c) => c.status === 'Rejected').length,
            averageScore: kpiAverage(scores),
            minScore: scores.length ? Math.min(...scores) : null,
            maxScore: scores.length ? Math.max(...scores) : null,
            completionRatePct: kpiCompletionRate(finalized, own.length - planning),
          };
        }),
      };
    });
  }

  employeeReport(
    employeeId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiEmployeeReport> {
    return this.respond(() => {
      const chosen = this.sortOf(request.sort, SORTS.employee);
      const { actor, visible } = this.scope();
      const person = this.employees.findById(employeeId);
      const theirs = visible.filter((r) => r.employeeId === employeeId);
      const inTier =
        actor.hr ||
        (actor.employeeId !== null &&
          (employeeId === actor.employeeId ||
            (actor.approver && person?.managerId === actor.employeeId)));
      if (!person || (!inTier && theirs.length === 0)) {
        throw mockError('NotFound', `Employee #${employeeId} was not found.`);
      }
      const cards = this.filtered(theirs, { ...request.filters, employeeId: undefined });
      const rows = this.ordered(
        cards,
        chosen,
        (r, key) =>
          key === 'periodStart'
            ? r.period.startDate
            : key === 'periodName'
              ? r.period.name
              : key === 'overallScore'
                ? (r.overallScore ?? 0)
                : r.status,
        (a, b) => a.scorecardId - b.scorecardId,
        (a, b) =>
          b.period.startDate.localeCompare(a.period.startDate) || b.scorecardId - a.scorecardId,
      );
      const page = this.page(rows, request);
      const categoryId = request.filters?.categoryId;
      return {
        employeeId,
        employeeCode: person.employeeCode,
        employeeName: fullName(person),
        finalized: cards.filter((c) => c.status === 'Finalized').length,
        averageScore: this.average(cards),
        scorecards: {
          ...page,
          items: page.items.map((r): KpiEmployeeScorecard => ({
            scorecardId: r.scorecardId,
            periodId: r.period.id,
            periodName: r.period.name,
            periodType: r.period.periodType,
            startDate: r.period.startDate,
            endDate: r.period.endDate,
            departmentId: r.departmentId,
            departmentName: r.departmentName,
            status: r.status,
            overallScore: r.overallScore,
            ratingLabel: r.ratingLabel,
            items: this.items([r], categoryId).map(({ item }) => this.reportItem(item)),
          })),
        },
      };
    });
  }

  departmentReport(
    departmentId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiDepartmentReport> {
    return this.respond(() => {
      const chosen = this.sortOf(request.sort, SORTS.department);
      const department = this.departments.findById(departmentId);
      if (!department) throw mockError('NotFound', `Department #${departmentId} was not found.`);
      const filtered = this.filtered(
        this.scope().visible,
        { ...request.filters, departmentId, periodId: undefined },
        request.search,
      );
      const period = this.choosePeriod(request.filters?.periodId, this.periodsOf(filtered));
      const cards = filtered.filter((r) => r.period.id === (period?.id ?? 0));
      const rows = this.ordered(
        cards,
        chosen,
        (r, key) =>
          key === 'overallScore'
            ? (r.overallScore ?? 0)
            : key === 'ratingLabel'
              ? (r.ratingLabel ?? '')
              : (r as unknown as Record<string, unknown>)[key],
        (a, b) => a.scorecardId - b.scorecardId,
        (a, b) => ordinal(a.employeeCode, b.employeeCode) || a.scorecardId - b.scorecardId,
      );
      const page = this.page(rows, request);
      return {
        departmentId,
        departmentName: department.name,
        period: this.periodRef(period),
        scorecards: cards.length,
        finalized: cards.filter((c) => c.status === 'Finalized').length,
        averageScore: this.average(cards),
        rows: {
          ...page,
          items: page.items.map((r): KpiReportScorecardRow => ({
            scorecardId: r.scorecardId,
            employeeId: r.employeeId,
            employeeCode: r.employeeCode,
            employeeName: r.employeeName,
            departmentId: r.departmentId,
            departmentName: r.departmentName,
            periodId: r.period.id,
            periodName: r.period.name,
            status: r.status,
            overallScore: r.overallScore,
            ratingLabel: r.ratingLabel,
          })),
        },
      };
    });
  }

  /** Finalized scorecards with a score, highest first, ties by employee code then id. */
  private ranked(rows: readonly CardRow[]): (CardRow & { overallScore: number })[] {
    return this.scored(rows).sort(
      (a, b) =>
        b.overallScore - a.overallScore ||
        ordinal(a.employeeCode, b.employeeCode) ||
        a.scorecardId - b.scorecardId,
    );
  }

  private rankingRows(
    rows: readonly (CardRow & { overallScore: number })[],
    firstRank: number,
    firstPosition: number,
  ): KpiRankingRow[] {
    const ranks = kpiCompetitionRanks(
      rows.map((r) => r.overallScore),
      firstRank,
      firstPosition,
    );
    return rows.map((r, i) => ({
      rank: ranks[i],
      scorecardId: r.scorecardId,
      employeeId: r.employeeId,
      employeeCode: r.employeeCode,
      employeeName: r.employeeName,
      departmentId: r.departmentId,
      departmentName: r.departmentName,
      periodId: r.period.id,
      periodName: r.period.name,
      overallScore: r.overallScore,
      ratingLabel: r.ratingLabel,
    }));
  }

  rankingReport(request: PageRequest<KpiReportFilter>): Observable<PagedResult<KpiRankingRow>> {
    return this.respond(() => {
      const sort = request.sort;
      if (
        sort?.active &&
        !(sort.active.toLowerCase() === 'rank' && sort.direction.toLowerCase() !== 'desc')
      ) {
        this.invalid('sort', 'The ranking is always ordered by rank (sort=rank:asc).');
      }
      const ranked = this.ranked(
        this.filtered(this.scope().visible, request.filters, request.search),
      );
      const page = this.page(ranked, request);
      if (page.items.length === 0) return { ...page, items: [] };
      // A page that starts inside a tie keeps the shared rank: 1 + the number of higher scores.
      const first = page.items[0].overallScore;
      const higher = ranked.filter((r) => r.overallScore > first).length;
      return {
        ...page,
        items: this.rankingRows(page.items, higher + 1, page.pageIndex * page.pageSize + 1),
      };
    });
  }

  kpiAchievementReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiAchievementRow>> {
    return this.respond(() => {
      const chosen = this.sortOf(request.sort, SORTS.kpiAchievement);
      const cards = this.filtered(this.scope().visible, request.filters, request.search);
      const groups = new Map<number, ItemRow[]>();
      for (const row of this.items(cards, request.filters?.categoryId)) {
        groups.set(row.item.templateId, [...(groups.get(row.item.templateId) ?? []), row]);
      }
      const rows = [...groups.entries()].map(([templateId, items]): KpiAchievementRow => {
        const template = this.templates.findById(templateId);
        const first = items[0].item;
        const counted = items
          .filter(({ card, item }) => card.status === 'Finalized' && item.achievementPct !== null)
          .map(({ item }) => item.achievementPct as number);
        const categoryId = template?.categoryId ?? first.categoryId;
        return {
          templateId,
          kpiCode: template?.code ?? first.kpiCode,
          kpiName: template?.name ?? first.kpiName,
          categoryId,
          categoryName: this.categoryName(categoryId, first.categoryName),
          assigned: items.length,
          counted: counted.length,
          averageAchievementPct: kpiAverage(counted),
          minAchievementPct: counted.length ? Math.min(...counted) : null,
          maxAchievementPct: counted.length ? Math.max(...counted) : null,
        };
      });
      const ordered = this.ordered(
        rows,
        chosen,
        (r, key) =>
          key === 'averageAchievement'
            ? (r.averageAchievementPct ?? 0)
            : (r as unknown as Record<string, unknown>)[key],
        (a, b) => a.templateId - b.templateId,
        (a, b) => ordinal(a.kpiCode, b.kpiCode) || a.templateId - b.templateId,
      );
      return this.page(ordered, request);
    });
  }

  /** Newest period first, then employee code, scorecard and KPI order. */
  private itemDefaultOrder(a: ItemRow, b: ItemRow): number {
    return (
      b.card.period.startDate.localeCompare(a.card.period.startDate) ||
      ordinal(a.card.employeeCode, b.card.employeeCode) ||
      a.card.scorecardId - b.card.scorecardId ||
      a.item.sortOrder - b.item.sortOrder ||
      a.item.id - b.item.id
    );
  }

  private targetRow({ card, item }: ItemRow): KpiTargetVsActualRow {
    return {
      itemId: item.id,
      scorecardId: card.scorecardId,
      employeeId: card.employeeId,
      employeeCode: card.employeeCode,
      employeeName: card.employeeName,
      departmentId: card.departmentId,
      departmentName: card.departmentName,
      periodId: card.period.id,
      periodName: card.period.name,
      status: card.status,
      kpiCode: item.kpiCode,
      kpiName: item.kpiName,
      categoryName: this.categoryName(item.categoryId, item.categoryName),
      unit: item.unit,
      target: item.target,
      actualValue: item.actualValue,
      achievementPct: item.achievementPct,
      weight: item.weight,
      weightedScore: item.weightedScore,
    };
  }

  targetVsActualReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiTargetVsActualRow>> {
    return this.respond(() => {
      const chosen = this.sortOf(request.sort, SORTS.targetVsActual);
      const cards = this.filtered(this.scope().visible, request.filters, request.search);
      const rows = this.items(cards, request.filters?.categoryId).map((row) => ({
        row,
        dto: this.targetRow(row),
      }));
      const ordered = this.ordered(
        rows,
        chosen,
        ({ row, dto }, key) =>
          key === 'periodStart'
            ? row.card.period.startDate
            : key === 'achievementPct'
              ? (dto.achievementPct ?? 0)
              : key === 'weightedScore'
                ? (dto.weightedScore ?? 0)
                : (dto as unknown as Record<string, unknown>)[key],
        (a, b) => a.dto.itemId - b.dto.itemId,
        (a, b) => this.itemDefaultOrder(a.row, b.row),
      );
      const page = this.page(ordered, request);
      return { ...page, items: page.items.map(({ dto }) => dto) };
    });
  }

  // ---- Excel export (synchronous; called by the KPI Excel modules) ----------------------------

  /**
   * The rows of a KPI export, as the API's modules build them: `filtered` applies the filters, the
   * search and the date range (periods that overlap it); `all` none; `selected` takes scorecard ids
   * (ids outside the caller's scope are left out). At most `limit + 1` rows. Never a comment, a
   * manager rating or an evidence file.
   */
  exportRows(module: KpiExportModuleCode, request: ExportRequest, limit: number): ExportRecord[] {
    const { visible } = this.scope();
    let cards: CardRow[];
    let categoryId: number | null = null;
    if (request.scope === 'selected') {
      const ids = new Set(request.selectedIds ?? []);
      cards = visible.filter((r) => ids.has(r.scorecardId));
    } else {
      const filtered = request.scope === 'filtered';
      const filters = (filtered ? (request.filters ?? {}) : {}) as Partial<KpiReportFilter>;
      cards = this.filtered(
        visible,
        filters,
        filtered ? request.search : undefined,
        filtered ? request.dateFrom : null,
        filtered ? request.dateTo : null,
      );
      categoryId = filters.categoryId ? Number(filters.categoryId) : null;
    }

    switch (module) {
      case 'kpi-scorecards':
        return cards
          .sort(
            (a, b) =>
              b.period.startDate.localeCompare(a.period.startDate) ||
              ordinal(a.employeeCode, b.employeeCode) ||
              a.scorecardId - b.scorecardId,
          )
          .slice(0, limit + 1)
          .map((r) => {
            const reviewer =
              r.record.reviewerEmployeeId === null
                ? undefined
                : this.employees.findById(r.record.reviewerEmployeeId);
            return {
              employeeCode: r.employeeCode,
              employeeName: r.employeeName,
              department: r.departmentName,
              period: r.period.name,
              status: r.status,
              itemCount: r.record.items.length,
              totalWeight: sumKpiWeights(r.record.items.map((i) => i.weight)),
              overallScore: r.overallScore,
              ratingLabel: r.ratingLabel,
              reviewer: reviewer ? fullName(reviewer) : null,
              planFinalizedOn: r.record.planFinalizedOn,
              submittedOn: r.record.submittedOn,
              submissionDueDate: r.period.submissionDueDate,
              finalizedOn: r.record.finalizedOn,
            };
          });
      case 'kpi-ranking':
        // Ranks over the exported rows (for `selected`, among the selected scorecards).
        return this.rankingRows(this.ranked(cards).slice(0, limit + 1), 1, 1).map((r) => ({
          rank: r.rank,
          employeeCode: r.employeeCode,
          employeeName: r.employeeName,
          department: r.departmentName,
          period: r.periodName,
          overallScore: r.overallScore,
          ratingLabel: r.ratingLabel,
        }));
      case 'kpi-target-vs-actual':
        return this.items(cards, categoryId)
          .sort((a, b) => this.itemDefaultOrder(a, b))
          .slice(0, limit + 1)
          .map((row) => {
            const r = this.targetRow(row);
            return {
              employeeCode: r.employeeCode,
              employeeName: r.employeeName,
              department: r.departmentName,
              period: r.periodName,
              status: r.status,
              kpiCode: r.kpiCode,
              kpiName: r.kpiName,
              category: r.categoryName,
              unit: r.unit,
              target: r.target,
              actual: r.actualValue,
              achievementPct: r.achievementPct,
              weight: r.weight,
              weightedScore: r.weightedScore,
            };
          });
    }
  }
}
