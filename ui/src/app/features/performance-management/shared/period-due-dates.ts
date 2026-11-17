import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { Scorecard } from '../../../core/models/kpi.models';
import { ScorecardDataService } from '../../../core/services/data/kpi-data.services';
import { isLateSubmission } from './scorecard-rules';

/** Submission due date by period id (null: the period has none, or it could not be read). */
export type DueDates = ReadonlyMap<number, string | null>;

/**
 * The due dates of the periods of a page of scorecards, for the Late chip. The list answer has no
 * due date and the period picker has none either (only the setup endpoint, which employees and
 * managers cannot call), so this reads ONE scorecard detail per distinct period of the submitted
 * rows: usually one or two calls per page. A failed read just leaves that period without a chip.
 */
export function loadDueDates(
  scorecards: ScorecardDataService,
  rows: readonly Scorecard[],
): Observable<DueDates> {
  const sample = new Map<number, number>();
  for (const row of rows) {
    if (row.submittedOn && !sample.has(row.periodId)) sample.set(row.periodId, row.id);
  }
  if (sample.size === 0) return of(new Map());
  return forkJoin(
    [...sample].map(([periodId, id]) =>
      scorecards.getById(id).pipe(
        map((card) => [periodId, card.submissionDueDate] as const),
        catchError(() => of([periodId, null] as const)),
      ),
    ),
  ).pipe(map((entries) => new Map(entries)));
}

/** Whether a row of a list was submitted late, given the due dates loaded for its page. */
export const rowIsLate = (row: Scorecard, dueDates: DueDates | undefined): boolean =>
  isLateSubmission(row.submittedOn, dueDates?.get(row.periodId));
