import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { OrganizationLookup } from '../../core/models/organization.models';
import { OrganizationLookupDataService } from '../../core/services/data/organization-data.services';
import { mockResponse } from '../mock-response';
import {
  BranchMockService,
  CompanyMockService,
  DepartmentMockService,
  JobGradeMockService,
  PositionMockService,
  SectionMockService,
} from './organization-mock.services';

/** Sorted by name, then id (the API's order). */
function byName<T extends { id: number; name: string }>(rows: readonly T[]): T[] {
  return [...rows].sort((a, b) => a.name.localeCompare(b.name) || a.id - b.id);
}

/**
 * Organization lookup (demo). Same shape as `GET /api/organization/lookup`: every row, inactive ones
 * included, with only the id, code, name, parent ids and status (no salary bands, heads or
 * contact details). Each list is ordered by name (positions by title), then id, like the API.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationLookupMockService implements OrganizationLookupDataService {
  private readonly companies = inject(CompanyMockService);
  private readonly branches = inject(BranchMockService);
  private readonly departments = inject(DepartmentMockService);
  private readonly sections = inject(SectionMockService);
  private readonly positions = inject(PositionMockService);
  private readonly grades = inject(JobGradeMockService);
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;

  getLookup(): Observable<OrganizationLookup> {
    return mockResponse(
      () => ({
        companies: byName(this.companies.records()).map(({ id, code, name, status }) => ({
          id,
          code,
          name,
          status,
        })),
        branches: byName(this.branches.records()).map(({ id, companyId, code, name, status }) => ({
          id,
          companyId,
          code,
          name,
          status,
        })),
        departments: byName(this.departments.records()).map(
          ({ id, companyId, branchId, code, name, status }) => ({
            id,
            companyId,
            branchId,
            code,
            name,
            status,
          }),
        ),
        sections: byName(this.sections.records()).map(
          ({ id, departmentId, code, name, status }) => ({
            id,
            departmentId,
            code,
            name,
            status,
          }),
        ),
        positions: [...this.positions.records()]
          .sort((a, b) => a.title.localeCompare(b.title) || a.id - b.id)
          .map(({ id, departmentId, sectionId, jobGradeId, code, title, status }) => ({
            id,
            departmentId,
            sectionId,
            jobGradeId,
            code,
            title,
            status,
          })),
        jobGrades: byName(this.grades.records()).map(({ id, code, name, level, status }) => ({
          id,
          code,
          name,
          level,
          status,
        })),
      }),
      this.latency,
    );
  }
}
