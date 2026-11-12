import { Injectable } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { Application, ApplicationStage } from '../../core/models/recruitment.models';
import { ApplicationDataService } from '../../core/services/data/recruitment-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  pick,
  textOrNull,
  validation,
} from './recruitment-http.base';

const FIELD_KEYS = ['jobOpeningId', 'candidateId', 'notes'] as const;

/**
 * HTTP implementation of the application contract.
 *
 *  - Create sends `{ jobOpeningId, candidateId, notes }`. The stage (Applied) and the date are set
 *    by the server.
 *  - Update changes the notes only; the opening and the candidate are sent as the record has them
 *    (the API answers 400 if they differ).
 *  - `moveStage` is the `stage` command with `{ rowVersion, stage, reason?, blacklistOverrideReason? }`.
 *    A rejection without a reason is refused locally, before any request, with a field error on
 *    `reason`. The override reason is only sent when selecting a candidate; the API answers 409
 *    when a matching candidate is selected without it and 403 when a caller who is not an HR
 *    Administrator sends it.
 */
@Injectable({ providedIn: 'root' })
export class ApplicationHttpService
  extends RecruitmentHttpBase<Application>
  implements ApplicationDataService
{
  protected readonly resource = 'applications';
  protected readonly filterKeys = ['jobOpeningId', 'candidateId', 'stage'];
  protected readonly sortKeys = ['id', 'jobOpeningId', 'candidateId', 'stage', 'appliedOn'];

  protected normalise(raw: ApiShape<Application>): Versioned<Application> {
    return {
      ...raw,
      notes: raw.notes ?? '',
      blacklistMatch: raw.blacklistMatch === true,
    } as Versioned<Application>;
  }

  create(model: CreateModel<Application>): Observable<Application> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<Application>): Observable<Application> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.current(id).pipe(
      switchMap((current) =>
        this.put(
          id,
          body({ ...current, ...pick(changes, FIELD_KEYS) }),
          rowVersion ?? current.rowVersion ?? '',
        ),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  moveStage(
    id: number,
    stage: ApplicationStage,
    reason = '',
    blacklistOverrideReason = '',
  ): Observable<Application> {
    const text = reason.trim();
    const override = blacklistOverrideReason.trim();
    if (stage === 'Rejected' && !text) {
      return throwError(() =>
        validation(
          'Enter a reason for rejecting this application.',
          'reason',
          'A reason is required.',
        ),
      );
    }
    return this.command(id, 'stage', {
      stage,
      ...(stage === 'Rejected' ? { reason: text } : {}),
      ...(stage === 'Selected' && override ? { blacklistOverrideReason: override } : {}),
    }).pipe(catchError((e) => this.fail(e)));
  }
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    jobOpeningId: Number(from['jobOpeningId']),
    candidateId: Number(from['candidateId']),
    notes: textOrNull(from['notes']),
  };
}
