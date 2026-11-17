import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import {
  KPI_REPORT_FILTER_KEYS,
  KPI_REPORT_SORT_KEYS,
  KpiAchievementRow,
  KpiDepartmentReport,
  KpiEmployeeDashboard,
  KpiEmployeeReport,
  KpiHrDashboard,
  KpiManagerDashboard,
  KpiPeriodSummary,
  KpiRankingRow,
  KpiReportFilter,
  KpiTargetVsActualRow,
} from '../../core/models/kpi-insight.models';
import { KpiInsightDataService } from '../../core/services/data/kpi-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';
import { buildPageParams } from './kpi-http.util';

/** The query of a dashboard: only the ids that are set. */
function idParams(values: Record<string, number | null | undefined>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined) params = params.set(key, value);
  }
  return params;
}

/**
 * HTTP implementation of the KPI dashboards and reports (`/api/kpi/dashboard/*`,
 * `/api/kpi/reports/*`). Read only; the answers are the server's figures, passed through unchanged.
 * Paging, the search and the report filters go in the query string; `sort` is sent only for a field
 * of the report's whitelist (`KPI_REPORT_SORT_KEYS`), and never for the ranking, whose order is fixed
 * (the API accepts only `rank:asc` there, which is its default anyway).
 */
@Injectable({ providedIn: 'root' })
export class KpiInsightHttpService implements KpiInsightDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/kpi`;
  }

  employeeDashboard(periodId?: number | null): Observable<KpiEmployeeDashboard> {
    return this.get(`${this.url}/dashboard/employee`, idParams({ periodId }));
  }

  managerDashboard(periodId?: number | null): Observable<KpiManagerDashboard> {
    return this.get(`${this.url}/dashboard/manager`, idParams({ periodId }));
  }

  hrDashboard(periodId?: number | null, companyId?: number | null): Observable<KpiHrDashboard> {
    return this.get(`${this.url}/dashboard/hr`, idParams({ periodId, companyId }));
  }

  summaryReport(request: PageRequest<KpiReportFilter>): Observable<PagedResult<KpiPeriodSummary>> {
    return this.get(
      `${this.url}/reports/summary`,
      this.params(request, KPI_REPORT_SORT_KEYS.summary),
    );
  }

  employeeReport(
    employeeId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiEmployeeReport> {
    // The route id replaces the filter, and the employee report takes no search.
    const { employeeId: _ignored, ...filters } = request.filters ?? {};
    void _ignored;
    return this.get(
      `${this.url}/reports/employee/${encodeURIComponent(employeeId)}`,
      this.params({ ...request, search: undefined, filters }, KPI_REPORT_SORT_KEYS.employee),
    );
  }

  departmentReport(
    departmentId: number,
    request: PageRequest<KpiReportFilter>,
  ): Observable<KpiDepartmentReport> {
    const { departmentId: _ignored, ...filters } = request.filters ?? {};
    void _ignored;
    return this.get(
      `${this.url}/reports/department/${encodeURIComponent(departmentId)}`,
      this.params({ ...request, filters }, KPI_REPORT_SORT_KEYS.department),
    );
  }

  rankingReport(request: PageRequest<KpiReportFilter>): Observable<PagedResult<KpiRankingRow>> {
    return this.get(
      `${this.url}/reports/ranking`,
      this.params(request, KPI_REPORT_SORT_KEYS.ranking),
    );
  }

  kpiAchievementReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiAchievementRow>> {
    return this.get(
      `${this.url}/reports/kpi-achievement`,
      this.params(request, KPI_REPORT_SORT_KEYS.kpiAchievement),
    );
  }

  targetVsActualReport(
    request: PageRequest<KpiReportFilter>,
  ): Observable<PagedResult<KpiTargetVsActualRow>> {
    return this.get(
      `${this.url}/reports/target-vs-actual`,
      this.params(request, KPI_REPORT_SORT_KEYS.targetVsActual),
    );
  }

  private params(request: PageRequest<KpiReportFilter>, sortKeys: readonly string[]): HttpParams {
    return buildPageParams(request, sortKeys, KPI_REPORT_FILTER_KEYS);
  }

  private get<T>(url: string, params: HttpParams): Observable<T> {
    return this.http
      .get<T>(url, { params })
      .pipe(catchError((error: unknown) => throwError(() => mapHttpError(error))));
  }
}
