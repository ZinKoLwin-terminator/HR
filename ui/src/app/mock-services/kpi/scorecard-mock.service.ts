import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap, timer } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { CURRENT_EMPLOYMENT_STATUSES, Employee, fullName } from '../../core/models/employee.models';
import {
  AssignKpiRequest,
  AssignKpiResult,
  AssignmentItemInput,
  BulkAssignKpiRequest,
  BulkAssignmentResult,
  BulkAssignedEmployee,
  BulkSkippedEmployee,
  EVIDENCE_MAX_PER_KPI,
  EvaluationPeriod,
  EvaluationPeriodStatus,
  EvidenceDownload,
  EvidenceRemovalResult,
  EvidenceUploadResult,
  KpiTemplate,
  ResultItemInput,
  ReviewAction,
  ReviewActionName,
  ReviewDecision,
  ReviewItemInput,
  ReviewStep,
  SaveResultsRequest,
  SaveReviewRequest,
  Scorecard,
  ScorecardDetail,
  ScorecardFilter,
  ScorecardItem,
  ScorecardItemUpdate,
  ScorecardStatus,
} from '../../core/models/kpi.models';
import { ADMIN_ROLE_NAME } from '../../core/models/settings.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { ScorecardDataService } from '../../core/services/data/kpi-data.services';
import {
  KpiRuleInput,
  kpiAchievement,
  kpiRatingLabel,
  kpiWeightedScore,
  scoreScorecard,
  sumKpiWeights,
  validateKpiRule,
  validateKpiWeights,
} from '../../core/utilities/kpi-calculation';
import { inspectEvidence, readFileBytes } from '../../core/utilities/kpi-evidence';
import { ScorecardRecord, nextRowVersion } from '../../mock-data/kpi/kpi-records';
import { MOCK_SCORECARDS } from '../../mock-data/kpi/scorecards.mock';
import { todayIso } from '../../shared/utilities/date.util';
import { matchesSearch, paginate, sortItems } from '../../shared/utilities/query.util';
import { ActivityMockService } from '../activity/activity-mock.service';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { mockError, mockResponse } from '../mock-response';
import { DepartmentMockService } from '../organization/organization-mock.services';
import { onDataReset, persistedSignal } from '../persistence';
import { initialRecords } from '../seed-mode';
import {
  EvaluationPeriodMockService,
  KpiCategoryMockService,
  KpiTemplateMockService,
  RatingBandMockService,
} from './kpi-setup-mock.services';

/** Same wording as the HTTP adapters use for a stale `rowVersion`. */
const STALE_MESSAGE = 'The record changed. Reload and try again.';
const COMMENT_MAX = 1000;
const MAX_ASSIGNMENT_ITEMS = 50;
const MAX_BULK_EMPLOYEES = 500;

const MANAGER_STAGE: readonly ScorecardStatus[] = ['Submitted', 'Resubmitted', 'Under Review'];

/** Role names the mock reads to decide who counts as HR or as a manager. Simulation only. */
const HR_OFFICER_ROLE = 'HR Officer';
const MANAGER_ROLE = 'Department Manager';

/** Who is acting, as the mock understands it. */
export interface KpiMockActor {
  userId: number;
  employeeId: number | null;
  name: string;
  /** HR tier: `kpi:Approve` with `employees:Edit` in the API. Sees every scorecard. */
  hr: boolean;
  /** `kpi:Approve` tier (managers and HR). */
  approver: boolean;
}

/** An assignment item after its template, defaults and dates are resolved. */
interface ResolvedItem {
  template: KpiTemplate;
  target: number;
  weight: number;
  startDate: string;
  endDate: string;
  sortOrder: number | null;
}

const ruleOf = (item: {
  measurementType: KpiRuleInput['measurementType'];
  direction: KpiRuleInput['direction'];
  scoringMethod: KpiRuleInput['scoringMethod'];
  target: number;
  rangeMin: number | null;
  rangeMax: number | null;
  maxRating: number | null;
  maxAchievementPct: number | null;
}): KpiRuleInput => ({
  measurementType: item.measurementType,
  direction: item.direction,
  scoringMethod: item.scoringMethod,
  target: item.target,
  rangeMin: item.rangeMin,
  rangeMax: item.rangeMax,
  maxRating: item.maxRating,
  maxAchievementPct: item.maxAchievementPct,
});

function decimalsOk(value: number, places: number): boolean {
  const scaled = value * 10 ** places;
  return Math.abs(scaled - Math.round(scaled)) < 1e-6;
}

const cleanComment = (value: string | null | undefined): string | null => {
  const text = (value ?? '').trim();
  return text === '' ? null : text;
};

/**
 * In-memory implementation of the scorecard contract: assignment, plan, results, submission,
 * review and evidence, with the same rules and messages as the API as closely as practical.
 *
 * DEMO ONLY, NO REAL SECURITY: "HR", "manager" and "own" come from the simulated session (its role
 * names and employee id). Nothing here is access control. Official scores come from the API; the
 * figures here use the mirror in `core/utilities/kpi-calculation.ts`.
 *
 * Differences from the API: the HR setup routes (`kpi-setup` permissions) are not gated by role;
 * `rowVersion` is a counter; evidence bytes live in memory and are lost on reload (the metadata
 * persists when persistence is on); Office files are recognised by entry names, not full ZIP checks.
 */
@Injectable({ providedIn: 'root' })
export class ScorecardMockService implements ScorecardDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly user = inject(CurrentUserService);
  private readonly activity = inject(ActivityMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly templates = inject(KpiTemplateMockService);
  private readonly categories = inject(KpiCategoryMockService);
  private readonly periods = inject(EvaluationPeriodMockService);
  private readonly bands = inject(RatingBandMockService);

  private readonly store = persistedSignal<ScorecardRecord[]>(
    'kpi_scorecards',
    () => structuredClone(initialRecords(MOCK_SCORECARDS)) as ScorecardRecord[],
  );
  /** Live read-only view, for the other KPI mock services (delete protection). */
  readonly records = this.store.asReadonly();

  /** Evidence bytes by attachment id. In memory only: a Blob cannot be saved in browser storage. */
  private readonly blobs = new Map<number, Blob>();

  constructor() {
    onDataReset(() => this.blobs.clear());
  }

  // ---- Plumbing -------------------------------------------------------------------------------

  private respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  private validation(message: string, fieldErrors?: Record<string, string>): never {
    throw mockError('Validation', message, fieldErrors);
  }

  private conflict(message: string): never {
    throw mockError('Conflict', message);
  }

  private actor(): KpiMockActor {
    const current = this.user.current();
    const roles = current.roles;
    const hr = roles.includes(ADMIN_ROLE_NAME) || roles.includes(HR_OFFICER_ROLE);
    return {
      userId: current.id,
      employeeId: current.employeeId !== null && current.employeeId > 0 ? current.employeeId : null,
      name: current.displayName,
      hr,
      approver: hr || roles.includes(MANAGER_ROLE),
    };
  }

  /** Row scope of reads: HR sees all, a manager sees own, direct reports and ones naming them reviewer, others own. */
  private inScope(record: ScorecardRecord, actor: KpiMockActor): boolean {
    if (actor.hr) {
      return true;
    }
    if (actor.employeeId === null) {
      return false;
    }
    if (record.employeeId === actor.employeeId) {
      return true;
    }
    if (!actor.approver) {
      return false;
    }
    return (
      record.reviewerEmployeeId === actor.employeeId ||
      this.employees.findById(record.employeeId)?.managerId === actor.employeeId
    );
  }

  /**
   * For the KPI dashboard and report mock: who is acting and the scorecards they may see, with the
   * same scope as every read here (HR all; a manager own, direct reports and the ones naming them
   * reviewer; anyone else own). Live records: do not change them.
   */
  scopeNow(): { actor: KpiMockActor; visible: readonly ScorecardRecord[] } {
    const actor = this.actor();
    return { actor, visible: this.store().filter((r) => this.inScope(r, actor)) };
  }

  private findRecord(id: number): ScorecardRecord {
    const record = this.store().find((r) => r.id === id);
    if (!record) {
      throw mockError('NotFound', `Scorecard #${id} was not found.`);
    }
    return record;
  }

  /** A scorecard the caller may see; one outside the scope is "not found". */
  private visible(id: number, actor: KpiMockActor): ScorecardRecord {
    const record = this.findRecord(id);
    if (!this.inScope(record, actor)) {
      throw mockError('NotFound', `Scorecard #${id} was not found.`);
    }
    return record;
  }

  private locateItem(itemId: number): { record: ScorecardRecord; item: ScorecardItem } {
    for (const record of this.store()) {
      const item = record.items.find((i) => i.id === itemId);
      if (item) {
        return { record, item };
      }
    }
    throw mockError('NotFound', `KPI #${itemId} was not found.`);
  }

  private locateAttachment(attachmentId: number): {
    record: ScorecardRecord;
    item: ScorecardItem;
  } {
    for (const record of this.store()) {
      const item = record.items.find((i) => i.attachments.some((a) => a.id === attachmentId));
      if (item) {
        return { record, item };
      }
    }
    throw mockError('NotFound', `Attachment #${attachmentId} was not found.`);
  }

  private replace(next: ScorecardRecord): void {
    this.store.update((items) => items.map((r) => (r.id === next.id ? next : r)));
  }

  private codeOf(record: ScorecardRecord): string {
    return this.employees.findById(record.employeeId)?.employeeCode ?? `#${record.employeeId}`;
  }

  private periodOf(record: ScorecardRecord): EvaluationPeriod {
    const period = this.periods.findById(record.periodId);
    if (!period) {
      this.conflict(`Period #${record.periodId} of this scorecard no longer exists.`);
    }
    return period;
  }

  /** `rowVersion` is required (400) and must be current (409). */
  private checkVersion(current: string, given: string | null | undefined): void {
    if (!given) {
      this.validation('The row version is required.', {
        rowVersion: 'The row version is required.',
      });
    }
    if (given !== current) {
      this.conflict(STALE_MESSAGE);
    }
  }

  private statusRefusal(record: ScorecardRecord, what: string): never {
    return this.conflict(`The scorecard is ${record.status}; ${what}.`);
  }

  private periodGate(
    record: ScorecardRecord,
    allowed: readonly EvaluationPeriodStatus[],
    what: string,
  ): EvaluationPeriod {
    const period = this.periodOf(record);
    if (!allowed.includes(period.status)) {
      this.conflict(`Period "${period.name}" is ${period.status}; ${what}.`);
    }
    return period;
  }

  private entry(
    step: ReviewStep,
    action: ReviewActionName,
    from: ScorecardStatus,
    to: ScorecardStatus,
    comment: string | null,
    score: number | null,
  ): ReviewAction {
    return {
      step,
      action,
      fromStatus: from,
      toStatus: to,
      actorUserId: this.actor().userId,
      actorName: this.actor().name,
      comment,
      overallScore: score,
      occurredAtUtc: new Date().toISOString(),
    };
  }

  private log(message: string): void {
    this.activity.record('performance', message);
  }

  private activeBands(): { minScore: number; label: string }[] {
    return this.bands.records().filter((b) => b.status === 'Active');
  }

  private checkComment(text: string | null | undefined, field: string): string | null {
    const cleaned = cleanComment(text);
    if (cleaned !== null && cleaned.length > COMMENT_MAX) {
      this.validation(`The comment must be at most ${COMMENT_MAX} characters.`, {
        [field]: `The comment must be at most ${COMMENT_MAX} characters.`,
      });
    }
    return cleaned;
  }

  // ---- DTOs -----------------------------------------------------------------------------------

  private summary(record: ScorecardRecord): Scorecard {
    const employee = this.employees.findById(record.employeeId);
    const reviewer =
      record.reviewerEmployeeId === null
        ? undefined
        : this.employees.findById(record.reviewerEmployeeId);
    return {
      id: record.id,
      employeeId: record.employeeId,
      employeeCode: employee?.employeeCode ?? `#${record.employeeId}`,
      employeeName: employee ? fullName(employee) : `Employee #${record.employeeId}`,
      periodId: record.periodId,
      periodName: this.periods.findById(record.periodId)?.name ?? `#${record.periodId}`,
      departmentId: record.departmentId,
      departmentName: this.departments.findById(record.departmentId)?.name ?? '—',
      status: record.status,
      itemCount: record.items.length,
      totalWeight: sumKpiWeights(record.items.map((i) => i.weight)),
      overallScore: record.overallScore,
      ratingLabel: record.ratingLabel,
      reviewerEmployeeId: record.reviewerEmployeeId,
      reviewerName: reviewer ? fullName(reviewer) : null,
      planFinalizedOn: record.planFinalizedOn,
      submittedOn: record.submittedOn,
      finalizedOn: record.finalizedOn,
      submissionDueDate: this.periods.findById(record.periodId)?.submissionDueDate ?? null,
      rowVersion: record.rowVersion,
    };
  }

  private detail(record: ScorecardRecord): ScorecardDetail {
    const period = this.periods.findById(record.periodId);
    return {
      ...this.summary(record),
      reviewedOn: record.reviewedOn,
      periodStatus: period?.status ?? 'Closed',
      requiresHrReview: period?.requiresHrReview ?? false,
      employeeComment: record.employeeComment,
      managerRating: record.managerRating,
      managerComment: record.managerComment,
      hrComment: record.hrComment,
      items: [...record.items].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id),
      history: [...record.history],
    };
  }

  // ---- Reads ----------------------------------------------------------------------------------

  private page(
    rows: ScorecardRecord[],
    request: PageRequest<ScorecardFilter>,
    defaultOrder: (a: Scorecard, b: Scorecard) => number,
  ): PagedResult<Scorecard> {
    const filters = request.filters ?? {};
    const summaries = rows
      .map((record) => this.summary(record))
      .filter(
        (row) =>
          matchesSearch(row, request.search, ['employeeCode', 'employeeName']) &&
          (!filters.periodId || row.periodId === filters.periodId) &&
          (!filters.employeeId || row.employeeId === filters.employeeId) &&
          (!filters.departmentId || row.departmentId === filters.departmentId) &&
          (!filters.companyId ||
            rows.find((r) => r.id === row.id)?.companyId === filters.companyId) &&
          (!filters.status || row.status === filters.status),
      );
    const ordered =
      request.sort?.active && request.sort.direction
        ? sortItems(summaries, request.sort)
        : [...summaries].sort(defaultOrder);
    return paginate(ordered, request.pageIndex, request.pageSize);
  }

  query(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.respond(() => {
      const actor = this.actor();
      return this.page(
        this.store().filter((r) => this.inScope(r, actor)),
        request,
        (a, b) => a.id - b.id,
      );
    });
  }

  getById(id: number): Observable<ScorecardDetail> {
    return this.respond(() => this.detail(this.visible(id, this.actor())));
  }

  myScorecards(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.respond(() => {
      const actor = this.actor();
      if (actor.employeeId === null) {
        return paginate([], request.pageIndex, request.pageSize);
      }
      const start = (periodId: number) => this.periods.findById(periodId)?.startDate ?? '';
      const own = this.store().filter(
        (r) => r.employeeId === actor.employeeId && r.status !== 'Planning',
      );
      const byPeriod = new Map(own.map((r) => [r.id, start(r.periodId)]));
      return this.page(
        own,
        request,
        (a, b) => (byPeriod.get(b.id) ?? '').localeCompare(byPeriod.get(a.id) ?? '') || b.id - a.id,
      );
    });
  }

  pendingReviews(request: PageRequest<ScorecardFilter>): Observable<PagedResult<Scorecard>> {
    return this.respond(() => {
      const actor = this.actor();
      if (!actor.approver) {
        return paginate([], request.pageIndex, request.pageSize);
      }
      const waiting = this.store().filter((r) => {
        if (r.employeeId === actor.employeeId) {
          return false; // never the caller's own scorecard
        }
        if (MANAGER_STAGE.includes(r.status)) {
          return actor.hr || r.reviewerEmployeeId === actor.employeeId;
        }
        // Approved ones wait for the HR final review, except those the caller approved.
        return r.status === 'Approved' && actor.hr && r.managerApprovedByUserId !== actor.userId;
      });
      const submitted = new Map(waiting.map((r) => [r.id, r.submittedOn ?? '']));
      return this.page(
        waiting,
        request,
        (a, b) =>
          (submitted.get(a.id) ?? '').localeCompare(submitted.get(b.id) ?? '') || a.id - b.id,
      );
    });
  }

  // ---- Assignment -----------------------------------------------------------------------------

  /** Request shape (400): field names are camel case, such as `items[0].weight`. */
  private checkAssignmentShape(items: readonly AssignmentItemInput[] | null | undefined): void {
    if (!items || items.length === 0) {
      this.validation('Choose at least one KPI to assign.', {
        items: 'Choose at least one KPI to assign.',
      });
    }
    if (items.length > MAX_ASSIGNMENT_ITEMS) {
      this.validation(`At most ${MAX_ASSIGNMENT_ITEMS} KPIs can be assigned in one request.`, {
        items: `At most ${MAX_ASSIGNMENT_ITEMS} KPIs can be assigned in one request.`,
      });
    }
    const errors: Record<string, string> = {};
    items.forEach((item, i) => {
      const at = (field: string, message: string) => (errors[`items[${i}].${field}`] ??= message);
      if (!(item.templateId > 0)) at('templateId', 'Choose a KPI template.');
      if (item.target !== undefined && item.target !== null) {
        if (item.target < 0) at('target', 'The target cannot be negative.');
        else if (!decimalsOk(item.target, 4)) at('target', 'Use at most 4 decimal places.');
      }
      if (item.weight !== undefined && item.weight !== null) {
        if (!(item.weight > 0 && item.weight <= 100)) {
          at('weight', 'The weight must be greater than 0 and at most 100.');
        } else if (!decimalsOk(item.weight, 2)) at('weight', 'Use at most 2 decimal places.');
      }
      if (
        item.sortOrder !== undefined &&
        item.sortOrder !== null &&
        !(item.sortOrder >= 0 && item.sortOrder <= 32767)
      ) {
        at('sortOrder', 'The sort order must be between 0 and 32767.');
      }
      if (item.startDate && item.endDate && item.endDate < item.startDate) {
        at('endDate', 'Must be on or after the start date.');
      }
    });
    if (Object.keys(errors).length > 0) {
      this.validation('Please correct the highlighted fields.', errors);
    }
  }

  private requireAssignablePeriod(periodId: number): EvaluationPeriod {
    const period = this.periods.findById(periodId);
    if (!period) {
      this.validation('The selected evaluation period does not exist.', {
        periodId: 'Choose an evaluation period.',
      });
    }
    if (period.status !== 'Draft' && period.status !== 'Active') {
      this.conflict(
        `Period "${period.name}" is ${period.status}; KPIs can only be assigned in a Draft or Active period.`,
      );
    }
    return period;
  }

  /** Why an employee cannot be assigned KPIs for the period, or null when they can. */
  private ineligible(employee: Employee, period: EvaluationPeriod): 'left' | 'company' | null {
    if (
      employee.exitDate !== null ||
      employee.employmentStatus === 'Resigned' ||
      employee.employmentStatus === 'Terminated'
    ) {
      return 'left';
    }
    return employee.companyId === period.companyId ? null : 'company';
  }

  /**
   * Looks up each template and resolves target, weight and dates. Request-level problems are a 400
   * on `items[i].<field>`; a department mismatch is left to the caller (a 400 for one employee, a
   * skip in a bulk run).
   */
  private resolveItems(
    period: EvaluationPeriod,
    items: readonly AssignmentItemInput[],
  ): ResolvedItem[] {
    const errors: Record<string, string> = {};
    const seen = new Set<number>();
    const resolved: ResolvedItem[] = [];
    items.forEach((item, i) => {
      const field = `items[${i}]`;
      const template = this.templates.findById(item.templateId);
      if (!template) {
        errors[`${field}.templateId`] = `KPI template #${item.templateId} was not found.`;
        return;
      }
      if (template.status !== 'Active') {
        errors[`${field}.templateId`] =
          `KPI template "${template.code}" is inactive and cannot be assigned.`;
        return;
      }
      if (seen.has(template.id)) {
        errors[`${field}.templateId`] =
          `KPI template "${template.code}" appears more than once in the request.`;
        return;
      }
      seen.add(template.id);
      const startDate = item.startDate ?? period.startDate;
      const endDate = item.endDate ?? period.endDate;
      const within = `Must be within the period (${period.startDate} to ${period.endDate}).`;
      if (startDate < period.startDate || startDate > period.endDate) {
        errors[`${field}.startDate`] = within;
      }
      if (endDate < period.startDate || endDate > period.endDate) {
        errors[`${field}.endDate`] = within;
      }
      if (endDate < startDate) {
        errors[`${field}.endDate`] = 'Must be on or after the start date.';
      }
      const target = item.target ?? template.defaultTarget;
      const rule = validateKpiRule({ ...ruleOf({ ...template, target }) });
      for (const [name, message] of Object.entries(rule)) {
        errors[`${field}.${name}`] = message;
      }
      resolved.push({
        template,
        target,
        weight: item.weight ?? template.defaultWeight,
        startDate,
        endDate,
        sortOrder: item.sortOrder ?? null,
      });
    });
    if (Object.keys(errors).length > 0) {
      this.validation('Please correct the highlighted fields.', errors);
    }
    return resolved;
  }

  private snapshotItem(resolved: ResolvedItem, id: number, sortOrder: number): ScorecardItem {
    const { template } = resolved;
    return {
      id,
      templateId: template.id,
      categoryId: template.categoryId,
      categoryName: this.categories.findById(template.categoryId)?.name ?? '—',
      kpiCode: template.code,
      kpiName: template.name,
      measurementType: template.measurementType,
      unit: template.unit,
      direction: template.direction,
      scoringMethod: template.scoringMethod,
      target: resolved.target,
      rangeMin: template.rangeMin,
      rangeMax: template.rangeMax,
      maxRating: template.maxRating,
      weight: resolved.weight,
      maxAchievementPct: template.maxAchievementPct,
      startDate: resolved.startDate,
      endDate: resolved.endDate,
      sortOrder,
      actualValue: null,
      employeeComment: null,
      achievementPct: null,
      weightedScore: null,
      managerRating: null,
      managerComment: null,
      rowVersion: '1',
      attachments: [],
    };
  }

  private nextItemId(extra = 0): number {
    return (
      this.store().reduce((max, r) => Math.max(max, ...r.items.map((i) => i.id), 0), 0) + 1 + extra
    );
  }

  /** The items a scorecard would get, in request order, after the highest sort order it has. */
  private newItems(
    resolved: readonly ResolvedItem[],
    existing: readonly ScorecardItem[],
    firstId: number,
  ): ScorecardItem[] {
    let order = existing.reduce((max, i) => Math.max(max, i.sortOrder), 0) + 1;
    return resolved.map((item, index) =>
      this.snapshotItem(item, firstId + index, item.sortOrder ?? order++),
    );
  }

  assign(request: AssignKpiRequest): Observable<AssignKpiResult> {
    return this.respond(() => {
      this.checkAssignmentShape(request.items);
      const period = this.requireAssignablePeriod(request.periodId);
      const employee = this.employees.findById(request.employeeId);
      if (!employee) {
        this.validation(`Employee #${request.employeeId} was not found.`, {
          employeeId: 'Choose an employee.',
        });
      }
      const problem = this.ineligible(employee, period);
      if (problem === 'left') {
        this.validation(
          `Employee ${employee.employeeCode} has left the company and cannot be assigned KPIs.`,
          { employeeId: 'Choose an employee who is still employed.' },
        );
      }
      if (problem === 'company') {
        this.validation(
          `Employee ${employee.employeeCode} does not belong to the company of period "${period.name}".`,
          { employeeId: "Choose an employee of the period's company." },
        );
      }
      const resolved = this.resolveItems(period, request.items);
      resolved.forEach((item, i) => {
        if (
          item.template.departmentId !== null &&
          item.template.departmentId !== employee.departmentId
        ) {
          this.validation(
            `KPI template "${item.template.code}" is for another department than the one of employee ${employee.employeeCode}.`,
            { [`items[${i}].templateId`]: 'This template is for another department.' },
          );
        }
      });

      const existing = this.store().find(
        (r) => r.employeeId === employee.id && r.periodId === period.id,
      );
      const current = existing?.items ?? [];
      const combined = sumKpiWeights([
        ...current.map((i) => i.weight),
        ...resolved.map((r) => r.weight),
      ]);
      if (combined > 100) {
        this.validation(
          `The KPI weights on the scorecard of ${employee.employeeCode} would add up to ${combined.toFixed(2)}%; they cannot exceed 100%.`,
          { items: 'The weights cannot add up to more than 100%.' },
        );
      }
      if (existing) {
        if (existing.status !== 'Planning') {
          this.conflict(
            `The scorecard of ${employee.employeeCode} is ${existing.status}; KPIs can only be assigned while it is in Planning.`,
          );
        }
        const duplicates = resolved
          .filter((r) => current.some((i) => i.templateId === r.template.id))
          .map((r) => r.template.code);
        if (duplicates.length > 0) {
          this.conflict(
            duplicates.length === 1
              ? `KPI ${duplicates[0]} is already assigned to the scorecard of ${employee.employeeCode}.`
              : `KPIs ${duplicates.join(', ')} are already assigned to the scorecard of ${employee.employeeCode}.`,
          );
        }
      }

      const added = this.newItems(resolved, current, this.nextItemId());
      let saved: ScorecardRecord;
      if (existing) {
        saved = { ...existing, items: [...current, ...added] };
        this.replace(saved);
      } else {
        saved = {
          id: this.store().reduce((max, r) => Math.max(max, r.id), 0) + 1,
          employeeId: employee.id,
          periodId: period.id,
          companyId: employee.companyId,
          branchId: employee.branchId,
          departmentId: employee.departmentId,
          positionId: employee.positionId,
          status: 'Planning',
          reviewerEmployeeId: null,
          planFinalizedOn: null,
          submittedOn: null,
          reviewedOn: null,
          finalizedOn: null,
          employeeComment: null,
          managerRating: null,
          managerComment: null,
          hrComment: null,
          overallScore: null,
          ratingLabel: null,
          managerApprovedByUserId: null,
          items: added,
          history: [],
          rowVersion: '1',
        };
        this.store.update((items) => [...items, saved]);
      }
      this.log(`${added.length} KPI(s) assigned to ${employee.employeeCode} for "${period.name}"`);
      return { scorecard: this.detail(saved), created: !existing };
    });
  }

  bulkAssign(request: BulkAssignKpiRequest): Observable<BulkAssignmentResult> {
    return this.respond(() => {
      const hasIds = request.employeeIds !== undefined && request.employeeIds !== null;
      const filterKeys = ['departmentId', 'branchId', 'positionId', 'jobGradeId'] as const;
      const hasFilters = filterKeys.some(
        (key) => request[key] !== undefined && request[key] !== null,
      );
      if (hasIds === hasFilters) {
        this.validation(
          'Choose the employees either with employeeIds or with filters (departmentId, branchId, positionId, jobGradeId), not both and not neither.',
          { employeeIds: 'Choose employees by id or by filters, not both.' },
        );
      }
      if (hasIds && request.employeeIds!.length === 0) {
        this.validation('Choose at least one employee.', {
          employeeIds: 'Choose at least one employee.',
        });
      }
      if (hasIds && request.employeeIds!.length > MAX_BULK_EMPLOYEES) {
        this.validation(`At most ${MAX_BULK_EMPLOYEES} employees can be assigned in one call.`, {
          employeeIds: `At most ${MAX_BULK_EMPLOYEES} employees can be assigned in one call.`,
        });
      }
      this.checkAssignmentShape(request.items);
      const period = this.requireAssignablePeriod(request.periodId);
      const resolved = this.resolveItems(period, request.items);

      // The employees of the call.
      let ids: number[];
      if (hasIds) {
        ids = [...new Set(request.employeeIds)];
      } else {
        const matches = this.employees
          .records()
          .filter(
            (e) =>
              e.companyId === period.companyId &&
              CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus) &&
              e.exitDate === null &&
              (request.departmentId == null || e.departmentId === request.departmentId) &&
              (request.branchId == null || e.branchId === request.branchId) &&
              (request.positionId == null || e.positionId === request.positionId) &&
              (request.jobGradeId == null || e.jobGradeId === request.jobGradeId),
          );
        if (matches.length > MAX_BULK_EMPLOYEES) {
          this.validation(
            `The filters match ${matches.length} employees; at most ${MAX_BULK_EMPLOYEES} can be assigned in one call.`,
            { filters: 'Narrow the filters.' },
          );
        }
        ids = matches.map((e) => e.id);
      }

      const assigned: BulkAssignedEmployee[] = [];
      const skipped: BulkSkippedEmployee[] = [];
      const created: ScorecardRecord[] = [];
      const updated = new Map<number, ScorecardRecord>();
      let nextCardId = this.store().reduce((max, r) => Math.max(max, r.id), 0) + 1;
      let nextItem = this.nextItemId();

      for (const id of ids) {
        const employee = this.employees.findById(id);
        if (!employee) {
          skipped.push({ employeeId: id, employeeCode: null, reason: 'Employee was not found.' });
          continue;
        }
        const skip = (reason: string) =>
          skipped.push({ employeeId: id, employeeCode: employee.employeeCode, reason });
        const problem = this.ineligible(employee, period);
        if (problem === 'left') {
          skip('Has left the company.');
          continue;
        }
        if (problem === 'company') {
          skip(`Belongs to another company than period "${period.name}".`);
          continue;
        }
        const mismatch = resolved.find(
          (r) =>
            r.template.departmentId !== null && r.template.departmentId !== employee.departmentId,
        );
        if (mismatch) {
          skip(`KPI template "${mismatch.template.code}" is for another department.`);
          continue;
        }
        const existing = this.store().find((r) => r.employeeId === id && r.periodId === period.id);
        if (existing && existing.status !== 'Planning') {
          skip(`The scorecard is ${existing.status}; KPIs can only be assigned in Planning.`);
          continue;
        }
        const current = existing?.items ?? [];
        const duplicate = resolved.find((r) => current.some((i) => i.templateId === r.template.id));
        if (duplicate) {
          skip(`KPI ${duplicate.template.code} is already assigned.`);
          continue;
        }
        const combined = sumKpiWeights([
          ...current.map((i) => i.weight),
          ...resolved.map((r) => r.weight),
        ]);
        if (combined > 100) {
          skip(`The KPI weights would add up to ${combined.toFixed(2)}%, which is more than 100%.`);
          continue;
        }
        const added = this.newItems(resolved, current, nextItem);
        nextItem += added.length;
        let saved: ScorecardRecord;
        if (existing) {
          saved = { ...existing, items: [...current, ...added] };
          updated.set(saved.id, saved);
        } else {
          saved = {
            id: nextCardId++,
            employeeId: employee.id,
            periodId: period.id,
            companyId: employee.companyId,
            branchId: employee.branchId,
            departmentId: employee.departmentId,
            positionId: employee.positionId,
            status: 'Planning',
            reviewerEmployeeId: null,
            planFinalizedOn: null,
            submittedOn: null,
            reviewedOn: null,
            finalizedOn: null,
            employeeComment: null,
            managerRating: null,
            managerComment: null,
            hrComment: null,
            overallScore: null,
            ratingLabel: null,
            managerApprovedByUserId: null,
            items: added,
            history: [],
            rowVersion: '1',
          };
          created.push(saved);
        }
        assigned.push({
          employeeId: id,
          employeeCode: employee.employeeCode,
          scorecardId: saved.id,
          itemsAdded: added.length,
        });
      }

      // Everything that passed is written together.
      if (assigned.length > 0) {
        this.store.update((items) => [...items.map((r) => updated.get(r.id) ?? r), ...created]);
        this.log(
          `KPIs assigned in bulk for "${period.name}": ${assigned.length} employee(s) assigned, ${skipped.length} skipped`,
        );
      }
      return { assigned, skipped };
    });
  }

  // ---- Plan -----------------------------------------------------------------------------------

  /** Totals of the other items plus this one, at most 100 (400 on `weight`). */
  private checkItemWeights(record: ScorecardRecord, itemId: number, weight: number): void {
    const total = sumKpiWeights([
      ...record.items.filter((i) => i.id !== itemId).map((i) => i.weight),
      weight,
    ]);
    if (total > 100) {
      this.validation(
        `The KPI weights on the scorecard would add up to ${total.toFixed(2)}%; they cannot exceed 100%.`,
        { weight: 'The weights cannot add up to more than 100%.' },
      );
    }
  }

  updateItem(itemId: number, changes: ScorecardItemUpdate): Observable<ScorecardItem> {
    return this.respond(() => {
      const { record, item } = this.locateItem(itemId);
      const errors: Record<string, string> = {};
      if (!(changes.target >= 0)) errors['target'] = 'The target cannot be negative.';
      else if (!decimalsOk(changes.target, 4)) errors['target'] = 'Use at most 4 decimal places.';
      if (!(changes.weight > 0 && changes.weight <= 100)) {
        errors['weight'] = 'The weight must be greater than 0 and at most 100.';
      } else if (!decimalsOk(changes.weight, 2)) errors['weight'] = 'Use at most 2 decimal places.';
      if (!changes.startDate) errors['startDate'] = 'Start date is required.';
      if (!changes.endDate) errors['endDate'] = 'End date is required.';
      else if (changes.endDate < changes.startDate)
        errors['endDate'] = 'Must be on or after the start date.';
      if (
        changes.sortOrder !== undefined &&
        changes.sortOrder !== null &&
        !(changes.sortOrder >= 0 && changes.sortOrder <= 32767)
      ) {
        errors['sortOrder'] = 'The sort order must be between 0 and 32767.';
      }
      if (Object.keys(errors).length > 0) {
        this.validation('Please correct the highlighted fields.', errors);
      }
      this.checkVersion(item.rowVersion, changes.rowVersion);
      if (record.status !== 'Planning') {
        this.statusRefusal(record, 'its KPIs can only be changed while it is in Planning');
      }
      const period = this.periodGate(
        record,
        ['Draft', 'Active'],
        'KPIs can only be assigned in a Draft or Active period',
      );
      const within = `Must be within the period (${period.startDate} to ${period.endDate}).`;
      if (changes.startDate < period.startDate || changes.startDate > period.endDate) {
        this.validation(within, { startDate: within });
      }
      if (changes.endDate < period.startDate || changes.endDate > period.endDate) {
        this.validation(within, { endDate: within });
      }
      const ruleErrors = validateKpiRule(ruleOf({ ...item, target: changes.target }));
      if (Object.keys(ruleErrors).length > 0) {
        this.validation('The KPI rule is not valid.', ruleErrors);
      }
      this.checkItemWeights(record, item.id, changes.weight);

      const next: ScorecardItem = {
        ...item,
        target: changes.target,
        weight: changes.weight,
        startDate: changes.startDate,
        endDate: changes.endDate,
        sortOrder: changes.sortOrder ?? item.sortOrder,
        rowVersion: nextRowVersion(item.rowVersion),
      };
      this.replace({ ...record, items: record.items.map((i) => (i.id === item.id ? next : i)) });
      this.log(`KPI "${item.kpiCode}" of ${this.codeOf(record)} for "${period.name}" updated`);
      return next;
    });
  }

  deleteItem(itemId: number, rowVersion: string): Observable<void> {
    return this.respond(() => {
      const { record, item } = this.locateItem(itemId);
      this.checkVersion(item.rowVersion, rowVersion);
      if (record.status !== 'Planning') {
        this.statusRefusal(record, 'its KPIs can only be changed while it is in Planning');
      }
      this.replace({ ...record, items: record.items.filter((i) => i.id !== item.id) });
      this.log(
        `KPI "${item.kpiCode}" removed from ${this.codeOf(record)} for "${this.periods.findById(record.periodId)?.name}"`,
      );
    });
  }

  deleteScorecard(id: number, rowVersion: string): Observable<void> {
    return this.respond(() => {
      const record = this.findRecord(id);
      this.checkVersion(record.rowVersion, rowVersion);
      if (record.status !== 'Planning') {
        this.statusRefusal(record, 'it can only be deleted while it is in Planning');
      }
      if (record.history.length > 0) {
        this.conflict('The scorecard has a review history and cannot be deleted.');
      }
      this.store.update((items) => items.filter((r) => r.id !== id));
      this.log(
        `Scorecard of ${this.codeOf(record)} for "${this.periods.findById(record.periodId)?.name}" deleted`,
      );
    });
  }

  finalizePlan(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.respond(() => {
      const record = this.findRecord(id);
      this.checkVersion(record.rowVersion, rowVersion);
      if (record.status !== 'Planning') {
        this.statusRefusal(record, 'its plan can only be finalized while it is in Planning');
      }
      const period = this.periodGate(
        record,
        ['Draft', 'Active'],
        'a plan can only be finalized in a Draft or Active period',
      );
      const weights = validateKpiWeights(record.items.map((i) => i.weight));
      if (!weights.ok) {
        this.validation(weights.message, weights.fieldErrors);
      }
      // The reviewer is the employee's CURRENT manager, fixed from now on.
      const managerId = this.employees.findById(record.employeeId)?.managerId ?? null;
      if (managerId === record.employeeId) {
        this.conflict(
          'The employee is their own manager, so nobody can review this scorecard. Correct the manager on the employee record first.',
        );
      }
      const saved: ScorecardRecord = {
        ...record,
        status: 'Draft',
        reviewerEmployeeId: managerId,
        planFinalizedOn: todayIso(),
        history: [
          ...record.history,
          this.entry('HR', 'Finalize Plan', 'Planning', 'Draft', null, null),
        ],
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(`KPI plan of ${this.codeOf(record)} for "${period.name}" finalized`);
      return this.detail(saved);
    });
  }

  // ---- Employee: results and submission -------------------------------------------------------

  /** Only the scorecard's own employee; someone else who can see it gets 403. */
  private requireOwner(record: ScorecardRecord, actor: KpiMockActor, message: string): void {
    if (actor.employeeId === null || record.employeeId !== actor.employeeId) {
      throw mockError('Forbidden', message);
    }
  }

  private rescoreItem(item: ScorecardItem): void {
    if (item.actualValue === null) {
      item.achievementPct = null;
      item.weightedScore = null;
      return;
    }
    const result = kpiAchievement(ruleOf(item), item.actualValue);
    item.achievementPct = result.ok ? result.value : null;
    item.weightedScore = result.ok ? kpiWeightedScore(result.value, item.weight) : null;
  }

  /** Each item of the request belongs to the scorecard and appears once (400 on `items[i].id`). */
  private checkRequestItems(
    record: ScorecardRecord,
    items: readonly { id: number }[] | null | undefined,
    what: string,
  ): void {
    if (!items) {
      this.validation(`Send the KPI ${what} in items.`, {
        items: `Send the KPI ${what} in items.`,
      });
    }
    const seen = new Set<number>();
    const errors: Record<string, string> = {};
    items.forEach((item, i) => {
      if (seen.has(item.id)) {
        errors[`items[${i}].id`] = 'A KPI can appear only once.';
      } else if (!record.items.some((x) => x.id === item.id)) {
        errors[`items[${i}].id`] = 'This KPI is not on the scorecard.';
      }
      seen.add(item.id);
    });
    if (Object.keys(errors).length > 0) {
      this.validation('Please correct the highlighted fields.', errors);
    }
  }

  saveResults(id: number, request: SaveResultsRequest): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireOwner(record, actor, 'You can only enter results on your own scorecard.');
      this.checkVersion(record.rowVersion, request.rowVersion);
      if (record.status !== 'Draft' && record.status !== 'Returned') {
        this.statusRefusal(record, 'results can only be entered while it is Draft or Returned');
      }
      const period = this.periodGate(
        record,
        ['Active'],
        'results can only be entered and submitted while the period is Active',
      );
      this.checkRequestItems(record, request.items, 'results');
      const errors: Record<string, string> = {};
      request.items.forEach((entry: ResultItemInput, i) => {
        const item = record.items.find((x) => x.id === entry.id)!;
        const value = entry.actualValue ?? null;
        if (value !== null) {
          if (!decimalsOk(value, 4)) {
            errors[`items[${i}].actualValue`] = 'Use at most 4 decimal places.';
          } else {
            const result = kpiAchievement(ruleOf(item), value);
            if (!result.ok) {
              errors[`items[${i}].actualValue`] =
                result.fieldErrors['actualValue'] ?? result.message;
            }
          }
        }
        const comment = cleanComment(entry.employeeComment);
        if (comment !== null && comment.length > COMMENT_MAX) {
          errors[`items[${i}].employeeComment`] =
            `The comment must be at most ${COMMENT_MAX} characters.`;
        }
      });
      const overallComment =
        request.employeeComment === undefined
          ? record.employeeComment
          : this.checkComment(request.employeeComment, 'employeeComment');
      if (Object.keys(errors).length > 0) {
        this.validation('Please correct the highlighted fields.', errors);
      }

      const items = record.items.map((item) => {
        const entry = request.items.find((x) => x.id === item.id);
        if (!entry) {
          return item;
        }
        const next: ScorecardItem = {
          ...item,
          actualValue: entry.actualValue ?? null,
          employeeComment: cleanComment(entry.employeeComment),
        };
        this.rescoreItem(next);
        return next;
      });
      // A draft: the scorecard gets no overall score until it is submitted.
      const saved: ScorecardRecord = {
        ...record,
        items,
        employeeComment: overallComment,
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(`Results saved on the scorecard of ${this.codeOf(record)} for "${period.name}"`);
      return this.detail(saved);
    });
  }

  /** Scores the stored results with the calculator; a problem is a 400 on `items[i].actualValue`. */
  private scoreOf(record: ScorecardRecord): { overall: number; items: ScorecardItem[] } {
    const sorted = [...record.items].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
    const result = scoreScorecard(
      sorted.map((item) => ({ rule: ruleOf(item), weight: item.weight, actual: item.actualValue })),
    );
    if (!result.ok) {
      this.validation(result.message, result.fieldErrors);
    }
    const scored = new Map(
      sorted.map((item, i) => [
        item.id,
        { ...item, ...result.value.items[i], rowVersion: item.rowVersion },
      ]),
    );
    return {
      overall: result.value.overallScore,
      items: record.items.map((item) => {
        const next = scored.get(item.id)!;
        return { ...item, achievementPct: next.achievementPct, weightedScore: next.weightedScore };
      }),
    };
  }

  submit(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireOwner(record, actor, 'You can only submit your own scorecard.');
      this.checkVersion(record.rowVersion, rowVersion);
      if (record.status !== 'Draft' && record.status !== 'Returned') {
        this.statusRefusal(record, 'only a Draft or Returned scorecard can be submitted');
      }
      const period = this.periodGate(
        record,
        ['Active'],
        'results can only be entered and submitted while the period is Active',
      );
      const { overall, items } = this.scoreOf(record);
      const again = record.status === 'Returned';
      const target: ScorecardStatus = again ? 'Resubmitted' : 'Submitted';
      const saved: ScorecardRecord = {
        ...record,
        status: target,
        items,
        overallScore: overall,
        ratingLabel: kpiRatingLabel(overall, this.activeBands()),
        submittedOn: todayIso(),
        history: [
          ...record.history,
          this.entry(
            'Employee',
            again ? 'Resubmit' : 'Submit',
            record.status,
            target,
            null,
            overall,
          ),
        ],
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(
        `Scorecard of ${this.codeOf(record)} for "${period.name}" ${again ? 'resubmitted' : 'submitted'}`,
      );
      return this.detail(saved);
    });
  }

  // ---- Review ---------------------------------------------------------------------------------

  /** Nobody reviews their own scorecard, whatever their role (409). */
  private refuseOwn(record: ScorecardRecord, actor: KpiMockActor): void {
    if (actor.employeeId !== null && record.employeeId === actor.employeeId) {
      this.conflict('You cannot review your own scorecard.');
    }
  }

  /** Manager stage: the scorecard's reviewer or the HR tier (403 for anyone else). */
  private requireManagerStage(record: ScorecardRecord, actor: KpiMockActor): void {
    this.refuseOwn(record, actor);
    if (!actor.approver) {
      throw mockError('Forbidden', 'You do not have permission to do that.');
    }
    if (!actor.hr && record.reviewerEmployeeId !== actor.employeeId) {
      throw mockError('Forbidden', "Only the employee's reviewer or HR can review this scorecard.");
    }
  }

  /** HR stage: the HR tier, and not the user who recorded the manager-stage approval. */
  private requireHrStage(
    record: ScorecardRecord,
    actor: KpiMockActor,
    checkSeparation: boolean,
  ): void {
    this.refuseOwn(record, actor);
    if (!actor.hr) {
      throw mockError('Forbidden', 'Only HR can give the final review of a scorecard.');
    }
    if (checkSeparation && record.managerApprovedByUserId === actor.userId) {
      this.conflict('The person who approved this scorecard cannot also give the HR final review.');
    }
  }

  private reviewGate(record: ScorecardRecord): EvaluationPeriod {
    return this.periodGate(
      record,
      ['Active', 'Completed'],
      'a scorecard can only be reviewed while its period is Active or Completed',
    );
  }

  startReview(id: number, rowVersion: string): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireManagerStage(record, actor);
      this.checkVersion(record.rowVersion, rowVersion);
      const period = this.reviewGate(record);
      if (record.status !== 'Submitted' && record.status !== 'Resubmitted') {
        this.statusRefusal(
          record,
          'only a Submitted or Resubmitted scorecard can be put under review',
        );
      }
      const saved: ScorecardRecord = {
        ...record,
        status: 'Under Review',
        history: [
          ...record.history,
          this.entry(
            'Manager',
            'Start Review',
            record.status,
            'Under Review',
            null,
            record.overallScore,
          ),
        ],
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(`Review of the scorecard of ${this.codeOf(record)} for "${period.name}" started`);
      return this.detail(saved);
    });
  }

  saveReview(id: number, request: SaveReviewRequest): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireManagerStage(record, actor);
      this.checkVersion(record.rowVersion, request.rowVersion);
      const period = this.reviewGate(record);
      if (!MANAGER_STAGE.includes(record.status)) {
        this.statusRefusal(
          record,
          'a review can only be saved while it is Submitted, Resubmitted or Under Review',
        );
      }
      this.checkRequestItems(record, request.items, 'ratings');
      const errors: Record<string, string> = {};
      const checkRating = (field: string, value: number | null | undefined): void => {
        if (value === undefined || value === null) {
          return;
        }
        if (!(value >= 1 && value <= 5)) {
          errors[field] = 'The rating must be between 1 and 5.';
        } else if (!decimalsOk(value, 2)) {
          errors[field] = 'Use at most 2 decimal places.';
        }
      };
      checkRating('managerRating', request.managerRating);
      request.items.forEach((entry: ReviewItemInput, i) => {
        checkRating(`items[${i}].managerRating`, entry.managerRating);
        const comment = cleanComment(entry.managerComment);
        if (comment !== null && comment.length > COMMENT_MAX) {
          errors[`items[${i}].managerComment`] =
            `The comment must be at most ${COMMENT_MAX} characters.`;
        }
      });
      const managerComment = this.checkComment(request.managerComment, 'managerComment');
      if (Object.keys(errors).length > 0) {
        this.validation('Please correct the highlighted fields.', errors);
      }
      const saved: ScorecardRecord = {
        ...record,
        managerRating: request.managerRating ?? null,
        managerComment,
        items: record.items.map((item) => {
          const entry = request.items.find((x) => x.id === item.id);
          return entry
            ? {
                ...item,
                managerRating: entry.managerRating ?? null,
                managerComment: cleanComment(entry.managerComment),
              }
            : item;
        }),
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(
        `Review notes saved on the scorecard of ${this.codeOf(record)} for "${period.name}"`,
      );
      return this.detail(saved);
    });
  }

  approve(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireManagerStage(record, actor);
      this.checkVersion(record.rowVersion, decision.rowVersion);
      const period = this.reviewGate(record);
      if (!MANAGER_STAGE.includes(record.status)) {
        this.statusRefusal(
          record,
          'only a Submitted, Resubmitted or Under Review scorecard can be approved',
        );
      }
      const comment = this.checkComment(decision.comment, 'comment');
      // Recalculated from the stored results, never from a figure sent by the client.
      const { overall, items } = this.scoreOf(record);
      const target: ScorecardStatus = period.requiresHrReview ? 'Approved' : 'Finalized';
      const today = todayIso();
      const saved: ScorecardRecord = {
        ...record,
        status: target,
        items,
        overallScore: overall,
        ratingLabel: kpiRatingLabel(overall, this.activeBands()),
        reviewedOn: today,
        finalizedOn: target === 'Finalized' ? today : record.finalizedOn,
        managerApprovedByUserId: actor.userId,
        history: [
          ...record.history,
          this.entry('Manager', 'Approve', record.status, target, comment, overall),
        ],
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(
        `Scorecard of ${this.codeOf(record)} for "${period.name}" approved${target === 'Finalized' ? ' and finalized' : ', waiting for the HR review'}`,
      );
      return this.detail(saved);
    });
  }

  /** Return and reject: the same rules, a different target status. */
  private turn(
    id: number,
    decision: ReviewDecision,
    action: 'Return' | 'Reject',
  ): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      const hrStage = record.status === 'Approved';
      if (hrStage) {
        this.requireHrStage(record, actor, false);
      } else {
        this.requireManagerStage(record, actor);
      }
      this.checkVersion(record.rowVersion, decision.rowVersion);
      const period = this.reviewGate(record);
      if (!MANAGER_STAGE.includes(record.status) && !hrStage) {
        this.statusRefusal(
          record,
          `only a Submitted, Resubmitted, Under Review or Approved scorecard can be ${action === 'Return' ? 'returned' : 'rejected'}`,
        );
      }
      if (hrStage) {
        this.requireHrStage(record, actor, true);
      }
      const reason = cleanComment(decision.comment);
      if (reason === null) {
        this.validation('A reason is required.', { comment: 'A reason is required.' });
      }
      if (reason.length > COMMENT_MAX) {
        this.validation(`The comment must be at most ${COMMENT_MAX} characters.`, {
          comment: `The comment must be at most ${COMMENT_MAX} characters.`,
        });
      }
      const target: ScorecardStatus = action === 'Return' ? 'Returned' : 'Rejected';
      const entry = this.entry(
        hrStage ? 'HR' : 'Manager',
        action,
        record.status,
        target,
        reason,
        record.overallScore,
      );
      const saved: ScorecardRecord =
        action === 'Return'
          ? {
              ...record,
              status: target,
              overallScore: null,
              ratingLabel: null,
              reviewedOn: null,
              history: [...record.history, entry],
              rowVersion: nextRowVersion(record.rowVersion),
            }
          : {
              ...record,
              status: target,
              history: [...record.history, entry],
              rowVersion: nextRowVersion(record.rowVersion),
            };
      this.replace(saved);
      this.log(
        action === 'Return'
          ? `Scorecard of ${this.codeOf(record)} for "${period.name}" returned to the employee`
          : `Scorecard of ${this.codeOf(record)} for "${period.name}" rejected`,
      );
      return this.detail(saved);
    });
  }

  returnForRevision(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.turn(id, decision, 'Return');
  }

  reject(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.turn(id, decision, 'Reject');
  }

  finalize(id: number, decision: ReviewDecision): Observable<ScorecardDetail> {
    return this.respond(() => {
      const actor = this.actor();
      const record = this.visible(id, actor);
      this.requireHrStage(record, actor, false);
      this.checkVersion(record.rowVersion, decision.rowVersion);
      const period = this.reviewGate(record);
      if (record.status !== 'Approved') {
        this.statusRefusal(record, 'only an Approved scorecard can be finalized by HR');
      }
      this.requireHrStage(record, actor, true);
      const comment = this.checkComment(decision.comment, 'comment');
      const saved: ScorecardRecord = {
        ...record,
        status: 'Finalized',
        finalizedOn: todayIso(),
        hrComment: comment,
        history: [
          ...record.history,
          this.entry('HR', 'Finalize', 'Approved', 'Finalized', comment, record.overallScore),
        ],
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.log(`Scorecard of ${this.codeOf(record)} for "${period.name}" finalized by HR`);
      return this.detail(saved);
    });
  }

  // ---- Evidence -------------------------------------------------------------------------------

  /** Owner, version, status and period checks shared by adding and removing evidence. */
  private checkEvidenceWrite(
    record: ScorecardRecord,
    actor: KpiMockActor,
    rowVersion: string,
  ): EvaluationPeriod {
    this.requireOwner(record, actor, 'You can only add or remove evidence on your own scorecard.');
    this.checkVersion(record.rowVersion, rowVersion);
    if (record.status !== 'Draft' && record.status !== 'Returned') {
      this.statusRefusal(
        record,
        'evidence can only be added or removed while it is Draft or Returned',
      );
    }
    return this.periodGate(
      record,
      ['Active'],
      'evidence can only be added or removed while the period is Active',
    );
  }

  uploadEvidence(itemId: number, rowVersion: string, file: File): Observable<EvidenceUploadResult> {
    return from(readFileBytes(file)).pipe(
      switchMap((bytes) =>
        this.respond(() => {
          const actor = this.actor();
          const located = this.locateItem(itemId);
          const record = this.visible(located.record.id, actor);
          const period = this.checkEvidenceWrite(record, actor, rowVersion);
          const item = record.items.find((i) => i.id === itemId)!;
          if (item.attachments.length >= EVIDENCE_MAX_PER_KPI) {
            this.conflict(
              `A KPI can have at most ${EVIDENCE_MAX_PER_KPI} evidence files. Remove one before adding another.`,
            );
          }
          const inspected = inspectEvidence(file.name, bytes);
          if (!inspected.ok) {
            this.validation(inspected.message, { file: inspected.message });
          }
          const id =
            this.store().reduce(
              (max, r) =>
                Math.max(max, ...r.items.flatMap((i) => i.attachments.map((a) => a.id)), 0),
              0,
            ) + 1;
          const attachment = {
            id,
            fileName: inspected.fileName,
            contentType: inspected.contentType,
            sizeBytes: bytes.length,
            uploadedOn: todayIso(),
          };
          this.blobs.set(id, new Blob([new Uint8Array(bytes)], { type: inspected.contentType }));
          const saved: ScorecardRecord = {
            ...record,
            items: record.items.map((i) =>
              i.id === itemId ? { ...i, attachments: [...i.attachments, attachment] } : i,
            ),
            rowVersion: nextRowVersion(record.rowVersion),
          };
          this.replace(saved);
          // Never the file name: the activity feed names the scorecard only.
          this.log(`Evidence added to the scorecard of ${this.codeOf(record)} (${period.name})`);
          return { attachment, scorecardRowVersion: saved.rowVersion };
        }),
      ),
    );
  }

  downloadEvidence(attachmentId: number): Observable<EvidenceDownload> {
    // Not through `respond`: that deep-clones the result, and a Blob is handed on as it is.
    return timer(this.latency).pipe(
      map(() => {
        const actor = this.actor();
        const { record, item } = this.locateAttachment(attachmentId);
        this.visible(record.id, actor);
        const attachment = item.attachments.find((a) => a.id === attachmentId)!;
        const blob = this.blobs.get(attachmentId);
        if (!blob) {
          throw mockError('NotFound', 'The file of this attachment is not available.');
        }
        return { blob, fileName: attachment.fileName };
      }),
    );
  }

  deleteEvidence(attachmentId: number, rowVersion: string): Observable<EvidenceRemovalResult> {
    return this.respond(() => {
      const actor = this.actor();
      const { record: located } = this.locateAttachment(attachmentId);
      const record = this.visible(located.id, actor);
      const period = this.checkEvidenceWrite(record, actor, rowVersion);
      const saved: ScorecardRecord = {
        ...record,
        items: record.items.map((i) => ({
          ...i,
          attachments: i.attachments.filter((a) => a.id !== attachmentId),
        })),
        rowVersion: nextRowVersion(record.rowVersion),
      };
      this.replace(saved);
      this.blobs.delete(attachmentId);
      this.log(`Evidence removed from the scorecard of ${this.codeOf(record)} (${period.name})`);
      return { scorecardRowVersion: saved.rowVersion };
    });
  }
}
