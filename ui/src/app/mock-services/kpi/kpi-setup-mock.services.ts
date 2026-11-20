import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Entity } from '../../core/models/common.models';
import {
  EvaluationPeriod,
  EvaluationPeriodFilter,
  EvaluationPeriodOption,
  EvaluationPeriodStatus,
  KPI_DIRECTIONS,
  KPI_FREQUENCIES,
  KPI_MEASUREMENT_TYPES,
  KPI_SCORING_METHODS,
  KpiCategory,
  KpiCategoryFilter,
  KpiCategoryOption,
  KpiTemplate,
  KpiTemplateFilter,
  RatingBand,
  RatingBandFilter,
} from '../../core/models/kpi.models';
import {
  EvaluationPeriodDataService,
  KpiCategoryDataService,
  KpiTemplateDataService,
  RatingBandDataService,
} from '../../core/services/data/kpi-data.services';
import { validateKpiRule } from '../../core/utilities/kpi-calculation';
import {
  KPI_STATUSES_BLOCKING_CLOSE,
  periodCloseRefusal,
} from '../../core/utilities/kpi-period-rules';
import { MOCK_EVALUATION_PERIODS } from '../../mock-data/kpi/evaluation-periods.mock';
import { MOCK_KPI_CATEGORIES } from '../../mock-data/kpi/kpi-categories.mock';
import { nextRowVersion } from '../../mock-data/kpi/kpi-records';
import { MOCK_KPI_TEMPLATES } from '../../mock-data/kpi/kpi-templates.mock';
import { DEFAULT_RATING_BANDS } from '../../mock-data/kpi/rating-bands.mock';
import { matchesEqualityFilters } from '../../shared/utilities/query.util';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { mockResponse } from '../mock-response';
import {
  CompanyMockService,
  DepartmentMockService,
} from '../organization/organization-mock.services';
import { ScorecardMockService } from './scorecard-mock.service';

/** Same wording as the HTTP adapters use for a stale `rowVersion`. */
const STALE_MESSAGE = 'The record changed. Reload and try again.';

const CODE_PATTERN = /^[A-Z0-9-]+$/;
const CODE_MESSAGE = 'Code must use capital letters, numbers and dashes only.';

/** True when the value has at most `places` decimals (tolerates float noise). */
function hasAtMostDecimals(value: number, places: number): boolean {
  const scaled = value * 10 ** places;
  return Math.abs(scaled - Math.round(scaled)) < 1e-6;
}

/**
 * Master-data CRUD with the API's concurrency emulation: a new record starts at version "1" and an
 * update that carries an older `rowVersion` than the stored one is a conflict. The mock enforces no
 * real security: permissions (`kpi-setup`) are not checked here.
 */
abstract class VersionedMockCrud<
  T extends Entity & { rowVersion?: string },
  TFilter extends object = Record<string, unknown>,
> extends InMemoryCrudService<T, T, TFilter> {
  protected override prepare(candidate: T, isNew: boolean): void {
    const target = candidate as { rowVersion?: string };
    if (isNew) {
      target.rowVersion = nextRowVersion(undefined);
      return;
    }
    const current = this.findById(candidate.id);
    if (!current) {
      return;
    }
    if (target.rowVersion !== current.rowVersion) {
      this.conflictError(STALE_MESSAGE);
    }
    target.rowVersion = nextRowVersion(current.rowVersion);
  }

  /** Code: required, at most 20 characters, capital letters, digits and dashes. */
  protected checkCode(code: string): void {
    const text = (code ?? '').trim();
    if (text === '') {
      this.validationError('Code is required.', { code: 'Code is required.' });
    }
    if (text.length > 20) {
      this.validationError('Code must be at most 20 characters.', {
        code: 'Use at most 20 characters.',
      });
    }
    if (!CODE_PATTERN.test(text)) {
      this.validationError(CODE_MESSAGE, { code: CODE_MESSAGE });
    }
  }

  protected checkText(
    field: string,
    label: string,
    value: string | null | undefined,
    max: number,
    required: boolean,
  ): void {
    const text = (value ?? '').trim();
    if (required && text === '') {
      this.validationError(`${label} is required.`, { [field]: `${label} is required.` });
    }
    if (text.length > max) {
      this.validationError(`${label} must be at most ${max} characters.`, {
        [field]: `Use at most ${max} characters.`,
      });
    }
  }

  /** Trims the text fields the API trims. */
  protected trimFields(candidate: T, fields: readonly string[]): void {
    const record = candidate as unknown as Record<string, unknown>;
    for (const field of fields) {
      if (typeof record[field] === 'string') {
        record[field] = (record[field] as string).trim();
      }
    }
  }
}

// ---- Categories -----------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class KpiCategoryMockService
  extends VersionedMockCrud<KpiCategory, KpiCategoryFilter>
  implements KpiCategoryDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'KPI category';
  protected readonly activityCategory = 'performance';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_KPI_CATEGORIES, 'kpi_categories');
  }

  protected override describe(entity: KpiCategory): string {
    return entity.name;
  }

  /** The category picker: every category, inactive ones included, by name then id. */
  options(): Observable<KpiCategoryOption[]> {
    return mockResponse(
      () =>
        this.store()
          .map(({ id, code, name, status }): KpiCategoryOption => ({ id, code, name, status }))
          .sort((a, b) => a.name.localeCompare(b.name) || a.id - b.id),
      this.latency,
    );
  }

  protected override prepare(candidate: KpiCategory, isNew: boolean): void {
    this.trimFields(candidate, ['code', 'name', 'description']);
    super.prepare(candidate, isNew);
  }

  protected override validate(candidate: KpiCategory): void {
    this.checkCode(candidate.code);
    this.checkText('name', 'Category name', candidate.name, 100, true);
    this.checkText('description', 'Description', candidate.description, 300, false);
    this.ensureUnique(candidate, 'code', 'KPI category code');
    this.ensureUnique(candidate, 'name', 'KPI category name');
  }

  protected override beforeDelete(entity: KpiCategory): void {
    const templates = this.injector
      .get(KpiTemplateMockService)
      .records()
      .filter((t) => t.categoryId === entity.id).length;
    const assigned = this.injector
      .get(ScorecardMockService)
      .records()
      .reduce((n, card) => n + card.items.filter((i) => i.categoryId === entity.id).length, 0);
    this.ensureNotReferenced(entity.name, [
      ['KPI template(s)', templates],
      ['assigned KPI(s)', assigned],
    ]);
  }
}

// ---- Templates ------------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class KpiTemplateMockService
  extends VersionedMockCrud<KpiTemplate, KpiTemplateFilter>
  implements KpiTemplateDataService
{
  private readonly injector = inject(Injector);
  private readonly categories = inject(KpiCategoryMockService);
  private readonly departments = inject(DepartmentMockService);
  protected readonly entityLabel = 'KPI template';
  protected readonly activityCategory = 'performance';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_KPI_TEMPLATES, 'kpi_templates');
  }

  protected override describe(entity: KpiTemplate): string {
    return entity.code;
  }

  protected override prepare(candidate: KpiTemplate, isNew: boolean): void {
    this.trimFields(candidate, ['code', 'name', 'description']);
    candidate.unit = candidate.unit?.trim() || null;
    super.prepare(candidate, isNew);
  }

  protected override validate(candidate: KpiTemplate, isNew: boolean): void {
    // Shape (400): required fields, lengths, known texts and decimal places.
    this.checkCode(candidate.code);
    this.checkText('name', 'KPI name', candidate.name, 150, true);
    this.checkText('description', 'Description', candidate.description, 500, false);
    this.checkText('unit', 'Unit', candidate.unit, 20, false);
    this.checkOneOf(
      'measurementType',
      'Measurement type',
      candidate.measurementType,
      KPI_MEASUREMENT_TYPES,
    );
    this.checkOneOf('direction', 'Direction', candidate.direction, KPI_DIRECTIONS);
    this.checkOneOf(
      'scoringMethod',
      'Scoring method',
      candidate.scoringMethod,
      KPI_SCORING_METHODS,
    );
    this.checkOneOf('frequency', 'Frequency', candidate.frequency, KPI_FREQUENCIES);
    const decimals: [string, number | null, number][] = [
      ['defaultTarget', candidate.defaultTarget, 4],
      ['rangeMin', candidate.rangeMin, 4],
      ['rangeMax', candidate.rangeMax, 4],
      ['maxRating', candidate.maxRating, 4],
      ['defaultWeight', candidate.defaultWeight, 2],
      ['maxAchievementPct', candidate.maxAchievementPct, 2],
    ];
    for (const [field, value, places] of decimals) {
      if (value !== null && !hasAtMostDecimals(value, places)) {
        this.validationError(`Use at most ${places} decimal places.`, {
          [field]: `Use at most ${places} decimal places.`,
        });
      }
    }

    // Scoring rule (domain): the same consistency as the database CHECK constraints.
    const errors = validateKpiRule({
      measurementType: candidate.measurementType,
      direction: candidate.direction,
      scoringMethod: candidate.scoringMethod,
      target: candidate.defaultTarget,
      rangeMin: candidate.rangeMin,
      rangeMax: candidate.rangeMax,
      maxRating: candidate.maxRating,
      maxAchievementPct: candidate.maxAchievementPct,
    });
    // The target error is reported on `defaultTarget`.
    const fieldErrors: Record<string, string> = {};
    for (const [field, message] of Object.entries(errors)) {
      fieldErrors[field === 'target' ? 'defaultTarget' : field] = message;
    }
    if (!(candidate.defaultWeight > 0 && candidate.defaultWeight <= 100)) {
      fieldErrors['defaultWeight'] = 'The weight must be greater than 0 and at most 100.';
    }
    if (Object.keys(fieldErrors).length > 0) {
      this.validationError('The KPI rule is not valid.', fieldErrors);
    }

    // Other records.
    this.ensureUnique(candidate, 'code', 'KPI code');
    const category = this.categories.findById(candidate.categoryId);
    if (!category) {
      this.validationError('The selected KPI category does not exist.', {
        categoryId: 'Choose a KPI category.',
      });
    }
    const previous = isNew ? undefined : this.findById(candidate.id);
    if (previous?.categoryId !== candidate.categoryId && category.status !== 'Active') {
      this.validationError(
        `KPI category "${category.name}" is inactive and cannot be used for new KPI templates.`,
        { categoryId: 'Choose an active category.' },
      );
    }
    if (candidate.departmentId !== null && !this.departments.findById(candidate.departmentId)) {
      this.validationError('The selected department does not exist.', {
        departmentId: 'Choose a department.',
      });
    }
  }

  private checkOneOf(
    field: string,
    label: string,
    value: string,
    allowed: readonly string[],
  ): void {
    if (!value) {
      this.validationError(`${label} is required.`, { [field]: `${label} is required.` });
    }
    if (!allowed.includes(value)) {
      this.validationError(`${label} must be one of: ${allowed.join(', ')}.`, {
        [field]: `Choose one of: ${allowed.join(', ')}.`,
      });
    }
  }

  protected override beforeDelete(entity: KpiTemplate): void {
    const assigned = this.injector
      .get(ScorecardMockService)
      .records()
      .reduce((n, card) => n + card.items.filter((i) => i.templateId === entity.id).length, 0);
    this.ensureNotReferenced(entity.code, [['assigned KPI(s)', assigned]]);
  }
}

// ---- Rating bands ---------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class RatingBandMockService
  extends VersionedMockCrud<RatingBand, RatingBandFilter>
  implements RatingBandDataService
{
  protected readonly entityLabel = 'Rating band';
  protected readonly activityCategory = 'performance';
  protected override readonly searchFields = ['label'] as const;

  constructor() {
    // The default bands are configuration, like roles: the empty-data mode keeps them.
    super(DEFAULT_RATING_BANDS, 'kpi_rating_bands', DEFAULT_RATING_BANDS);
  }

  protected override describe(entity: RatingBand): string {
    return entity.label;
  }

  protected override prepare(candidate: RatingBand, isNew: boolean): void {
    this.trimFields(candidate, ['label', 'description']);
    super.prepare(candidate, isNew);
  }

  protected override validate(candidate: RatingBand): void {
    this.checkText('label', 'Label', candidate.label, 60, true);
    this.checkText('description', 'Description', candidate.description, 300, false);
    if (!(candidate.minScore >= 0 && candidate.minScore <= 500)) {
      this.validationError('The minimum score must be between 0 and 500.', {
        minScore: 'Use a score from 0 to 500.',
      });
    }
    if (!hasAtMostDecimals(candidate.minScore, 2)) {
      this.validationError('Use at most 2 decimal places.', {
        minScore: 'Use at most 2 decimal places.',
      });
    }
    this.ensureUnique(candidate, 'label', 'Rating label');
    if (this.store().some((b) => b.id !== candidate.id && b.minScore === candidate.minScore)) {
      this.validationError(`Minimum score "${candidate.minScore}" already exists.`, {
        minScore: 'Minimum score must be unique.',
      });
    }
  }
}

// ---- Evaluation periods ---------------------------------------------------------------------

/** Past-tense verb for each status move, used in the message and the activity entry. */
const MOVES: Record<
  'activate' | 'complete' | 'close',
  [from: EvaluationPeriodStatus, to: EvaluationPeriodStatus, verb: string]
> = {
  activate: ['Draft', 'Active', 'activated'],
  complete: ['Active', 'Completed', 'completed'],
  close: ['Completed', 'Closed', 'closed'],
};

@Injectable({ providedIn: 'root' })
export class EvaluationPeriodMockService
  extends VersionedMockCrud<EvaluationPeriod, EvaluationPeriodFilter>
  implements EvaluationPeriodDataService
{
  private readonly injector = inject(Injector);
  private readonly companies = inject(CompanyMockService);
  protected readonly entityLabel = 'Evaluation period';
  protected readonly activityCategory = 'performance';
  protected override readonly searchFields = ['name'] as const;

  constructor() {
    super(MOCK_EVALUATION_PERIODS, 'kpi_periods');
  }

  protected override describe(entity: EvaluationPeriod): string {
    return entity.name;
  }

  /** Equality filters, plus `year` (the year of the start date). */
  protected override matchesFilters(
    item: EvaluationPeriod,
    filters: Partial<EvaluationPeriodFilter>,
  ): boolean {
    const { year, ...equality } = filters;
    return (
      matchesEqualityFilters(item, equality) &&
      (year === undefined || year === null || item.startDate.startsWith(String(year)))
    );
  }

  protected override prepare(candidate: EvaluationPeriod, isNew: boolean): void {
    this.trimFields(candidate, ['name']);
    candidate.submissionDueDate ||= null;
    if (isNew) {
      // A period always starts in Draft; the status never changes through update.
      candidate.status = 'Draft';
    } else {
      candidate.status = this.require(candidate.id).status;
    }
    super.prepare(candidate, isNew);
  }

  protected override validate(candidate: EvaluationPeriod, isNew: boolean): void {
    // Shape and dates (400).
    this.checkText('name', 'Period name', candidate.name, 100, true);
    if (!KPI_FREQUENCIES.includes(candidate.periodType)) {
      this.validationError(`Period type must be one of: ${KPI_FREQUENCIES.join(', ')}.`, {
        periodType: 'Choose a period type.',
      });
    }
    if (candidate.endDate <= candidate.startDate) {
      this.validationError('The end date must be after the start date.', {
        endDate: 'Must be after the start date.',
      });
    }
    if (candidate.submissionDueDate && candidate.submissionDueDate < candidate.startDate) {
      this.validationError('The submission due date cannot be before the period starts.', {
        submissionDueDate: 'Must be on or after the start date.',
      });
    }

    // What may change in which status (409).
    const existing = isNew ? undefined : this.require(candidate.id);
    if (existing) {
      if (existing.status === 'Active') {
        const locked =
          candidate.companyId !== existing.companyId ||
          candidate.periodType !== existing.periodType ||
          candidate.startDate !== existing.startDate ||
          candidate.endDate !== existing.endDate ||
          candidate.requiresHrReview !== existing.requiresHrReview;
        if (locked) {
          this.conflictError(
            `Period "${existing.name}" is Active: only its name and submission due date can be changed.`,
          );
        }
      } else if (existing.status !== 'Draft') {
        this.conflictError(
          `Period "${existing.name}" is ${existing.status} and can no longer be changed.`,
        );
      }
    }

    // Company, unique name and no overlap.
    const company = this.companies.findById(candidate.companyId);
    if (!company) {
      this.validationError('The selected company does not exist.', {
        companyId: 'Choose a company.',
      });
    }
    if (existing?.companyId !== candidate.companyId && company.status !== 'Active') {
      this.validationError(
        `Company "${company.name}" is inactive and cannot be used for new evaluation periods.`,
        { companyId: 'Choose an active company.' },
      );
    }
    const sameName = this.store().some(
      (p) =>
        p.id !== candidate.id &&
        p.companyId === candidate.companyId &&
        p.name.trim().toLowerCase() === candidate.name.trim().toLowerCase(),
    );
    if (sameName) {
      this.validationError(`Period name "${candidate.name}" already exists in this company.`, {
        name: 'Period name must be unique within the company.',
      });
    }
    const datesChanged =
      !existing ||
      existing.companyId !== candidate.companyId ||
      existing.periodType !== candidate.periodType ||
      existing.startDate !== candidate.startDate ||
      existing.endDate !== candidate.endDate;
    if (datesChanged) {
      const clash = this.store()
        .filter(
          (p) =>
            p.id !== candidate.id &&
            p.companyId === candidate.companyId &&
            p.periodType === candidate.periodType &&
            p.startDate <= candidate.endDate &&
            p.endDate >= candidate.startDate,
        )
        .sort((a, b) => a.startDate.localeCompare(b.startDate) || a.id - b.id)[0];
      if (clash) {
        this.conflictError(
          `The dates overlap period "${clash.name}" (${clash.startDate} to ${clash.endDate}), which is also ${candidate.periodType}. Periods of the same type cannot overlap in a company.`,
        );
      }
    }
  }

  protected override beforeDelete(entity: EvaluationPeriod): void {
    if (entity.status !== 'Draft') {
      this.conflictError(
        `Period "${entity.name}" is ${entity.status}; only a Draft period can be deleted.`,
      );
    }
    const scorecards = this.injector
      .get(ScorecardMockService)
      .records()
      .filter((card) => card.periodId === entity.id).length;
    this.ensureNotReferenced(entity.name, [['scorecard(s)', scorecards]]);
  }

  // ---- Commands ----

  activate(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.respond(() => this.move(id, 'activate', rowVersion));
  }

  complete(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.respond(() => this.move(id, 'complete', rowVersion));
  }

  close(id: number, rowVersion?: string): Observable<EvaluationPeriod> {
    return this.respond(() => this.move(id, 'close', rowVersion));
  }

  private move(
    id: number,
    command: keyof typeof MOVES,
    rowVersion: string | undefined,
  ): EvaluationPeriod {
    const period = this.require(id);
    if (rowVersion !== undefined && rowVersion !== period.rowVersion) {
      this.conflictError(STALE_MESSAGE);
    }
    const [from, to, verb] = MOVES[command];
    if (period.status !== from) {
      this.conflictError(
        `Period "${period.name}" is ${period.status}; only a ${from} period can be ${verb}.`,
      );
    }
    if (command === 'close') {
      // A Closed period refuses every scorecard write: one still in review or returned would be stuck.
      const blocking = this.injector
        .get(ScorecardMockService)
        .records()
        .filter(
          (card) => card.periodId === id && KPI_STATUSES_BLOCKING_CLOSE.includes(card.status),
        ).length;
      const refusal = periodCloseRefusal(period.name, blocking);
      if (refusal !== null) {
        this.conflictError(refusal);
      }
    }
    const moved: EvaluationPeriod = {
      ...period,
      status: to,
      rowVersion: nextRowVersion(period.rowVersion),
    };
    this.store.update((items) => items.map((p) => (p.id === id ? moved : p)));
    this.activity.record('performance', `Evaluation period "${period.name}" ${verb}`);
    return moved;
  }

  /** The period picker: periods that are not Draft, newest start first. */
  options(companyId?: number | null): Observable<EvaluationPeriodOption[]> {
    return mockResponse(
      () =>
        this.store()
          .filter((p) => p.status !== 'Draft' && (!companyId || p.companyId === companyId))
          .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.id - b.id)
          .map((p): EvaluationPeriodOption => ({
            id: p.id,
            name: p.name,
            companyId: p.companyId,
            periodType: p.periodType,
            startDate: p.startDate,
            endDate: p.endDate,
            status: p.status,
            requiresHrReview: p.requiresHrReview,
            submissionDueDate: p.submissionDueDate,
          })),
      this.latency,
    );
  }
}
