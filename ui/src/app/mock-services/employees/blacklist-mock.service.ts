import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BLACKLIST_REASON_MAX_LENGTH,
  BlacklistEntry,
  BlacklistFilter,
} from '../../core/models/blacklist.models';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { Candidate } from '../../core/models/recruitment.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { BlacklistDataService } from '../../core/services/data/blacklist-data.service';
import { MOCK_BLACKLIST } from '../../mock-data/employees/blacklist.mock';
import { todayIso } from '../../shared/utilities/date.util';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { EmployeeMockService } from './employee-mock.service';

const digitsOf = (value: string | null | undefined): string => (value ?? '').replace(/\D/g, '');
const lower = (value: string | null | undefined): string => (value ?? '').trim().toLowerCase();

/**
 * Do-not-rehire list (demo). Same rules as the API:
 *  - only a former employee (one with an exit date) can be listed, one entry in force per employee;
 *  - a reason is required (at most 500 characters), also when lifting; a lifted entry stays as history;
 *  - a candidate matches when their email, phone (digits only) or national id equals one of an
 *    employee with an entry in force. Nothing else is revealed to recruitment.
 * The demo does not enforce `employees:ViewBlacklist` / `EditBlacklist`: it has no real roles.
 */
@Injectable({ providedIn: 'root' })
export class BlacklistMockService
  extends InMemoryCrudService<BlacklistEntry, BlacklistEntry, BlacklistFilter>
  implements BlacklistDataService
{
  private readonly employees = inject(EmployeeMockService);
  private readonly user = inject(CurrentUserService);
  protected readonly entityLabel = 'Do-not-rehire entry';
  protected readonly activityCategory = 'employee';
  protected override readonly searchFields = ['employeeCode', 'employeeName'] as const;

  constructor() {
    super(MOCK_BLACKLIST, 'employee_blacklist');
  }

  protected override matchesFilters(
    item: BlacklistEntry,
    filters: Partial<BlacklistFilter>,
  ): boolean {
    const active = String(filters.active ?? '');
    if (active === 'true') return item.liftedOn === null;
    if (active === 'false') return item.liftedOn !== null;
    return true;
  }

  /** Newest listing first unless another sort is asked for. */
  override query(request: PageRequest<BlacklistFilter>): Observable<PagedResult<BlacklistEntry>> {
    const sort = request.sort?.direction
      ? request.sort
      : { active: 'blacklistedOn', direction: 'desc' as const };
    return super.query({ ...request, sort });
  }

  getActiveForEmployee(employeeId: number): Observable<BlacklistEntry | null> {
    return this.respond(() => this.activeFor(employeeId) ?? null);
  }

  add(employeeId: number, reason: string): Observable<BlacklistEntry> {
    return this.respond(() => {
      const text = this.checkedReason(reason);
      const employee = this.employees.findById(Number(employeeId));
      if (!employee) {
        this.validationError('Choose an employee.', { employeeId: 'Choose an employee.' });
      }
      if (!employee.exitDate) {
        this.conflictError('Only a former employee can be put on the do-not-rehire list.');
      }
      if (this.activeFor(employee.id)) {
        this.conflictError('This employee is already on the do-not-rehire list.');
      }
      const entry: BlacklistEntry = {
        id: this.nextId(),
        employeeId: employee.id,
        employeeCode: employee.employeeCode,
        employeeName: `${employee.firstName} ${employee.lastName}`.trim(),
        reason: text,
        blacklistedOn: todayIso(),
        blacklistedBy: this.user.current().displayName,
        liftedOn: null,
        liftedBy: null,
        liftReason: null,
      };
      this.store.update((items) => [...items, entry]);
      // The activity feed never carries the reason.
      this.activity.record(
        'employee',
        `Employee ${employee.employeeCode} added to the do-not-rehire list`,
      );
      return entry;
    });
  }

  lift(id: number, reason: string): Observable<BlacklistEntry> {
    return this.respond(() => {
      const entry = this.require(id);
      const text = this.checkedReason(reason);
      if (entry.liftedOn !== null) {
        this.conflictError('This do-not-rehire entry has already been lifted.');
      }
      const updated: BlacklistEntry = {
        ...entry,
        liftedOn: todayIso(),
        liftedBy: this.user.current().displayName,
        liftReason: text,
      };
      this.store.update((items) => items.map((i) => (i.id === id ? updated : i)));
      this.activity.record(
        'employee',
        `Employee ${entry.employeeCode} removed from the do-not-rehire list`,
      );
      return updated;
    });
  }

  // ---- Mock-layer helpers (used by other mock services only) ---------------

  /** The entry in force for an employee. */
  activeFor(employeeId: number): BlacklistEntry | undefined {
    return this.store().find((e) => e.employeeId === employeeId && e.liftedOn === null);
  }

  /** Number of entries, in force or lifted, that refer to the employee (they block deleting them). */
  historyCount(employeeId: number): number {
    return this.store().filter((e) => e.employeeId === employeeId).length;
  }

  /** True when the candidate matches an employee with an entry in force. */
  matches(candidate: Pick<Candidate, 'email' | 'phone' | 'nationalId'>): boolean {
    const email = lower(candidate.email);
    const phone = digitsOf(candidate.phone);
    const nationalId = lower(candidate.nationalId);
    return this.store()
      .filter((entry) => entry.liftedOn === null)
      .some((entry) => {
        const employee = this.employees.findById(entry.employeeId);
        if (!employee) return false;
        if (email && [employee.workEmail, employee.personalEmail].some((m) => lower(m) === email)) {
          return true;
        }
        if (phone && [employee.phone, employee.alternatePhone].some((p) => digitsOf(p) === phone)) {
          return true;
        }
        return !!nationalId && lower(employee.nationalId) === nationalId;
      });
  }

  private checkedReason(reason: string): string {
    const text = (reason ?? '').trim();
    if (!text) {
      this.validationError('Enter a reason.', { reason: 'A reason is required.' });
    }
    if (text.length > BLACKLIST_REASON_MAX_LENGTH) {
      this.validationError(
        `The reason can have at most ${BLACKLIST_REASON_MAX_LENGTH} characters.`,
        { reason: `At most ${BLACKLIST_REASON_MAX_LENGTH} characters.` },
      );
    }
    return text;
  }
}
