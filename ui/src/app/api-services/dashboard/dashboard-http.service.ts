import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { DashboardSummary } from '../../core/models/dashboard.models';
import { isAppError } from '../../core/models/common.models';
import { DashboardDataService } from '../../core/services/data/dashboard-data.service';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * HTTP implementation of the dashboard contract. One call (`GET /dashboard/summary`) returns the
 * whole summary in the shape of the mock's `DashboardSummary`, so nothing is remapped.
 * A null payroll stays null. Needs the `dashboard:View` permission.
 */
@Injectable({ providedIn: 'root' })
export class DashboardHttpService implements DashboardDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getSummary(): Observable<DashboardSummary> {
    return this.http
      .get<DashboardSummary>(`${this.baseUrl}/dashboard/summary`)
      .pipe(catchError((e) => throwError(() => (isAppError(e) ? e : mapHttpError(e)))));
  }
}
