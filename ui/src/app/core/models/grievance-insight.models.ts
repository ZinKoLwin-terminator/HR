import {
  GrievanceHistoryAction,
  GrievancePriority,
  GrievanceStatus,
  MyGrievance,
} from './grievance.models';

/**
 * Grievance dashboards and reports, typed to the API contract. Read only: every figure is computed by
 * the server and the screens only display it. Dates are `yyyy-MM-dd`, months `yyyy-MM`, numbers are
 * plain numbers, "today" is the business time zone's.
 *
 * Confidentiality: no aggregate names a person or carries a subject, description or note. Recent
 * activity carries case numbers only. Department groups are the department copied onto the case at
 * submission; an anonymous case (or one with no department) is counted as "Not disclosed", and every
 * department with fewer than `GRIEVANCE_DEPARTMENT_MIN_GROUP` cases is merged into one
 * "Other / suppressed" group.
 *
 * Metric definitions (one each; the demo mock repeats them):
 *  - cases counted: every case that is not a Draft, archived ones included (lists hide archived cases,
 *    aggregates do not);
 *  - overdue: open (`GRIEVANCE_OPEN_STATUSES`) and the target date is before today;
 *  - resolution rate: (Resolved + Closed) ÷ (counted − Rejected) × 100, 2 decimals rounded half away
 *    from zero; 0 when the divisor is 0;
 *  - resolution days: calendar days from the submission date to the resolution date; average over
 *    the cases now Resolved or Closed, 1 decimal; null (never 0) when there is none.
 */

/** Groups smaller than this are merged into the suppressed group. */
export const GRIEVANCE_DEPARTMENT_MIN_GROUP = 3;
/** Name of the merged group of small departments. */
export const GRIEVANCE_SUPPRESSED_LABEL = 'Other / suppressed';

/** How many cases have one status. Lists the 11 non-Draft statuses in workflow order, zeros included. */
export interface GrievanceStatusCount {
  status: GrievanceStatus;
  count: number;
}

/** Lists the four priorities then `null` (not yet acknowledged), zeros included. */
export interface GrievancePriorityCount {
  priority: GrievancePriority | null;
  count: number;
}

/** Categories with at least one case, most cases first, then by name. */
export interface GrievanceCategoryCount {
  categoryId: number;
  categoryName: string;
  count: number;
}

/** `Department` is a real department; the other two are the merged groups (`departmentId` null). */
export type GrievanceDepartmentBucket = 'Department' | 'NotDisclosed' | 'Suppressed';

/** Departments by name, then Not disclosed, then Other / suppressed (merged groups only when > 0). */
export interface GrievanceDepartmentCount {
  bucket: GrievanceDepartmentBucket;
  departmentId: number | null;
  /** The department name, `GRIEVANCE_NOT_DISCLOSED_LABEL` or `GRIEVANCE_SUPPRESSED_LABEL`. */
  departmentName: string;
  count: number;
}

/**
 * One calendar month: cases submitted in it, and cases now Resolved or Closed whose resolution date
 * is in it.
 */
export interface GrievanceMonthlyCount {
  /** `yyyy-MM`. */
  month: string;
  submitted: number;
  resolved: number;
}

/** One history step, without actor, reason or subject. */
export interface GrievanceRecentActivity {
  caseId: number;
  caseNo: string;
  action: GrievanceHistoryAction;
  toStatus: GrievanceStatus;
  occurredAtUtc: string;
}

// ---- HR dashboard (`grievance-management:View`) -----------------------------------------------

/** `GET /api/grievances/dashboard/hr`. */
export interface GrievanceHrDashboard {
  /** Today, the date `overdue` was computed for. */
  asOfDate: string;
  total: number;
  /** Submitted (waiting for acknowledgement). */
  new: number;
  /** Acknowledged, UnderReview or Reopened. */
  underReview: number;
  /** Assigned or UnderInvestigation. */
  underInvestigation: number;
  awaitingEmployeeResponse: number;
  /** ResolutionProposed. */
  pendingResolution: number;
  resolved: number;
  closed: number;
  rejected: number;
  overdue: number;
  /** Open and escalated. */
  escalated: number;
  resolutionRatePct: number;
  averageResolutionDays: number | null;
  byStatus: GrievanceStatusCount[];
  byPriority: GrievancePriorityCount[];
  byCategory: GrievanceCategoryCount[];
  byDepartment: GrievanceDepartmentCount[];
  /** The last 12 months, oldest first, the current month last, zeros included. */
  monthlyTrend: GrievanceMonthlyCount[];
  /** The 10 latest history steps, newest first. */
  recentActivity: GrievanceRecentActivity[];
}

// ---- Review dashboard (`grievance-review:View`) ----------------------------------------------

/**
 * `GET /api/grievances/dashboard/review`: aggregates over the review queue (escalated open cases and
 * cases with a pending proposal). No case list, no names.
 */
export interface GrievanceReviewDashboard {
  asOfDate: string;
  /** Cases in ResolutionProposed with a pending proposal. */
  pendingProposals: number;
  /** Open and escalated. */
  escalated: number;
  /** Open and escalated with an immediate escalation not cleared. */
  immediateEscalations: number;
  /** Queue cases that are overdue. */
  overdue: number;
  /** Days since the oldest pending proposal was made; null when none is pending. */
  oldestPendingDays: number | null;
  /** Proposals the caller approved or rejected (all time). */
  decidedByMe: { approved: number; rejected: number };
  byPriority: GrievancePriorityCount[];
  byCategory: GrievanceCategoryCount[];
}

// ---- My dashboard (`grievance:View`) -----------------------------------------------------------

/** `GET /api/grievances/my/dashboard`: the caller's own cases (named and anonymous). */
export interface MyGrievanceDashboard {
  drafts: number;
  /** Submitted and still open. */
  open: number;
  /** AwaitingEmployeeResponse: the caller has to answer. */
  awaitingMyResponse: number;
  resolved: number;
  closed: number;
  rejected: number;
  /** Cases with `hasUnreadUpdate`. */
  unreadUpdates: number;
  /** The 5 most recently updated, newest first (drafts included). */
  recent: MyGrievance[];
}

// ---- Reports (`grievance-management:View`) -----------------------------------------------------

/**
 * Filters of the case report (combined with AND). A department filter never matches an anonymous
 * case. `minResolutionDays`/`maxResolutionDays` (inclusive) keep only cases with a resolution;
 * `overdue` true keeps overdue cases, false the others.
 */
export interface GrievanceReportFilter {
  submittedFrom: string;
  submittedTo: string;
  departmentId: number;
  categoryId: number;
  status: GrievanceStatus;
  priority: GrievancePriority;
  investigatorEmployeeId: number;
  minResolutionDays: number;
  maxResolutionDays: number;
  overdue: boolean;
}

/** The query-string names of {@link GrievanceReportFilter}, in the order the API lists them. */
export const GRIEVANCE_REPORT_FILTER_KEYS: readonly (keyof GrievanceReportFilter)[] = [
  'submittedFrom',
  'submittedTo',
  'departmentId',
  'categoryId',
  'status',
  'priority',
  'investigatorEmployeeId',
  'minResolutionDays',
  'maxResolutionDays',
  'overdue',
];

/**
 * One case of the report (and of the `grievance-cases` Excel export, same columns). Never the
 * subject, description, notes or submitter.
 */
export interface GrievanceReportRow {
  caseId: number;
  caseNo: string;
  categoryName: string;
  status: GrievanceStatus;
  priority: GrievancePriority | null;
  /** Null for an anonymous case. */
  departmentId: number | null;
  /** `GRIEVANCE_NOT_DISCLOSED_LABEL` for an anonymous case, null when none was recorded. */
  departmentName: string | null;
  submittedOn: string;
  targetResolutionDate: string | null;
  resolvedOn: string | null;
  /** Submission to resolution; null until resolved. */
  resolutionDays: number | null;
  /** Submission to resolution, or to today while unresolved. */
  ageDays: number;
  isOverdue: boolean;
  leadInvestigatorName: string | null;
  isEscalated: boolean;
}

/** The `sort` fields the case report accepts (anything else is a 400 on `sort`). */
export const GRIEVANCE_REPORT_SORT_KEYS: readonly string[] = [
  'caseNo',
  'categoryName',
  'status',
  'priority',
  'departmentName',
  'submittedOn',
  'targetResolutionDate',
  'resolvedOn',
  'resolutionDays',
  'ageDays',
];

/**
 * Filters of the summary (`grievance-management:View` or `grievance-review:View`). There is no
 * department filter: it would undo the small-group suppression for reviewers.
 */
export interface GrievanceSummaryFilter {
  submittedFrom: string;
  submittedTo: string;
  categoryId: number;
  priority: GrievancePriority;
}

export const GRIEVANCE_SUMMARY_FILTER_KEYS: readonly (keyof GrievanceSummaryFilter)[] = [
  'submittedFrom',
  'submittedTo',
  'categoryId',
  'priority',
];

/** `GET /api/grievances/reports/summary`: aggregates over the matching cases. */
export interface GrievanceSummary {
  asOfDate: string;
  total: number;
  open: number;
  resolved: number;
  closed: number;
  rejected: number;
  overdue: number;
  escalated: number;
  resolutionRatePct: number;
  averageResolutionDays: number | null;
  byStatus: GrievanceStatusCount[];
  byPriority: GrievancePriorityCount[];
  byCategory: GrievanceCategoryCount[];
  byDepartment: GrievanceDepartmentCount[];
}
