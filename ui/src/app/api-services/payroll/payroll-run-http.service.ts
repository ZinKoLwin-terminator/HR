import { Injectable } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { PayrollRun } from '../../core/models/payroll.models';
import { PayrollRunDataService } from '../../core/services/data/payroll-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  textOrNull,
  validation,
} from '../recruitment/recruitment-http.base';

/** The API rejects a longer reason. */
export const MAX_RETURN_REASON_LENGTH = 200;

/**
 * HTTP implementation of the payroll run contract (payroll periods and their workflow).
 *
 *  - Create sends `{ year, month, payDate? }` and PUT the same plus `rowVersion` (Draft only). The
 *    name, dates, totals and status belong to the server; the status never changes through PUT.
 *  - `process`, `approve`, `markPaid` and `returnToDraft` map to the commands of the same name
 *    (`/process`, `/approve`, `/mark-paid`, `/return-to-draft`), each with the `rowVersion` last
 *    seen. `returnToDraft` also sends the optional reason (blank is left out, over 200 characters
 *    is a validation error and no request).
 *  - The server decides the rules (self-approval, locked runs, earlier periods unfinished) and
 *    answers 409 with the message the user sees.
 */
@Injectable({ providedIn: 'root' })
export class PayrollRunHttpService
  extends RecruitmentHttpBase<PayrollRun>
  implements PayrollRunDataService
{
  protected readonly resource = 'payroll-runs';
  protected readonly filterKeys = ['status', 'year'];
  protected readonly sortKeys = [
    'id',
    'periodName',
    'year',
    'month',
    'startDate',
    'payDate',
    'status',
    'employeeCount',
    'totalGross',
    'totalNet',
  ];

  protected normalise(raw: ApiShape<PayrollRun>): Versioned<PayrollRun> {
    return { ...raw, warnings: raw.warnings ?? [] } as Versioned<PayrollRun>;
  }

  create(model: CreateModel<PayrollRun>): Observable<PayrollRun> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<PayrollRun>): Observable<PayrollRun> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.current(id).pipe(
      switchMap((current) =>
        this.put(id, body({ ...current, ...changes }), rowVersion ?? current.rowVersion ?? ''),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  process(id: number): Observable<PayrollRun> {
    return this.command(id, 'process').pipe(catchError((e) => this.fail(e)));
  }

  approve(id: number): Observable<PayrollRun> {
    return this.command(id, 'approve').pipe(catchError((e) => this.fail(e)));
  }

  markPaid(id: number): Observable<PayrollRun> {
    return this.command(id, 'mark-paid').pipe(catchError((e) => this.fail(e)));
  }

  returnToDraft(id: number, reason: string): Observable<PayrollRun> {
    const text = textOrNull(reason);
    if (text !== null && text.length > MAX_RETURN_REASON_LENGTH) {
      return throwError(() =>
        validation(
          `The reason can be at most ${MAX_RETURN_REASON_LENGTH} characters.`,
          'reason',
          `Use at most ${MAX_RETURN_REASON_LENGTH} characters.`,
        ),
      );
    }
    return this.command(id, 'return-to-draft', text === null ? {} : { reason: text }).pipe(
      catchError((e) => this.fail(e)),
    );
  }
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    year: Number(from['year']),
    month: Number(from['month']),
    payDate: textOrNull(from['payDate']),
  };
}
