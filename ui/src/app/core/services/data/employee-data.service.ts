import { Observable } from 'rxjs';
import { PagedResult } from '../../models/common.models';
import {
  Employee,
  EmployeeFilter,
  EmployeeListItem,
  EmployeeLookupItem,
} from '../../models/employee.models';
import { CrudDataService } from './crud-data.service';

export abstract class EmployeeDataService extends CrudDataService<
  Employee,
  EmployeeListItem,
  EmployeeFilter
> {
  /** Suggested code for a new employee, e.g. "EMP061". */
  abstract nextEmployeeCode(): Observable<string>;
}

/**
 * Picker source for "choose an employee" fields outside the employee module (for example the
 * hiring manager of a job opening). Current employees only, and only non-personal fields. The API
 * allows it with `recruitment:Edit` or `employees:View`.
 */
export abstract class EmployeeLookupDataService {
  abstract query(request: {
    search?: string;
    pageIndex: number;
    pageSize: number;
  }): Observable<PagedResult<EmployeeLookupItem>>;
}
