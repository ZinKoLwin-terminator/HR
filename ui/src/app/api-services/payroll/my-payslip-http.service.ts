import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { PagedResult } from '../../core/models/common.models';
import { PayLine, Payslip } from '../../core/models/payroll.models';
import { MyPayslipDataService } from '../../core/services/data/payroll-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * HTTP implementation of "My payslips": `GET /api/me/payslips?pageIndex&pageSize` and
 * `GET /api/me/payslips/{id}`. The server decides which employee is meant (from the session), so
 * no employee id is ever sent. It returns payslips of Approved or Paid runs only, newest first,
 * with the masked account only. An id that is not the caller's own is 404.
 */
@Injectable({ providedIn: 'root' })
export class MyPayslipHttpService implements MyPayslipDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/me/payslips`;
  }

  query(request: { pageIndex: number; pageSize: number }): Observable<PagedResult<Payslip>> {
    const params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    return this.http.get<PagedResult<Payslip>>(this.url, { params }).pipe(
      map((page) => ({ ...page, items: page.items.map(normalise) })),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  getById(id: number): Observable<Payslip> {
    return this.http.get<Payslip>(`${this.url}/${id}`).pipe(
      map(normalise),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }
}

function normalise(raw: Payslip): Payslip {
  const lines = (list: PayLine[] | null | undefined): PayLine[] => list ?? [];
  return { ...raw, earnings: lines(raw.earnings), deductions: lines(raw.deductions) };
}
