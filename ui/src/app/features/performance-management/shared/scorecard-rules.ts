import {
  EVIDENCE_MAX_BYTES,
  EVIDENCE_MAX_PER_KPI,
  EvaluationPeriodStatus,
  ReviewAction,
  Scorecard,
  ScorecardDetail,
  ScorecardItem,
  ScorecardStatus,
} from '../../../core/models/kpi.models';
import {
  EVIDENCE_EMPTY_MESSAGE,
  EVIDENCE_TOO_LARGE_MESSAGE,
  inspectEvidence,
  readFileBytes,
} from '../../../core/utilities/kpi-evidence';

/**
 * What the employee and reviewer screens show and offer. These are UX rules only: the server
 * decides who may act and answers 403 or 409 whatever the screen shows (see the API's "HTTP
 * contract (KPI submission and review)"). Kept here, outside the components, so a test can check
 * every status and role.
 */

/** Statuses of the manager stage (reviewer or HR). */
export const MANAGER_STAGE: readonly ScorecardStatus[] = [
  'Submitted',
  'Resubmitted',
  'Under Review',
];
/** Statuses the review queue can hold (manager stage, and Approved waiting for HR). */
export const REVIEW_QUEUE_STATUSES: readonly ScorecardStatus[] = [...MANAGER_STAGE, 'Approved'];

/** Who is looking at the scorecard, as the screens understand it. */
export interface ScorecardViewer {
  /** The signed-in user's employee, or null for an account without one. */
  employeeId: number | null;
  /** The signed-in user's account id: what the review history records as `actorUserId`. */
  userId: number;
  /** Holds `kpi:Create` (enter results, submit, evidence). */
  canCreate: boolean;
  /** Holds `kpi:Approve` (review). */
  canApprove: boolean;
  /** HR tier: `kpi:Approve` together with `employees:Edit`. */
  hrTier: boolean;
}

/** True when `submittedOn` is after the period's submission due date (a late submission is allowed). */
export function isLateSubmission(
  submittedOn: string | null | undefined,
  dueDate: string | null | undefined,
): boolean {
  return !!submittedOn && !!dueDate && submittedOn > dueDate;
}

export function isOwnScorecard(
  card: Pick<Scorecard, 'employeeId'>,
  viewer: Pick<ScorecardViewer, 'employeeId'>,
): boolean {
  return viewer.employeeId !== null && viewer.employeeId === card.employeeId;
}

/** The server's overall score with its rating label ("80.88 · Exceeds Expectations"), or a dash. */
export function overallText(row: Pick<Scorecard, 'overallScore' | 'ratingLabel'>): string {
  if (row.overallScore === null) return '—';
  const score = row.overallScore.toFixed(2);
  return row.ratingLabel ? `${score} · ${row.ratingLabel}` : score;
}

/** The employee may enter results, submit and change evidence: own, Draft or Returned, period Active. */
export function canWorkOnScorecard(
  card: Pick<ScorecardDetail, 'employeeId' | 'status' | 'periodStatus'>,
  viewer: Pick<ScorecardViewer, 'employeeId' | 'canCreate'>,
): boolean {
  return (
    viewer.canCreate &&
    isOwnScorecard(card, viewer) &&
    (card.status === 'Draft' || card.status === 'Returned') &&
    card.periodStatus === 'Active'
  );
}

/** Why the employee's page is read-only (shown as a note), or null while results can be entered. */
export function employeeNote(
  card: Pick<
    ScorecardDetail,
    'employeeId' | 'employeeName' | 'status' | 'periodStatus' | 'periodName'
  >,
  viewer: Pick<ScorecardViewer, 'employeeId' | 'canCreate'>,
): string | null {
  if (canWorkOnScorecard(card, viewer)) return null;
  if (!isOwnScorecard(card, viewer)) {
    return `This is the scorecard of ${card.employeeName}. Only they can enter results and evidence.`;
  }
  switch (card.status) {
    case 'Draft':
    case 'Returned':
      return card.periodStatus !== 'Active'
        ? `Period ${card.periodName} is ${card.periodStatus}: results can no longer be entered or submitted.`
        : 'Your role cannot enter results.';
    case 'Submitted':
    case 'Resubmitted':
    case 'Under Review':
      return 'Submitted for review. Your results can no longer be changed unless the scorecard is returned to you.';
    case 'Approved':
      return 'Approved by your reviewer. It now waits for the HR final review.';
    case 'Finalized':
      return 'Finalized: the review is complete and the scorecard is read-only.';
    case 'Rejected':
      return 'Rejected: this scorecard is final and read-only.';
    default:
      return `This scorecard is ${card.status}.`;
  }
}

/** Review actions need an Active or Completed period. */
export const periodAllowsReview = (status: EvaluationPeriodStatus): boolean =>
  status === 'Active' || status === 'Completed';

/** The stage a scorecard waits in: the reviewer (manager) or HR's final review. */
export function reviewStage(status: ScorecardStatus): 'Manager' | 'HR' | null {
  if (MANAGER_STAGE.includes(status)) return 'Manager';
  return status === 'Approved' ? 'HR' : null;
}

export function stageLabel(status: ScorecardStatus): string {
  const stage = reviewStage(status);
  return stage === 'HR' ? 'HR final review' : stage === 'Manager' ? 'Manager review' : '—';
}

/**
 * The account that recorded the latest manager-stage approval (`actorUserId`), or null when there is
 * none or no account was recorded. Display names are never compared: two people may share one.
 */
export function managerApproverUserId(history: readonly ReviewAction[]): number | null {
  for (let i = history.length - 1; i >= 0; i--) {
    const entry = history[i];
    if (entry.action === 'Approve' && entry.step === 'Manager') return entry.actorUserId ?? null;
  }
  return null;
}

/** The latest Return or Reject step, with its reason (shown prominently to the employee). */
export function latestDecision(
  history: readonly ReviewAction[],
  action: 'Return' | 'Reject',
): ReviewAction | null {
  for (let i = history.length - 1; i >= 0; i--) {
    if (history[i].action === action) return history[i];
  }
  return null;
}

/** Which reviewer actions the screen offers. */
export interface ReviewActions {
  startReview: boolean;
  /** Edit the per-KPI and overall ratings and comments ("Save review notes"). */
  saveReview: boolean;
  approve: boolean;
  /** HR's final review of an Approved scorecard. */
  finalize: boolean;
  returnForRevision: boolean;
  reject: boolean;
}

export const NO_REVIEW_ACTIONS: ReviewActions = {
  startReview: false,
  saveReview: false,
  approve: false,
  finalize: false,
  returnForRevision: false,
  reject: false,
};

/**
 * Reviewer buttons, by status and viewer:
 * - never on the viewer's own scorecard, and only while the period is Active or Completed;
 * - manager stage (Submitted, Resubmitted, Under Review): the scorecard's reviewer or the HR tier.
 *   Start review only from Submitted or Resubmitted;
 * - HR stage (Approved): the HR tier, except the account that recorded the manager-stage approval
 *   (its `actorUserId` against the signed-in user's id; the server refuses it with a 409 anyway).
 */
export function reviewActions(
  card: Pick<
    ScorecardDetail,
    'employeeId' | 'status' | 'periodStatus' | 'reviewerEmployeeId' | 'history'
  >,
  viewer: ScorecardViewer,
): ReviewActions {
  if (
    !viewer.canApprove ||
    isOwnScorecard(card, viewer) ||
    !periodAllowsReview(card.periodStatus)
  ) {
    return NO_REVIEW_ACTIONS;
  }
  const stage = reviewStage(card.status);
  if (stage === 'Manager') {
    const reviewer =
      viewer.hrTier ||
      (viewer.employeeId !== null && card.reviewerEmployeeId === viewer.employeeId);
    if (!reviewer) return NO_REVIEW_ACTIONS;
    return {
      ...NO_REVIEW_ACTIONS,
      startReview: card.status === 'Submitted' || card.status === 'Resubmitted',
      saveReview: true,
      approve: true,
      returnForRevision: true,
      reject: true,
    };
  }
  if (stage === 'HR') {
    const approver = managerApproverUserId(card.history);
    if (!viewer.hrTier || (approver !== null && approver === viewer.userId)) {
      return NO_REVIEW_ACTIONS;
    }
    return { ...NO_REVIEW_ACTIONS, finalize: true, returnForRevision: true, reject: true };
  }
  return NO_REVIEW_ACTIONS;
}

/** Why the viewer gets no reviewer buttons (shown as a note), or null when there are some. */
export function noActionReason(
  card: Pick<
    ScorecardDetail,
    'employeeId' | 'status' | 'periodStatus' | 'periodName' | 'reviewerEmployeeId' | 'history'
  >,
  viewer: ScorecardViewer,
): string | null {
  const actions = reviewActions(card, viewer);
  if (Object.values(actions).some(Boolean)) return null;
  if (isOwnScorecard(card, viewer)) return 'This is your own scorecard: nobody reviews their own.';
  const stage = reviewStage(card.status);
  if (stage === null) return `This scorecard is ${card.status}: there is nothing to review.`;
  if (!periodAllowsReview(card.periodStatus)) {
    return `Period ${card.periodName} is ${card.periodStatus}: reviews are closed.`;
  }
  if (stage === 'HR') {
    return viewer.hrTier
      ? 'You approved this scorecard, so another HR person gives the final review.'
      : 'This scorecard waits for the HR final review.';
  }
  return "Only the employee's reviewer or HR can review this scorecard.";
}

// ---- Result inputs ----------------------------------------------------------------------------

/** How the actual value is entered: a yes/no choice, a rating on the item's scale, or a decimal. */
export type ActualInput =
  | { kind: 'boolean' }
  | { kind: 'rating'; min: 0; max: number }
  | { kind: 'decimal'; min: number | null };

export function actualInputFor(
  item: Pick<ScorecardItem, 'measurementType' | 'maxRating' | 'direction'>,
): ActualInput {
  if (item.measurementType === 'Boolean') return { kind: 'boolean' };
  if (item.measurementType === 'Rating')
    return { kind: 'rating', min: 0, max: item.maxRating ?? 5 };
  return { kind: 'decimal', min: item.direction === 'Lower Is Better' ? 0 : null };
}

/** An actual value as text: Yes/No for a Boolean KPI, a dash when there is none. */
export function actualText(
  item: Pick<ScorecardItem, 'measurementType' | 'actualValue' | 'unit'>,
): string {
  if (item.actualValue === null) return '—';
  if (item.measurementType === 'Boolean') return item.actualValue === 1 ? 'Yes' : 'No';
  return item.unit ? `${item.actualValue} ${item.unit}` : String(item.actualValue);
}

// ---- Server errors on their row ---------------------------------------------------------------

/** Field errors split into the ones of each KPI (by item id) and the scorecard-level ones. */
export interface RowErrors {
  items: Map<number, Record<string, string>>;
  other: Record<string, string>;
}

const ITEM_KEY = /^items\[(\d+)\]\.(\w+)$/i;

/**
 * Maps `items[i].field` errors to the KPI at index i of `itemIds`. The screens send the items in
 * the scorecard's order, and the submit check reports missing results by the index in that same
 * order (sort order), so the index always points at `card.items[i]`.
 */
export function rowErrors(
  fieldErrors: Record<string, string> | undefined,
  itemIds: readonly number[],
): RowErrors {
  const items = new Map<number, Record<string, string>>();
  const other: Record<string, string> = {};
  for (const [key, message] of Object.entries(fieldErrors ?? {})) {
    const match = ITEM_KEY.exec(key);
    const id = match ? itemIds[Number(match[1])] : undefined;
    if (match && id !== undefined) {
      const field = match[2][0].toLowerCase() + match[2].slice(1);
      items.set(id, { ...items.get(id), [field]: message });
    } else {
      other[key] = message;
    }
  }
  return { items, other };
}

// ---- Evidence ---------------------------------------------------------------------------------

/** "12 KB", "1.5 MB": the size of an evidence file. */
export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export const EVIDENCE_LIMIT_MESSAGE = `A KPI can have at most ${EVIDENCE_MAX_PER_KPI} evidence files. Remove one before adding another.`;

/**
 * The client-side check before an upload (the server checks again): 5 files per KPI, 1 byte to
 * 10 MB, and a type decided by the content that agrees with the extension. Null when it may go.
 */
export async function evidenceProblem(file: File, existing: number): Promise<string | null> {
  if (existing >= EVIDENCE_MAX_PER_KPI) return EVIDENCE_LIMIT_MESSAGE;
  if (file.size === 0) return EVIDENCE_EMPTY_MESSAGE;
  if (file.size > EVIDENCE_MAX_BYTES) return EVIDENCE_TOO_LARGE_MESSAGE;
  const inspected = inspectEvidence(file.name, await readFileBytes(file));
  return inspected.ok ? null : inspected.message;
}
