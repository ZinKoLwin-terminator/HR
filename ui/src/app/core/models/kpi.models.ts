import { Entity, RecordStatus } from './common.models';

/**
 * KPI / performance models, typed to the API contract (`api/README.md`, "HTTP contract (KPI ...)").
 *
 * Dates are `yyyy-MM-dd` strings, decimals are plain numbers, and every enum is the exact text of the
 * database CHECK constraints (for example `Higher Is Better`, `Half-Yearly`, `Under Review`).
 * `rowVersion` is the opaque concurrency token: send back the value you last received.
 *
 * Official scores come from the API. The demo mock repeats the calculation (see
 * `core/utilities/kpi-calculation.ts`) and is not authoritative.
 */

// ---- Enumerations (exact API texts) --------------------------------------------------------

export type KpiMeasurementType =
  'Number' | 'Percentage' | 'Currency' | 'Hours' | 'Rating' | 'Boolean';
export const KPI_MEASUREMENT_TYPES: readonly KpiMeasurementType[] = [
  'Number',
  'Percentage',
  'Currency',
  'Hours',
  'Rating',
  'Boolean',
];

export type KpiDirection = 'Higher Is Better' | 'Lower Is Better' | 'Exact Target' | 'Range Based';
export const KPI_DIRECTIONS: readonly KpiDirection[] = [
  'Higher Is Better',
  'Lower Is Better',
  'Exact Target',
  'Range Based',
];

export type KpiScoringMethod =
  'Direct Percentage' | 'Capped Percentage' | 'Rating Based' | 'Boolean' | 'Range Based';
export const KPI_SCORING_METHODS: readonly KpiScoringMethod[] = [
  'Direct Percentage',
  'Capped Percentage',
  'Rating Based',
  'Boolean',
  'Range Based',
];

/** How often a KPI is measured; also the type of an evaluation period. */
export type KpiFrequency = 'Monthly' | 'Quarterly' | 'Half-Yearly' | 'Yearly' | 'Custom';
export const KPI_FREQUENCIES: readonly KpiFrequency[] = [
  'Monthly',
  'Quarterly',
  'Half-Yearly',
  'Yearly',
  'Custom',
];

export type EvaluationPeriodStatus = 'Draft' | 'Active' | 'Completed' | 'Closed';
export const EVALUATION_PERIOD_STATUSES: readonly EvaluationPeriodStatus[] = [
  'Draft',
  'Active',
  'Completed',
  'Closed',
];

export type ScorecardStatus =
  | 'Planning'
  | 'Draft'
  | 'Submitted'
  | 'Under Review'
  | 'Returned'
  | 'Resubmitted'
  | 'Approved'
  | 'Rejected'
  | 'Finalized';
export const SCORECARD_STATUSES: readonly ScorecardStatus[] = [
  'Planning',
  'Draft',
  'Submitted',
  'Under Review',
  'Returned',
  'Resubmitted',
  'Approved',
  'Rejected',
  'Finalized',
];

export type ReviewStep = 'Employee' | 'Manager' | 'HR';

export type ReviewActionName =
  | 'Finalize Plan'
  | 'Submit'
  | 'Resubmit'
  | 'Start Review'
  | 'Approve'
  | 'Reject'
  | 'Return'
  | 'Finalize';

// ---- Master data ----------------------------------------------------------------------------

export interface KpiCategory extends Entity {
  /** At most 20 characters: upper-case letters, digits and dashes. */
  code: string;
  /** At most 100 characters, unique. */
  name: string;
  description: string;
  status: RecordStatus;
  rowVersion?: string;
}

/**
 * A category as the picker returns it (`GET /api/kpi/categories/options`, kpi:View): every
 * category, inactive ones included, by name. No description or row version.
 */
export type KpiCategoryOption = Pick<KpiCategory, 'id' | 'code' | 'name' | 'status'>;

/** A reusable KPI definition. A scorecard item copies it when assigned, so later edits never change an item. */
export interface KpiTemplate extends Entity {
  code: string;
  name: string;
  description: string;
  categoryId: number;
  /** Null: the template is offered for every department. */
  departmentId: number | null;
  measurementType: KpiMeasurementType;
  unit: string | null;
  direction: KpiDirection;
  scoringMethod: KpiScoringMethod;
  defaultTarget: number;
  /** Range Based only. */
  rangeMin: number | null;
  rangeMax: number | null;
  /** Rating Based only: the top of the rating scale. */
  maxRating: number | null;
  defaultWeight: number;
  /** 1 to 500. Required for Capped Percentage and for Lower Is Better; not allowed for Direct Percentage. */
  maxAchievementPct: number | null;
  frequency: KpiFrequency;
  status: RecordStatus;
  rowVersion?: string;
}

/** An overall-score band: a score of at least `minScore` gets the label, up to the next band. */
export interface RatingBand extends Entity {
  label: string;
  minScore: number;
  description: string;
  status: RecordStatus;
  rowVersion?: string;
}

/** An evaluation window of a company. Its status only changes through the period commands. */
export interface EvaluationPeriod extends Entity {
  companyId: number;
  name: string;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  /** May be after `endDate`; a later submission is allowed and can be flagged. */
  submissionDueDate: string | null;
  requiresHrReview: boolean;
  status: EvaluationPeriodStatus;
  rowVersion?: string;
}

/** One entry of the period picker (periods that are not Draft). Carries no `rowVersion`. */
export interface EvaluationPeriodOption {
  id: number;
  name: string;
  companyId: number;
  periodType: KpiFrequency;
  startDate: string;
  endDate: string;
  status: EvaluationPeriodStatus;
  requiresHrReview: boolean;
  /** Null when the period has none. */
  submissionDueDate: string | null;
}

export interface KpiCategoryFilter {
  status: RecordStatus;
}

export interface KpiTemplateFilter {
  status: RecordStatus;
  categoryId: number;
  departmentId: number;
  scoringMethod: KpiScoringMethod;
}

export interface RatingBandFilter {
  status: RecordStatus;
}

export interface EvaluationPeriodFilter {
  companyId: number;
  periodType: KpiFrequency;
  status: EvaluationPeriodStatus;
  /** Year of the start date. */
  year: number;
}

// ---- Scorecards -----------------------------------------------------------------------------

/** One scorecard in a list: an employee's KPI plan and result for one period. */
export interface Scorecard extends Entity {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  periodId: number;
  periodName: string;
  departmentId: number;
  departmentName: string;
  status: ScorecardStatus;
  itemCount: number;
  /** Sum of the item weights, in percent. Exactly 100 once the plan is finalized. */
  totalWeight: number;
  overallScore: number | null;
  ratingLabel: string | null;
  reviewerEmployeeId: number | null;
  reviewerName: string | null;
  planFinalizedOn: string | null;
  submittedOn: string | null;
  finalizedOn: string | null;
  /**
   * The period's submission due date, or null when it has none, so a list can show a Late flag
   * without reading each scorecard.
   */
  submissionDueDate: string | null;
  rowVersion: string;
}

/** Metadata of one evidence file. The storage key is never exposed. */
export interface KpiAttachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedOn: string;
}

/**
 * One assigned KPI. `kpiCode` to `maxAchievementPct` is the snapshot copied from the template at assignment;
 * target, weight, dates and sort order are what HR planned.
 */
export interface ScorecardItem extends Entity {
  templateId: number;
  categoryId: number;
  categoryName: string;
  kpiCode: string;
  kpiName: string;
  measurementType: KpiMeasurementType;
  unit: string | null;
  direction: KpiDirection;
  scoringMethod: KpiScoringMethod;
  target: number;
  rangeMin: number | null;
  rangeMax: number | null;
  maxRating: number | null;
  weight: number;
  maxAchievementPct: number | null;
  startDate: string;
  endDate: string;
  sortOrder: number;
  actualValue: number | null;
  employeeComment: string | null;
  achievementPct: number | null;
  weightedScore: number | null;
  managerRating: number | null;
  managerComment: string | null;
  rowVersion: string;
  /** At most 5, oldest first. Filled in the scorecard detail only. */
  attachments: KpiAttachment[];
}

/** One step of the approval history (append-only, oldest first). */
export interface ReviewAction {
  step: ReviewStep;
  action: ReviewActionName;
  fromStatus: ScorecardStatus;
  toStatus: ScorecardStatus;
  /**
   * The user account that recorded the step (null when none was recorded). Compare it with the
   * signed-in user's id, never with the display name.
   */
  actorUserId: number | null;
  actorName: string;
  comment: string | null;
  overallScore: number | null;
  /** ISO date-time (UTC). */
  occurredAtUtc: string;
}

export interface ScorecardDetail extends Scorecard {
  reviewedOn: string | null;
  periodStatus: EvaluationPeriodStatus;
  requiresHrReview: boolean;
  employeeComment: string | null;
  managerRating: number | null;
  managerComment: string | null;
  hrComment: string | null;
  /** By `sortOrder`. */
  items: ScorecardItem[];
  history: ReviewAction[];
}

/** Filters of every scorecard query (`status` is one scorecard status text). */
export interface ScorecardFilter {
  periodId: number;
  employeeId: number;
  departmentId: number;
  companyId: number;
  status: ScorecardStatus;
}

// ---- Commands and requests ------------------------------------------------------------------

/** One KPI to assign. Target and weight default to the template's, dates to the period's. */
export interface AssignmentItemInput {
  templateId: number;
  target?: number | null;
  weight?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  sortOrder?: number | null;
}

/** One to 50 KPIs for one employee in one period. */
export interface AssignKpiRequest {
  employeeId: number;
  periodId: number;
  items: AssignmentItemInput[];
}

/** The scorecard, and whether the call created it (API: 201) or added items to the one in Planning (200). */
export interface AssignKpiResult {
  scorecard: ScorecardDetail;
  created: boolean;
}

/**
 * Same KPIs for many employees. Choose the employees with `employeeIds` (1 to 500) OR with one or more
 * of the org filters (combined with AND), never both.
 */
export interface BulkAssignKpiRequest {
  periodId: number;
  employeeIds?: number[] | null;
  departmentId?: number | null;
  branchId?: number | null;
  positionId?: number | null;
  jobGradeId?: number | null;
  items: AssignmentItemInput[];
}

export interface BulkAssignedEmployee {
  employeeId: number;
  employeeCode: string;
  scorecardId: number;
  itemsAdded: number;
}

export interface BulkSkippedEmployee {
  employeeId: number;
  /** Null for an id that matches no employee. */
  employeeCode: string | null;
  reason: string;
}

export interface BulkAssignmentResult {
  assigned: BulkAssignedEmployee[];
  skipped: BulkSkippedEmployee[];
}

/** What HR may change on an assigned KPI while the scorecard is in Planning (the item's own `rowVersion`). */
export interface ScorecardItemUpdate {
  target: number;
  weight: number;
  startDate: string;
  endDate: string;
  /** Keeps its value when left out. */
  sortOrder?: number | null;
  rowVersion: string;
}

export interface ResultItemInput {
  id: number;
  /** Null clears the result. */
  actualValue?: number | null;
  employeeComment?: string | null;
}

/** The employee's draft results (Draft or Returned scorecard, Active period). No score is stored yet. */
export interface SaveResultsRequest {
  rowVersion: string;
  employeeComment?: string | null;
  items: ResultItemInput[];
}

export interface ReviewItemInput {
  id: number;
  /** 1 to 5, at most 2 decimals. */
  managerRating?: number | null;
  managerComment?: string | null;
}

/** The reviewer's draft: no status change and no history entry. */
export interface SaveReviewRequest {
  rowVersion: string;
  managerRating?: number | null;
  managerComment?: string | null;
  items: ReviewItemInput[];
}

/** Body of approve and finalize (comment optional) and of return and reject (comment required). */
export interface ReviewDecision {
  rowVersion: string;
  comment?: string | null;
}

/** Answer to an upload: the new attachment and the scorecard's new row version. */
export interface EvidenceUploadResult {
  attachment: KpiAttachment;
  scorecardRowVersion: string;
}

/** Answer to a removal: the scorecard's new row version. */
export interface EvidenceRemovalResult {
  scorecardRowVersion: string;
}

/** A downloaded evidence file. */
export interface EvidenceDownload {
  blob: Blob;
  fileName: string;
}

/** Evidence limits (the API enforces the same). */
export const EVIDENCE_MAX_BYTES = 10 * 1024 * 1024;
export const EVIDENCE_MAX_PER_KPI = 5;
export const EVIDENCE_ALLOWED_EXTENSIONS: readonly string[] = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.docx',
  '.xlsx',
];
