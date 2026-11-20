import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { Employee, fullName } from '../../core/models/employee.models';
import {
  BranchRef,
  CompanyRef,
  DepartmentRef,
  JobGradeRef,
  OrganizationLookup,
  PositionRef,
  SectionRef,
} from '../../core/models/organization.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import {
  EmployeeDataService,
  EmployeeLookupDataService,
} from '../../core/services/data/employee-data.service';
import { OrganizationLookupDataService } from '../../core/services/data/organization-data.services';
import { PermissionService } from '../../core/services/permission.service';
import { FieldConfig, FieldOption } from '../../shared/components/crud/crud.models';
import { readAllPages } from '../../shared/utilities/paged-query';

/** An employee as a dropdown entry or a name in a table cell. */
export interface EmployeeRef {
  id: number;
  employeeCode: string;
  displayName: string;
}

export interface OrgLookups {
  companies: CompanyRef[];
  branches: BranchRef[];
  departments: DepartmentRef[];
  sections: SectionRef[];
  grades: JobGradeRef[];
  positions: PositionRef[];
  /** The employees the signed-in user may see (see {@link OrganizationLookupService.people}). */
  employees: EmployeeRef[];
  /**
   * Full employee records (manager, position, status, ...) for screens that build trees or counts.
   * Filled only for roles holding employees:View; empty otherwise.
   */
  employeeRecords: Employee[];
}

/** The employees part of the lookups. */
export interface PeopleLookup {
  employees: EmployeeRef[];
  employeeRecords: Employee[];
}

/** Most rows read from the employee picker (`/api/employees/lookup`). */
const EMPLOYEE_LOOKUP_CAP = 2000;

const EMPTY_ORGANIZATION: OrganizationLookup = {
  companies: [],
  branches: [],
  departments: [],
  sections: [],
  positions: [],
  jobGrades: [],
};

export function toEmployeeRef(e: Employee): EmployeeRef {
  return { id: e.id, employeeCode: e.employeeCode, displayName: fullName(e) };
}

/**
 * Loads the organization lists and the employees a screen needs for dropdowns and names, asking
 * the server only for what the signed-in role may read, so no screen gets a 403 for a lookup:
 *
 * - Organization lists: `GET /api/organization/lookup` (any signed-in user).
 * - Employees: the full list with employees:View; else the employee picker
 *   (`/api/employees/lookup`) with recruitment:Edit; else only the user's own employee, since such
 *   roles only see their own rows anyway.
 *
 * Each part falls back to empty lists on an error, so a failed lookup never breaks a screen.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationLookupService {
  private readonly lookupApi = inject(OrganizationLookupDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly employeeLookupApi = inject(EmployeeLookupDataService);
  private readonly permissions = inject(PermissionService);
  private readonly currentUser = inject(CurrentUserService);

  load(): Observable<OrgLookups> {
    return forkJoin({ org: this.organization(), people: this.people() }).pipe(
      map(({ org, people }) => ({
        companies: org.companies,
        branches: org.branches,
        departments: org.departments,
        sections: org.sections,
        grades: org.jobGrades,
        positions: org.positions,
        employees: people.employees,
        employeeRecords: people.employeeRecords,
      })),
    );
  }

  /** The organization lists only (empty lists when the request fails). */
  organization(): Observable<OrganizationLookup> {
    return this.lookupApi.getLookup().pipe(catchError(() => of(EMPTY_ORGANIZATION)));
  }

  /**
   * The employees the signed-in user may pick: every employee (employees:View), current employees
   * from the picker (recruitment:Edit), or just the user's own employee. Never fails.
   */
  people(): Observable<PeopleLookup> {
    if (this.permissions.can('employees:View')) {
      return this.employeesApi.getAll().pipe(
        map((records) => ({ employees: records.map(toEmployeeRef), employeeRecords: records })),
        catchError(() => of({ employees: [], employeeRecords: [] })),
      );
    }
    if (this.permissions.can('recruitment:Edit')) {
      return readAllPages(
        (r) =>
          this.employeeLookupApi.query({
            search: r.search,
            pageIndex: r.pageIndex,
            pageSize: r.pageSize,
          }),
        {},
        EMPLOYEE_LOOKUP_CAP,
      ).pipe(
        map(({ items }) => ({
          employees: items.map(({ id, employeeCode, displayName }) => ({
            id,
            employeeCode,
            displayName,
          })),
          employeeRecords: [],
        })),
        catchError(() => of({ employees: [], employeeRecords: [] })),
      );
    }
    return of({ employees: this.ownEmployee(), employeeRecords: [] });
  }

  /** `people()` without the full records. */
  employeeRefs(): Observable<EmployeeRef[]> {
    return this.people().pipe(map((p) => p.employees));
  }

  /** The signed-in user's own employee, when the account is linked to one. */
  private ownEmployee(): EmployeeRef[] {
    const user = this.currentUser.current();
    return user.employeeId === null
      ? []
      : [{ id: user.employeeId, employeeCode: '', displayName: user.displayName }];
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

/** The name of an employee given as a reference or a full record. */
export function personName(e: EmployeeRef | Employee): string {
  return 'displayName' in e ? e.displayName : fullName(e);
}

/** "Name (code)", or just the name when the code is unknown (the user's own employee). */
export function employeeLabel(e: EmployeeRef | Employee): string {
  return e.employeeCode ? `${personName(e)} (${e.employeeCode})` : personName(e);
}

export function employeeOptions(employees: readonly (EmployeeRef | Employee)[]): FieldOption[] {
  return options(employees, employeeLabel);
}

/** Shown for a head the role cannot name (no employees:View); never a bare `#id`. */
export const UNNAMED_PERSON = 'Assigned (name not available to your role)';

/**
 * The picker part of a head / manager / supervisor field of an organization record. With
 * employees:View it lists every employee. Without it the role cannot list employees (it would get
 * at most its own employee), so the field is read-only: it keeps the current person, shown by
 * name when known and otherwise as {@link UNNAMED_PERSON}, and saving leaves it unchanged.
 */
export function personField(
  key: string,
  people: readonly (EmployeeRef | Employee)[],
  canPick: boolean,
): Pick<FieldConfig, 'options' | 'force' | 'hint'> {
  if (canPick) {
    const all = employeeOptions(people);
    return { options: () => all };
  }
  const known = new Map(people.map((e) => [e.id, personName(e)]));
  return {
    options: (values) => {
      const id = values[key];
      return typeof id === 'number' ? [{ value: id, label: known.get(id) ?? UNNAMED_PERSON }] : [];
    },
    force: (values) => ({ value: values[key] ?? null }),
    hint: 'Choosing a person needs access to employees (employees:View).',
  };
}

/** id → display text, for table cells. */
export function nameMap<T extends { id: number }>(
  items: readonly T[],
  label: (item: T) => string,
): (id: number | null | undefined) => string {
  const map = new Map(items.map((item) => [item.id, label(item)]));
  return (id) => (id === null || id === undefined ? '—' : (map.get(id) ?? `#${id}`));
}
