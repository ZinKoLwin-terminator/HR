import { Injectable } from '@angular/core';
import { Observable, catchError, switchMap } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { Candidate } from '../../core/models/recruitment.models';
import { CandidateDataService } from '../../core/services/data/recruitment-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  pick,
  textOrNull,
} from './recruitment-http.base';

const FIELD_KEYS = [
  'firstName',
  'lastName',
  'email',
  'phone',
  'source',
  'currentEmployer',
  'experienceYears',
  'resumeName',
  'notes',
  'nationalId',
] as const;

/**
 * HTTP implementation of the candidate contract. The API stores the email in lower case, and
 * blank optional text is sent as null. `nationalId` is write-only: the API never returns it, so a
 * row never has one. It is sent only when the caller supplies it (trimmed, at most 40 characters on
 * the form); a blank field is not sent, so the stored value is kept. `blacklistMatch` is read-only and
 * defaults to false. Update is a full PUT: fields the caller leaves out are
 * filled from the record as last seen.
 */
@Injectable({ providedIn: 'root' })
export class CandidateHttpService
  extends RecruitmentHttpBase<Candidate>
  implements CandidateDataService
{
  protected readonly resource = 'candidates';
  protected readonly filterKeys = ['source'];
  protected readonly sortKeys = [
    'id',
    'firstName',
    'lastName',
    'email',
    'source',
    'currentEmployer',
    'experienceYears',
  ];

  protected normalise(raw: ApiShape<Candidate>): Versioned<Candidate> {
    const row = {
      ...raw,
      phone: raw.phone ?? '',
      currentEmployer: raw.currentEmployer ?? '',
      notes: raw.notes ?? '',
      blacklistMatch: raw.blacklistMatch === true,
    } as Versioned<Candidate>;
    delete row.nationalId;
    return row;
  }

  create(model: CreateModel<Candidate>): Observable<Candidate> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<Candidate>): Observable<Candidate> {
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
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  const nationalId = from['nationalId'];
  return {
    // Write-only and never prefilled, so a blank field means "keep the stored value": send it only
    // when the user typed one.
    ...(typeof nationalId === 'string' && nationalId.trim()
      ? { nationalId: nationalId.trim() }
      : {}),
    firstName: from['firstName'],
    lastName: from['lastName'],
    email: typeof from['email'] === 'string' ? from['email'].trim().toLowerCase() : from['email'],
    phone: from['phone'],
    source: from['source'],
    currentEmployer: textOrNull(from['currentEmployer']),
    experienceYears: Number(from['experienceYears']),
    resumeName: textOrNull(from['resumeName']),
    notes: textOrNull(from['notes']),
  };
}
