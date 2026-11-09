import { Observable } from 'rxjs';
import { Employee, EmployeeFilter, EmployeeListItem } from '../../models/employee.models';
import { CrudDataService } from './crud-data.service';

export abstract class EmployeeDataService extends CrudDataService<
  Employee,
  EmployeeListItem,
  EmployeeFilter
> {
  /** Suggested code for a new employee, e.g. "EMP061". */
  abstract nextEmployeeCode(): Observable<string>;
}
