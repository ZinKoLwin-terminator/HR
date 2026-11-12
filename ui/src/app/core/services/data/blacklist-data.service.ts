import { Observable } from 'rxjs';
import { BlacklistEntry, BlacklistFilter } from '../../models/blacklist.models';
import { PagedResult, PageRequest } from '../../models/common.models';

/**
 * The do-not-rehire list of former employees. Reads need `employees:ViewBlacklist`, changes need
 * `employees:EditBlacklist` (UX only here; the server enforces them). The reason is sensitive:
 * only the screens that hold ViewBlacklist show it. There is no unpaged list and no delete: a
 * lifted entry stays as history.
 */
export abstract class BlacklistDataService {
  abstract query(request: PageRequest<BlacklistFilter>): Observable<PagedResult<BlacklistEntry>>;
  abstract getById(id: number): Observable<BlacklistEntry>;
  /** The entry in force for an employee, or `null` when the employee is not listed. */
  abstract getActiveForEmployee(employeeId: number): Observable<BlacklistEntry | null>;
  /**
   * Lists a former employee. Conflict when the employee has no exit date or is already listed;
   * validation error when the reason is blank or longer than 500 characters.
   */
  abstract add(employeeId: number, reason: string): Observable<BlacklistEntry>;
  /** Lifts an entry with a reason (required, at most 500 characters). `rowVersion` is optional. */
  abstract lift(id: number, reason: string, rowVersion?: string): Observable<BlacklistEntry>;
}
