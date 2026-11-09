import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { Employee, fullName } from '../../core/models/employee.models';
import {
  Branch,
  Company,
  Department,
  JobGrade,
  Position,
  Section,
} from '../../core/models/organization.models';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../../core/services/data/organization-data.services';
import { FieldOption } from '../../shared/components/crud/crud.models';

export interface OrgLookups {
  companies: Company[];
  branches: Branch[];
  departments: Department[];
  sections: Section[];
  grades: JobGrade[];
  positions: Position[];
  employees: Employee[];
}

/** Loads every organization list once so screens can build dropdowns and show names. */
@Injectable({ providedIn: 'root' })
export class OrganizationLookupService {
  private readonly companies = inject(CompanyDataService);
  private readonly branches = inject(BranchDataService);
  private readonly departments = inject(DepartmentDataService);
  private readonly sections = inject(SectionDataService);
  private readonly grades = inject(JobGradeDataService);
  private readonly positions = inject(PositionDataService);
  private readonly employees = inject(EmployeeDataService);

  load(): Observable<OrgLookups> {
    return forkJoin({
      companies: this.companies.getAll(),
      branches: this.branches.getAll(),
      departments: this.departments.getAll(),
      sections: this.sections.getAll(),
      grades: this.grades.getAll(),
      positions: this.positions.getAll(),
      employees: this.employees.getAll(),
    });
  }
}

export const STATUS_OPTIONS: FieldOption[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Inactive', label: 'Inactive' },
];

/** Dropdown options sorted by label. */
export function options<T extends { id: number }>(
  items: readonly T[],
  label: (item: T) => string,
): FieldOption[] {
  return items
    .map((item) => ({ value: item.id, label: label(item) }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

export function employeeOptions(employees: readonly Employee[]): FieldOption[] {
  return options(employees, (e) => `${fullName(e)} (${e.employeeCode})`);
}

/** id → display text, for table cells. */
export function nameMap<T extends { id: number }>(
  items: readonly T[],
  label: (item: T) => string,
): (id: number | null | undefined) => string {
  const map = new Map(items.map((item) => [item.id, label(item)]));
  return (id) => (id === null || id === undefined ? '—' : (map.get(id) ?? `#${id}`));
}
