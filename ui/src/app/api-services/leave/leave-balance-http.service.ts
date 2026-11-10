import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { PagedResult, isAppError } from '../../core/models/common.models';
import {
  EmployeeLeaveBalances,
  LeaveBalance,
  LeaveBalanceQuery,
} from '../../core/models/leave.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { LeaveBalanceDataService } from '../../core/services/data/leave-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/** Table column keys that name a field of the employee list rather than of the balance row. */
const SORT_KEYS: Record<string, string> = {
  employeeName: 'fullName',
};

/**
 * HTTP implementation of the leave balance contract.
 *
 *  - `getForEmployee`: `GET /leave/balances?employeeId&year`.
 *  - `query` (all employees): the API has no all-employees balance endpoint, so the adapter pages the
 *    employee list (search, department, branch, sort and paging are done by the employee API) and
 *    then calls `/leave/balances` once per employee on the visible page only. A failure of any of
 *    those calls fails the page; nothing is returned as empty.
 *
 * Limit: the employee API filters by ONE employment status, so exited employees are not excluded
 * here the way the mock excludes them. See the report for the suggested API change.
 */
@Injectable({ providedIn: 'root' })
export class LeaveBalanceHttpService implements LeaveBalanceDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly employees = inject(EmployeeDataService);

  getForEmployee(employeeId: number, year: number): Observable<LeaveBalance[]> {
    const params = new HttpParams().set('employeeId', employeeId).set('year', year);
    return this.http
      .get<LeaveBalance[]>(`${this.baseUrl}/leave/balances`, { params })
      .pipe(catchError((e) => this.fail(e)));
  }

  query(request: LeaveBalanceQuery): Observable<PagedResult<EmployeeLeaveBalances>> {
    const sort =
      request.sort?.active && request.sort.direction
        ? { ...request.sort, active: SORT_KEYS[request.sort.active] ?? request.sort.active }
        : { active: 'employeeCode', direction: 'asc' as const };
    return this.employees
      .query({
        pageIndex: request.pageIndex,
        pageSize: request.pageSize,
        sort,
        search: request.search,
        filters: { departmentId: request.departmentId, branchId: request.branchId },
      })
      .pipe(
        switchMap((page) =>
          (page.items.length === 0
            ? of([] as LeaveBalance[][])
            : forkJoin(page.items.map((e) => this.getForEmployee(e.id, request.year)))
          ).pipe(
            map((balances) => ({
              items: page.items.map((e, i): EmployeeLeaveBalances => ({
                employeeId: e.id,
                employeeCode: e.employeeCode,
                employeeName: e.fullName,
                departmentId: e.departmentId,
                departmentName: e.departmentName,
                branchId: e.branchId,
                branchName: e.branchName,
                balances: balances[i],
              })),
              total: page.total,
              pageIndex: page.pageIndex,
              pageSize: page.pageSize,
            })),
          ),
        ),
        catchError((e) => this.fail(e)),
      );
  }

  private fail(error: unknown): Observable<never> {
    return throwError(() => (isAppError(error) ? error : mapHttpError(error)));
  }
}
