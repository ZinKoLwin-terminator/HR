import {
  Branch,
  Company,
  Department,
  JobGrade,
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
