import { Injectable } from '@angular/core';
import { Observable, catchError, of, switchMap, throwError } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { JobOpening, JobOpeningStatus } from '../../core/models/recruitment.models';
import { JobOpeningDataService } from '../../core/services/data/recruitment-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  conflict,
  pick,
  textOrNull,
} from './recruitment-http.base';

/** Fields of POST and PUT. `code` and `status` belong to the server. */
const FIELD_KEYS = [
  'title',
  'departmentId',
  'hiringManagerId',
  'employmentType',
  'vacancies',
  'postedOn',
  'closingDate',
  'description',
] as const;

/**
 * HTTP implementation of the job opening contract.
 *
 *  - Create and update send only the user-owned fields. A new opening is a Draft; `code` and
 *    `status` are never sent, and PUT never carries the status.
 *  - `changeStatus` maps onto the commands: Open to `open` (`reopen` from Closed, the same
 *    transition), On Hold to `hold`, Closed to `close`. `Filled` is set by the server when the
 *    selected count reaches the vacancies and `Draft` is only the starting state, so neither can
 *    be requested (a local conflict, no request).
 */
@Injectable({ providedIn: 'root' })
export class JobOpeningHttpService
  extends RecruitmentHttpBase<JobOpening>
  implements JobOpeningDataService
{
  protected readonly resource = 'job-openings';
  protected readonly filterKeys = ['status', 'departmentId', 'hiringManagerId', 'employmentType'];
  protected readonly sortKeys = [
    'id',
    'code',
    'title',
    'departmentId',
    'hiringManagerId',
    'employmentType',
    'vacancies',
    'postedOn',
    'closingDate',
    'status',
  ];

  protected normalise(raw: ApiShape<JobOpening>): Versioned<JobOpening> {
    return { ...raw, description: raw.description ?? '' } as Versioned<JobOpening>;
  }

  create(model: CreateModel<JobOpening>): Observable<JobOpening> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<JobOpening>): Observable<JobOpening> {
    const { rowVersion, status } = changes as { rowVersion?: string; status?: JobOpeningStatus };
    const patch = pick(changes, FIELD_KEYS);
    if (Object.keys(patch).length === 0) {
      return status === undefined ? this.getById(id) : this.changeStatus(id, status);
    }
    return this.current(id).pipe(
      switchMap((current) =>
        this.put(id, body({ ...current, ...patch }), rowVersion ?? current.rowVersion ?? ''),
      ),
      switchMap((saved) =>
        status === undefined || saved.status === status ? of(saved) : this.changeStatus(id, status),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  changeStatus(id: number, status: JobOpeningStatus): Observable<JobOpening> {
    if (status === 'Filled' || status === 'Draft') {
      return throwError(() =>
        conflict(
          status === 'Filled'
            ? 'An opening becomes filled by itself when every vacancy has a selected candidate.'
            : 'An opening cannot be set back to draft.',
        ),
      );
    }
    return this.current(id).pipe(
      switchMap((current) => {
        const command =
          status === 'Open' ? (current.status === 'Closed' ? 'reopen' : 'open') : commandOf(status);
        return this.command(id, command);
      }),
      catchError((e) => this.fail(e)),
    );
  }
}

function commandOf(status: JobOpeningStatus): string {
  return status === 'On Hold' ? 'hold' : 'close';
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    title: from['title'],
    departmentId: Number(from['departmentId']),
    hiringManagerId: Number(from['hiringManagerId']),
    employmentType: from['employmentType'],
    vacancies: Number(from['vacancies']),
    postedOn: textOrNull(from['postedOn']),
    closingDate: from['closingDate'],
    description: textOrNull(from['description']),
  };
}
