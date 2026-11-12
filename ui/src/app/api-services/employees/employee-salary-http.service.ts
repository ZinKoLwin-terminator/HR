import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { EmployeeSalarySummary } from '../../core/models/employee-extras.models';
import { EmployeeSalaryDataService } from '../../core/services/data/employee-extras-data.services';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * HTTP implementation of the salary summary (`GET /api/employees/{id}/salary-summary`, needs
 * `payroll:View`). The server computes the figures; a 404 carries the "no salary assignment yet"
 * message, which is passed through as a NotFound error for the Salary tab to show.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeSalaryHttpService implements EmployeeSalaryDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getSummary(employeeId: number): Observable<EmployeeSalarySummary> {
    return this.http
      .get<EmployeeSalarySummary>(`${this.baseUrl}/employees/${employeeId}/salary-summary`)
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }
}
