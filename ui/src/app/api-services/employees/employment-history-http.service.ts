import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { AppError, PagedResult, PageRequest } from '../../core/models/common.models';
import { EmploymentHistoryEntry, HistoryEventType } from '../../core/models/employee-extras.models';
import { EmploymentHistoryDataService } from '../../core/services/data/employee-extras-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';
import { HistoryEntryApi } from './employee-api.mapper';

const READ_ONLY: AppError = {
  code: 'Forbidden',
  message: 'Employment history is recorded by the system and cannot be edited.',
};

/**
 * Read-only HTTP implementation of the employment history contract
 * (`GET /api/employees/{id}/history`). The endpoint returns the whole list for one employee, so
 * sorting and paging are applied here. The API records history itself: create, update and delete
 * are refused, and there is no way to list the history of every employee at once.
 */
@Injectable({ providedIn: 'root' })
export class EmploymentHistoryHttpService implements EmploymentHistoryDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  query(
    request: PageRequest<Record<string, unknown>>,
  ): Observable<PagedResult<EmploymentHistoryEntry>> {
    const employeeId = Number(request.filters?.['employeeId']);
    if (!Number.isFinite(employeeId)) {
      return throwError(() => ({
        code: 'Validation',
        message: 'An employee is required to read the history.',
      }));
    }
    return this.http
      .get<HistoryEntryApi[] | { items: HistoryEntryApi[] }>(
        `${this.baseUrl}/employees/${employeeId}/history`,
      )
      .pipe(
        map((body) => (Array.isArray(body) ? body : body.items)),
        map((entries) => entries.map(toEntry)),
        map((entries) => {
          const direction = request.sort?.direction === 'asc' ? 1 : -1;
          const sorted = [...entries].sort(
            (a, b) => direction * (a.date.localeCompare(b.date) || a.id - b.id),
          );
          const start = request.pageIndex * request.pageSize;
          return {
            items: sorted.slice(start, start + request.pageSize),
            total: sorted.length,
            pageIndex: request.pageIndex,
            pageSize: request.pageSize,
          };
        }),
        catchError((e) => throwError(() => mapHttpError(e))),
      );
  }

  getAll(): Observable<EmploymentHistoryEntry[]> {
    return throwError(() => ({
      code: 'Validation',
      message: 'History is read per employee.',
    }));
  }

  getById(): Observable<EmploymentHistoryEntry> {
    return throwError(() => ({ code: 'NotFound', message: 'History is read per employee.' }));
  }

  create(): Observable<EmploymentHistoryEntry> {
    return throwError(() => READ_ONLY);
  }

  update(): Observable<EmploymentHistoryEntry> {
    return throwError(() => READ_ONLY);
  }

  delete(): Observable<void> {
    return throwError(() => READ_ONLY);
  }
}

function toEntry(api: HistoryEntryApi): EmploymentHistoryEntry {
  return {
    id: api.id,
    employeeId: api.employeeId,
    date: api.date,
    type: api.type as HistoryEventType,
    title: api.title,
    details: api.details ?? '',
    recordedBy: api.recordedBy ?? '',
  };
}
