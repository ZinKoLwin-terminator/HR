import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { PayLine, Payslip, PayslipFilter } from '../../core/models/payroll.models';
import { PayslipDataService } from '../../core/services/data/payroll-data.services';
import { API_BASE_URL } from '../api-base-url';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';
import { mapHttpError } from '../http-error.mapper';

const SORT_KEYS = [
  'id',
  'runId',
  'employeeCode',
  'employeeName',
  'departmentName',
  'grossSalary',
  'netSalary',
  'generatedOn',
];
const FILTER_KEYS: readonly (keyof PayslipFilter)[] = ['runId', 'employeeId', 'departmentId'];

/**
 * HTTP implementation of the read-only payslip contract. Payslips are personal data: there is no
 * "get all" and no write. The API sends the masked account only (`accountMasked`), which is passed
 * through as it is; no screen can show a full account number.
 */
@Injectable({ providedIn: 'root' })
export class PayslipHttpService implements PayslipDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/payslips`;
  }

  getById(id: number): Observable<Payslip> {
    return this.http.get<Payslip>(`${this.url}/${id}`).pipe(
      map(normalise),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  query(request: PageRequest<PayslipFilter>): Observable<PagedResult<Payslip>> {
    let params = new HttpParams()
      .set('pageIndex', request.pageIndex)
      .set('pageSize', request.pageSize);
    if (request.sort?.active && request.sort.direction && SORT_KEYS.includes(request.sort.active)) {
      params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
    }
    const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
    if (search) {
      params = params.set('search', search);
    }
    for (const key of FILTER_KEYS) {
      const value = request.filters?.[key];
      if (value !== null && value !== undefined && String(value).trim() !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<Payslip>>(`${this.url}/query`, { params }).pipe(
      map((page) => ({ ...page, items: page.items.map(normalise) })),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }
}

function normalise(raw: Payslip): Payslip {
  const lines = (list: PayLine[] | null | undefined): PayLine[] => list ?? [];
  return { ...raw, earnings: lines(raw.earnings), deductions: lines(raw.deductions) };
}
