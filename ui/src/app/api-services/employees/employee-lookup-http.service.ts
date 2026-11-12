import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { PagedResult } from '../../core/models/common.models';
import { EmployeeLookupItem } from '../../core/models/employee.models';
import { EmployeeLookupDataService } from '../../core/services/data/employee-data.service';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

/** The API serves at most this many lookup rows per page. */
const MAX_PAGE_SIZE = 200;

/**
 * HTTP implementation of the employee picker: `GET /api/employees/lookup?search&pageIndex&pageSize`
 * (allowed with `recruitment:Edit` or `employees:View`). Current employees only, with no personal
 * data. Nothing is cached, so a picker always shows the latest employees.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeLookupHttpService implements EmployeeLookupDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  query(request: {
    search?: string;
    pageIndex: number;
    pageSize: number;
  }): Observable<PagedResult<EmployeeLookupItem>> {
    let params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', Math.min(Math.max(request.pageSize || 20, 1), MAX_PAGE_SIZE));
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    return this.http
      .get<PagedResult<EmployeeLookupItem>>(`${this.baseUrl}/employees/lookup`, { params })
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }
}
