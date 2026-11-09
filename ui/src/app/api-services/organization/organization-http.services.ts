import { Injectable } from '@angular/core';
import {
  Branch,
  Company,
  Department,
  JobGrade,
  Position,
  Section,
} from '../../core/models/organization.models';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../../core/services/data/organization-data.services';
import { HttpCrudService } from '../http-crud.service';

// HTTP implementations of the Organization contracts. Filter keys match the API's query filters.

@Injectable({ providedIn: 'root' })
export class CompanyHttpService extends HttpCrudService<Company> implements CompanyDataService {
  protected readonly resource = 'companies';
  protected readonly filterKeys = ['status'] as const;
}

@Injectable({ providedIn: 'root' })
export class BranchHttpService extends HttpCrudService<Branch> implements BranchDataService {
  protected readonly resource = 'branches';
  protected readonly filterKeys = ['companyId', 'type', 'status'] as const;
}

@Injectable({ providedIn: 'root' })
export class DepartmentHttpService
  extends HttpCrudService<Department>
  implements DepartmentDataService
{
  protected readonly resource = 'departments';
  protected readonly filterKeys = ['companyId', 'branchId', 'status'] as const;
}

@Injectable({ providedIn: 'root' })
export class SectionHttpService extends HttpCrudService<Section> implements SectionDataService {
  protected readonly resource = 'sections';
  protected readonly filterKeys = ['departmentId', 'status'] as const;
}

@Injectable({ providedIn: 'root' })
export class JobGradeHttpService extends HttpCrudService<JobGrade> implements JobGradeDataService {
  protected readonly resource = 'job-grades';
  protected readonly filterKeys = ['status'] as const;
}

@Injectable({ providedIn: 'root' })
export class PositionHttpService extends HttpCrudService<Position> implements PositionDataService {
  protected readonly resource = 'positions';
  protected readonly filterKeys = ['departmentId', 'jobGradeId', 'status'] as const;
}
