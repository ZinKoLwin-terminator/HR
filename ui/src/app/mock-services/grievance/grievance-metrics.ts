import {
  GRIEVANCE_NOT_DISCLOSED_LABEL,
  GRIEVANCE_PRIORITIES,
  GRIEVANCE_STATUSES,
  GrievancePriority,
  GrievanceStatus,
} from '../../core/models/grievance.models';
import {
  GRIEVANCE_DEPARTMENT_MIN_GROUP,
  GRIEVANCE_SUPPRESSED_LABEL,
  GrievanceCategoryCount,
  GrievanceDepartmentCount,
  GrievanceMonthlyCount,
  GrievancePriorityCount,
  GrievanceStatusCount,
} from '../../core/models/grievance-insight.models';
import { GrievanceCaseRecord } from '../../mock-data/grievance/grievance-records';
import { addMonths, diffInDays, startOfMonth } from '../../shared/utilities/date.util';

/**
 * The grievance figures, as defined in `grievance-insight.models.ts` (one definition each). DEMO
 * MIRROR of the API's figures: the API is authoritative. Pure functions over case records.
 */

/** Rounded half away from zero to `places` decimals (decimal-string based, no float drift). */
export function roundHalfAwayFromZero(value: number, places: number): number {
  const sign = value < 0 ? -1 : 1;
  const shifted = Math.round(Number(`${Math.abs(value)}e${places}`));
  return sign * Number(`${shifted}e-${places}`);
}

/** The date part of an ISO date-time (the demo reads UTC dates as business dates). */
const dateOf = (iso: string | null): string | null => (iso === null ? null : iso.slice(0, 10));

/** Every case that is not a Draft (archived ones included). */
export function countedCases(records: readonly GrievanceCaseRecord[]): GrievanceCaseRecord[] {
  return records.filter((r) => r.status !== 'Draft');
}

const resolvedNow = (r: GrievanceCaseRecord): boolean =>
  r.status === 'Resolved' || r.status === 'Closed';

/** Calendar days from the submission date to the resolution date; null until resolved. */
export function resolutionDays(record: GrievanceCaseRecord): number | null {
  const submitted = dateOf(record.submittedAtUtc);
  const resolved = dateOf(record.resolvedAtUtc);
  return resolvedNow(record) && submitted !== null && resolved !== null
    ? diffInDays(submitted, resolved)
    : null;
}

/** (Resolved + Closed) ÷ (counted − Rejected) × 100, 2 decimals; 0 when the divisor is 0. */
export function resolutionRatePct(counted: readonly GrievanceCaseRecord[]): number {
  const divisor = counted.filter((r) => r.status !== 'Rejected').length;
  if (divisor === 0) {
    return 0;
  }
  return roundHalfAwayFromZero((counted.filter(resolvedNow).length * 100) / divisor, 2);
}

/** Mean resolution days of the cases now Resolved or Closed, 1 decimal; null when none. */
export function averageResolutionDays(counted: readonly GrievanceCaseRecord[]): number | null {
  const days = counted.map(resolutionDays).filter((d): d is number => d !== null);
  if (days.length === 0) {
    return null;
  }
  return roundHalfAwayFromZero(days.reduce((sum, d) => sum + d, 0) / days.length, 1);
}

/** The 11 non-Draft statuses in workflow order, zeros included. */
export function statusCounts(counted: readonly GrievanceCaseRecord[]): GrievanceStatusCount[] {
  return GRIEVANCE_STATUSES.filter((s) => s !== 'Draft').map((status) => ({
    status,
    count: counted.filter((r) => r.status === status).length,
  }));
}

/** The four priorities then null (not acknowledged), zeros included. */
export function priorityCounts(counted: readonly GrievanceCaseRecord[]): GrievancePriorityCount[] {
  const keys: (GrievancePriority | null)[] = [...GRIEVANCE_PRIORITIES, null];
  return keys.map((priority) => ({
    priority,
    count: counted.filter((r) => r.priority === priority).length,
  }));
}

/** Categories with at least one case, most cases first, then by name. */
export function categoryCounts(
  counted: readonly GrievanceCaseRecord[],
  nameOf: (categoryId: number) => string,
): GrievanceCategoryCount[] {
  const counts = new Map<number, number>();
  for (const r of counted) {
    counts.set(r.categoryId, (counts.get(r.categoryId) ?? 0) + 1);
  }
  return [...counts.entries()]
    .map(([categoryId, count]) => ({ categoryId, categoryName: nameOf(categoryId), count }))
    .sort((a, b) => b.count - a.count || a.categoryName.localeCompare(b.categoryName));
}

/**
 * Departments copied at submission. Anonymous cases, and named ones without a department, are
 * "Not disclosed"; departments with fewer than 3 cases are merged into "Other / suppressed". Order:
 * departments by name, then Not disclosed, then Other / suppressed (merged groups only when > 0).
 */
export function departmentCounts(
  counted: readonly GrievanceCaseRecord[],
  nameOf: (departmentId: number) => string,
): GrievanceDepartmentCount[] {
  let notDisclosed = 0;
  const counts = new Map<number, number>();
  for (const r of counted) {
    if (r.isAnonymous || r.departmentId === null) {
      notDisclosed++;
    } else {
      counts.set(r.departmentId, (counts.get(r.departmentId) ?? 0) + 1);
    }
  }
  let suppressed = 0;
  const shown: GrievanceDepartmentCount[] = [];
  for (const [departmentId, count] of counts) {
    if (count < GRIEVANCE_DEPARTMENT_MIN_GROUP) {
      suppressed += count;
    } else {
      shown.push({
        bucket: 'Department',
        departmentId,
        departmentName: nameOf(departmentId),
        count,
      });
    }
  }
  shown.sort((a, b) => a.departmentName.localeCompare(b.departmentName));
  if (notDisclosed > 0) {
    shown.push({
      bucket: 'NotDisclosed',
      departmentId: null,
      departmentName: GRIEVANCE_NOT_DISCLOSED_LABEL,
      count: notDisclosed,
    });
  }
  if (suppressed > 0) {
    shown.push({
      bucket: 'Suppressed',
      departmentId: null,
      departmentName: GRIEVANCE_SUPPRESSED_LABEL,
      count: suppressed,
    });
  }
  return shown;
}

/** The 12 months up to the current one, oldest first, zeros included. */
export function monthlyTrend(
  counted: readonly GrievanceCaseRecord[],
  today: string,
): GrievanceMonthlyCount[] {
  const first = startOfMonth(today);
  const months = Array.from({ length: 12 }, (_, i) => addMonths(first, i - 11).slice(0, 7));
  return months.map((month) => ({
    month,
    submitted: counted.filter((r) => dateOf(r.submittedAtUtc)?.slice(0, 7) === month).length,
    resolved: counted.filter(
      (r) => resolvedNow(r) && dateOf(r.resolvedAtUtc)?.slice(0, 7) === month,
    ).length,
  }));
}

/** How many of the counted cases have one of the statuses. */
export function countIn(
  counted: readonly GrievanceCaseRecord[],
  statuses: readonly GrievanceStatus[],
): number {
  return counted.filter((r) => statuses.includes(r.status)).length;
}
