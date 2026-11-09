import { Observable } from 'rxjs';
import {
  EmployeeDocument,
  EmployeeSalarySummary,
  EmploymentHistoryEntry,
} from '../../models/employee-extras.models';
import { CrudDataService } from './crud-data.service';

export abstract class EmployeeDocumentDataService extends CrudDataService<EmployeeDocument> {}

export abstract class EmploymentHistoryDataService extends CrudDataService<EmploymentHistoryEntry> {}

export abstract class EmployeeSalaryDataService {
  abstract getSummary(employeeId: number): Observable<EmployeeSalarySummary>;
}
