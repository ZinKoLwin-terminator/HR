import { KpiStatusCount } from '../models/kpi-insight.models';
import { EvaluationPeriodStatus, SCORECARD_STATUSES, ScorecardStatus } from '../models/kpi.models';

/**
 * The arithmetic of the KPI dashboards and reports, one definition each, mirrored from the API's
 * `KpiFigures` so the demo mock gives the same figures. NOT authoritative: with the API on, every
 * figure comes from the server and the screens only display it.
 *
 * Inputs are scores and achievements as the calculator stores them (at most 2 decimals). Averages
 * and rates are rounded to 2 decimals, a midpoint away from zero, with integer arithmetic (no float
 * drift).
 */

/** The attention threshold when no Active rating band starts at or above {@link KPI_MEETS_EXPECTATIONS_FLOOR}. */
export const KPI_DEFAULT_ATTENTION_THRESHOLD = 60;
/** The lowest minimum score a band may have to count as the "Meets Expectations" band. */
export const KPI_MEETS_EXPECTATIONS_FLOOR = 60;
/** At most this many scorecards in the HR dashboard's top performers. */
export const KPI_TOP_PERFORMER_COUNT = 5;
/** At most this many rows in the HR dashboard's attention list (the count covers all). */
export const KPI_ATTENTION_LIST_LIMIT = 50;
/** At most this many earlier scorecards in the employee dashboard's history. */
export const KPI_HISTORY_LIMIT = 12;

/** A value with at most 2 decimals, in hundredths. */
const hundredths = (value: number): number => Math.round(value * 100);

/** `numerator / denominator` (denominator > 0) rounded to a whole number, a midpoint away from zero. */
function divideRounded(numerator: number, denominator: number): number {
  const magnitude = Math.floor((2 * Math.abs(numerator) + denominator) / (2 * denominator));
  return numerator < 0 ? -magnitude : magnitude;
}

/** The mean of the values, 2 decimals; null when there are none (never 0). */
export function kpiAverage(values: readonly number[]): number | null {
  if (values.length === 0) return null;
  const sum = values.reduce((total, value) => total + hundredths(value), 0);
  return divideRounded(sum, values.length) / 100;
}

/**
 * Finalized ÷ evaluated × 100, 2 decimals, where evaluated is every scorecard past Planning. 0 when
 * nothing has been evaluated (never a division by zero).
 */
export function kpiCompletionRate(finalized: number, evaluated: number): number {
  return evaluated <= 0 ? 0 : divideRounded(finalized * 10_000, evaluated) / 100;
}

/**
 * The score below which a Finalized scorecard needs attention: the minimum score of the lowest
 * Active rating band whose minimum is at least 60 (taken as "Meets Expectations"), or 60 when there
 * is no such band.
 */
export function kpiAttentionThreshold(activeBandMinScores: readonly number[]): number {
  const candidates = activeBandMinScores.filter((score) => score >= KPI_MEETS_EXPECTATIONS_FLOOR);
  return candidates.length === 0 ? KPI_DEFAULT_ATTENTION_THRESHOLD : Math.min(...candidates);
}

/**
 * Standard competition ranks (1, 2, 2, 4) of scores already sorted from highest to lowest.
 * `firstRank` is the rank of the first score and `firstPosition` its 1-based position in the whole
 * ranking (they differ when a page starts in the middle of a tie).
 */
export function kpiCompetitionRanks(
  scoresDescending: readonly number[],
  firstRank = 1,
  firstPosition = 1,
): number[] {
  const ranks: number[] = [];
  scoresDescending.forEach((score, i) => {
    ranks.push(
      i === 0 ? firstRank : score === scoresDescending[i - 1] ? ranks[i - 1] : firstPosition + i,
    );
  });
  return ranks;
}

/** Every status with its count (0 when absent), in workflow order. */
export function kpiByStatus(statuses: readonly ScorecardStatus[]): KpiStatusCount[] {
  return SCORECARD_STATUSES.map((status) => ({
    status,
    count: statuses.filter((s) => s === status).length,
  }));
}

/** Ordinal text order (as the database compares employee codes). */
export const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** Highest score first, unscored last, then employee code, then scorecard id. */
export function byScoreThenCode(
  a: { overallScore: number | null; employeeCode: string; scorecardId: number },
  b: { overallScore: number | null; employeeCode: string; scorecardId: number },
): number {
  if (a.overallScore === null || b.overallScore === null) {
    if (a.overallScore !== b.overallScore) return a.overallScore === null ? 1 : -1;
  } else if (a.overallScore !== b.overallScore) {
    return b.overallScore - a.overallScore;
  }
  return ordinal(a.employeeCode, b.employeeCode) || a.scorecardId - b.scorecardId;
}

/**
 * The period a dashboard or report is about when none is named: the most recent Active candidate
 * (latest start date, then highest id), else the most recent candidate whatever its status, else none.
 */
export function kpiDefaultPeriod<
  P extends { id: number; startDate: string; status: EvaluationPeriodStatus },
>(candidates: readonly P[]): P | null {
  const ordered = [...candidates].sort(
    (a, b) =>
      (a.status === 'Active' ? 0 : 1) - (b.status === 'Active' ? 0 : 1) ||
      b.startDate.localeCompare(a.startDate) ||
      b.id - a.id,
  );
  return ordered[0] ?? null;
}
