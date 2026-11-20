import { Observable } from 'rxjs';
import {
  Branch,
  Company,
  Department,
  JobGrade,
  OrganizationLookup,
  Position,
  Section,
} from '../../models/organization.models';
import { CrudDataService } from './crud-data.service';

export abstract class CompanyDataService extends CrudDataService<Company> {}
export abstract class BranchDataService extends CrudDataService<Branch> {}
export abstract class DepartmentDataService extends CrudDataService<Department> {}
export abstract class SectionDataService extends CrudDataService<Section> {}
export abstract class JobGradeDataService extends CrudDataService<JobGrade> {}
export abstract class PositionDataService extends CrudDataService<Position> {}

/**
 * Names of every organization row (companies, branches, departments, sections, positions and job
 * grades, inactive ones included) for dropdowns and name columns. The API serves it to any
 * signed-in user (`GET /api/organization/lookup`), unlike the full lists (organization:View).
 */
export abstract class OrganizationLookupDataService {
  abstract getLookup(): Observable<OrganizationLookup>;
}
