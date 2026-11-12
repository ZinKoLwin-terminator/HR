import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/models/common.models';
import { EmployeeLookupItem, fullName } from '../../core/models/employee.models';
import { EmployeeLookupDataService } from '../../core/services/data/employee-data.service';
import { paginate } from '../../shared/utilities/query.util';
import { mockResponse } from '../mock-response';
import {
  DepartmentMockService,
  PositionMockService,
} from '../organization/organization-mock.services';
import { EmployeeMockService } from './employee-mock.service';
import { APP_CONFIG } from '../../core/configuration/app-config';

/**
 * Employee picker (demo). Same rules as `GET /api/employees/lookup`: current employees only (no
 * exit date, not Resigned or Terminated), ordered by first name, search matches code or name, and
 * only non-personal fields are returned. The page size is capped at 200 (default 20).
 */
@Injectable({ providedIn: 'root' })
export class EmployeeLookupMockService implements EmployeeLookupDataService {
  private readonly employees = inject(EmployeeMockService);
  private readonly positions = inject(PositionMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;

  query(request: {
    search?: string;
    pageIndex: number;
    pageSize: number;
  }): Observable<PagedResult<EmployeeLookupItem>> {
    return mockResponse(() => {
      const term = (request.search ?? '').trim().toLowerCase().slice(0, 100);
      const rows = this.employees
        .records()
        .filter(
          (e) =>
            !e.exitDate &&
            e.employmentStatus !== 'Resigned' &&
            e.employmentStatus !== 'Terminated' &&
            (!term ||
              e.employeeCode.toLowerCase().includes(term) ||
              fullName(e).toLowerCase().includes(term)),
        )
        .sort((a, b) => a.firstName.localeCompare(b.firstName) || a.id - b.id)
        .map<EmployeeLookupItem>((e) => ({
          id: e.id,
          employeeCode: e.employeeCode,
          displayName: fullName(e),
          jobTitle: this.positions.findById(e.positionId)?.title ?? '',
          departmentName: this.departments.findById(e.departmentId)?.name ?? '',
        }));
      const pageSize = Math.min(Math.max(request.pageSize || 20, 1), 200);
      return paginate(rows, request.pageIndex, pageSize);
    }, this.latency);
  }
}
