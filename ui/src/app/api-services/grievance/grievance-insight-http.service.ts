import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import {
  GRIEVANCE_REPORT_FILTER_KEYS,
  GRIEVANCE_REPORT_SORT_KEYS,
  GRIEVANCE_SUMMARY_FILTER_KEYS,
  GrievanceHrDashboard,
  GrievanceReportFilter,
  GrievanceReportRow,
  GrievanceReviewDashboard,
  GrievanceSummary,
  GrievanceSummaryFilter,
} from '../../core/models/grievance-insight.models';
import { GrievanceInsightDataService } from '../../core/services/data/grievance-data.services';
import { API_BASE_URL } from '../api-base-url';
import { buildPageParams } from '../kpi/kpi-http.util';
import { failGrievance } from './grievance-http.support';

/**
 * HTTP implementation of the grievance dashboards and reports (`/api/grievances/dashboard/*`,
 * `/reports/*`). Read only; the server's figures are passed through unchanged. The case report
 * sends paging, the search, its filters and a whitelisted sort; the summary sends only its four
 * filters (it has no department filter). A lazy chunk: bound through `lazyDataService`.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceInsightHttpService implements GrievanceInsightDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/grievances`;
  }

  hrDashboard(): Observable<GrievanceHrDashboard> {
    return this.http
      .get<GrievanceHrDashboard>(`${this.url}/dashboard/hr`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  reviewDashboard(): Observable<GrievanceReviewDashboard> {
    return this.http
      .get<GrievanceReviewDashboard>(`${this.url}/dashboard/review`)
      .pipe(catchError((e) => failGrievance(e)));
  }

  casesReport(
    request: PageRequest<GrievanceReportFilter>,
  ): Observable<PagedResult<GrievanceReportRow>> {
    return this.http
      .get<PagedResult<GrievanceReportRow>>(`${this.url}/reports/cases`, {
        params: buildPageParams(request, GRIEVANCE_REPORT_SORT_KEYS, GRIEVANCE_REPORT_FILTER_KEYS),
      })
      .pipe(catchError((e) => failGrievance(e)));
  }

  summary(filters?: Partial<GrievanceSummaryFilter>): Observable<GrievanceSummary> {
    let params = new HttpParams();
    for (const key of GRIEVANCE_SUMMARY_FILTER_KEYS) {
      const value = filters?.[key];
      if (value !== null && value !== undefined && String(value).trim() !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http
      .get<GrievanceSummary>(`${this.url}/reports/summary`, { params })
      .pipe(catchError((e) => failGrievance(e)));
  }
}
