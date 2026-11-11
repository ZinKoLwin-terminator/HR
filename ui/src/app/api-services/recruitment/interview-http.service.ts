import { Injectable } from '@angular/core';
import { Observable, catchError, of, switchMap, throwError } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { Interview, InterviewStatus } from '../../core/models/recruitment.models';
import { InterviewDataService } from '../../core/services/data/recruitment-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  conflict,
  hhmm,
  numberOrNull,
  pick,
  textOrNull,
  validation,
} from './recruitment-http.base';

const SCHEDULE_KEYS = [
  'applicationId',
  'round',
  'date',
  'startTime',
  'durationMinutes',
  'mode',
  'interviewerId',
  'location',
] as const;

/**
 * HTTP implementation of the interview contract.
 *
 *  - Create sends the schedule fields. A `Completed` create (a past interview) also sends the
 *    outcome fields in the same request; `Scheduled` sends neither status nor outcome.
 *  - PUT reschedules and never carries the status or the outcome. It is only sent when a schedule
 *    field really changed.
 *  - The status moves through commands, after the PUT when both are needed: Completed is the
 *    `outcome` command (`{ rowVersion, score, recommendation, feedback }`, also used to correct a
 *    completed interview) and Cancelled is the `cancel` command (`{ rowVersion, reason? }`, the
 *    feedback text is sent as the reason). An update with only `{ status: 'Cancelled' }` is the
 *    list's cancel action and sends the command alone.
 *  - A cancelled interview is locked, and a completed one can be neither cancelled nor set back to
 *    Scheduled: those are refused locally, without a request, as the API would refuse them.
 */
@Injectable({ providedIn: 'root' })
export class InterviewHttpService
  extends RecruitmentHttpBase<Interview>
  implements InterviewDataService
{
  protected readonly resource = 'interviews';
  protected readonly filterKeys = [
    'status',
    'interviewerId',
    'applicationId',
    'dateFrom',
    'dateTo',
  ];
  protected readonly sortKeys = [
    'id',
    'applicationId',
    'round',
    'date',
    'startTime',
    'durationMinutes',
    'mode',
    'interviewerId',
    'status',
    'score',
  ];

  protected normalise(raw: ApiShape<Interview>): Versioned<Interview> {
    return {
      ...raw,
      startTime: hhmm(raw.startTime) ?? '',
      location: raw.location ?? '',
      feedback: raw.feedback ?? '',
    } as Versioned<Interview>;
  }

  create(model: CreateModel<Interview>): Observable<Interview> {
    const from = model as Record<string, unknown>;
    const status = (from['status'] as InterviewStatus | undefined) ?? 'Scheduled';
    if (status === 'Cancelled') {
      return throwError(() =>
        validation(
          'Schedule the interview first, then cancel it.',
          'status',
          'A new interview cannot be cancelled.',
        ),
      );
    }
    const outcome =
      status === 'Completed'
        ? {
            status,
            score: numberOrNull(from['score']),
            recommendation: textOrNull(from['recommendation']),
            feedback: textOrNull(from['feedback']),
          }
        : {};
    return this.post('', { ...scheduleBody(from), ...outcome }).pipe(
      catchError((e) => this.fail(e)),
    );
  }

  update(id: number, changes: UpdateModel<Interview>): Observable<Interview> {
    const given = changes as Record<string, unknown>;
    const rowVersion = given['rowVersion'] as string | undefined;
    return this.current(id).pipe(
      switchMap((current) => {
        const target = (given['status'] as InterviewStatus | undefined) ?? current.status;
        const refusal = refuse(current.status, target);
        if (refusal) {
          return throwError(() => refusal);
        }
        const patch = pick(changes, SCHEDULE_KEYS);
        const merged = scheduleBody({ ...current, ...patch });
        const reschedule =
          Object.keys(patch).length > 0 &&
          JSON.stringify(merged) !== JSON.stringify(scheduleBody(current));
        const saved$ = reschedule
          ? this.put(id, merged, rowVersion ?? current.rowVersion ?? '')
          : of(current);
        return saved$.pipe(
          switchMap((saved) =>
            this.moveStatus(saved, target, given, reschedule ? undefined : rowVersion),
          ),
        );
      }),
      catchError((e) => this.fail(e)),
    );
  }

  private moveStatus(
    saved: Versioned<Interview>,
    target: InterviewStatus,
    given: Record<string, unknown>,
    explicitVersion: string | undefined,
  ): Observable<Interview> {
    if (target === 'Cancelled') {
      const reason = textOrNull(given['feedback']);
      return this.command(saved.id, 'cancel', reason === null ? {} : { reason }, explicitVersion);
    }
    if (target === 'Completed') {
      const score = numberOrNull('score' in given ? given['score'] : saved.score);
      const recommendation = textOrNull(
        'recommendation' in given ? given['recommendation'] : saved.recommendation,
      );
      const feedback = textOrNull('feedback' in given ? given['feedback'] : saved.feedback);
      const unchanged =
        saved.status === 'Completed' &&
        score === saved.score &&
        recommendation === saved.recommendation &&
        feedback === textOrNull(saved.feedback);
      return unchanged
        ? of(saved)
        : this.command(saved.id, 'outcome', { score, recommendation, feedback }, explicitVersion);
    }
    return of(saved);
  }
}

/** The transitions the API refuses, caught before any request. */
function refuse(from: InterviewStatus, to: InterviewStatus) {
  if (from === 'Cancelled') {
    return conflict('A cancelled interview cannot be changed. Schedule a new one instead.');
  }
  if (from === 'Completed' && to === 'Cancelled') {
    return conflict('A completed interview is part of the hiring record and cannot be cancelled.');
  }
  if (from === 'Completed' && to === 'Scheduled') {
    return validation(
      'A completed interview cannot be set back to scheduled.',
      'status',
      'Completed interviews stay completed.',
    );
  }
  return null;
}

function scheduleBody(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    applicationId: Number(from['applicationId']),
    round: from['round'],
    date: from['date'],
    startTime: hhmm(from['startTime']),
    durationMinutes: Number(from['durationMinutes']),
    mode: from['mode'],
    interviewerId: Number(from['interviewerId']),
    location: textOrNull(from['location']),
  };
}
