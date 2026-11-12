import { Injectable } from '@angular/core';
import { EMPTY, Observable, catchError, expand, map, reduce, switchMap } from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import { SalaryAssignment } from '../../core/models/payroll.models';
import { SalaryAssignmentDataService } from '../../core/services/data/payroll-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  numberOrNull,
  textOrNull,
} from '../recruitment/recruitment-http.base';

/** The API's largest page (it clamps anything bigger). */
export const ASSIGNMENT_PAGE_SIZE = 200;

/**
 * HTTP implementation of the salary assignment contract. Salaries are personal data, so the API has
 * no "get all": `getAll` reads `/query` page by page (200 rows each) until every row is in. The
 * Current, Scheduled and Previous labels are derived by the screen, as with the mock. Create and
 * PUT send `employeeId, structureId, basicSalary, effectiveFrom, remarks`; PUT adds `rowVersion`
 * (the employee cannot change). Delete needs `payroll:Edit`.
 */
@Injectable({ providedIn: 'root' })
export class SalaryAssignmentHttpService
  extends RecruitmentHttpBase<SalaryAssignment>
  implements SalaryAssignmentDataService
{
  protected readonly resource = 'salary-assignments';
  protected readonly filterKeys = ['employeeId', 'structureId'];
  protected readonly sortKeys = ['id', 'employeeId', 'structureId', 'basicSalary', 'effectiveFrom'];

  protected normalise(raw: ApiShape<SalaryAssignment>): Versioned<SalaryAssignment> {
    return { ...raw, remarks: raw.remarks ?? '' } as Versioned<SalaryAssignment>;
  }

  override getAll(): Observable<SalaryAssignment[]> {
    const page = (pageIndex: number) => this.query({ pageIndex, pageSize: ASSIGNMENT_PAGE_SIZE });
    return page(0).pipe(
      expand((result) =>
        (result.pageIndex + 1) * result.pageSize < result.total
          ? page(result.pageIndex + 1)
          : EMPTY,
      ),
      map((result) => result.items),
      reduce((all, items) => [...all, ...items], [] as SalaryAssignment[]),
      catchError((e) => this.fail(e)),
    );
  }

  create(model: CreateModel<SalaryAssignment>): Observable<SalaryAssignment> {
    return this.post('', body(model)).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<SalaryAssignment>): Observable<SalaryAssignment> {
    const { rowVersion } = changes as { rowVersion?: string };
    return this.current(id).pipe(
      switchMap((current) =>
        this.put(id, body({ ...current, ...changes }), rowVersion ?? current.rowVersion ?? ''),
      ),
      catchError((e) => this.fail(e)),
    );
  }
}

function body(source: object): Record<string, unknown> {
  const from = source as Record<string, unknown>;
  return {
    employeeId: Number(from['employeeId']),
    structureId: Number(from['structureId']),
    basicSalary: numberOrNull(from['basicSalary']),
    effectiveFrom: from['effectiveFrom'],
    remarks: textOrNull(from['remarks']),
  };
}
