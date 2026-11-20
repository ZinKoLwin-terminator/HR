import { Entity, RecordStatus } from './common.models';

export interface Company extends Entity {
  code: string;
  name: string;
  legalName: string;
  registrationNumber: string;
  taxId: string;
  industry: string;
  email: string;
  phone: string;
  website: string;
  address: string;
  city: string;
  country: string;
  foundedDate: string;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

export type BranchType = 'Head Office' | 'Branch' | 'Regional Office' | 'Warehouse';

export interface Branch extends Entity {
  companyId: number;
  code: string;
  name: string;
  type: BranchType;
  email: string;
  phone: string;
  address: string;
  city: string;
  country: string;
  managerEmployeeId: number | null;
  openedDate: string;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

export interface Department extends Entity {
  companyId: number;
  branchId: number;
  code: string;
  name: string;
  description: string;
  headEmployeeId: number | null;
  costCenter: string;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

export interface Section extends Entity {
  departmentId: number;
  code: string;
  name: string;
  description: string;
  supervisorEmployeeId: number | null;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

export interface JobGrade extends Entity {
  code: string;
  name: string;
  /** 1 = most junior. */
  level: number;
  /** Sample monthly salary band. */
  minSalary: number;
  maxSalary: number;
  description: string;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

export interface Position extends Entity {
  departmentId: number;
  sectionId: number | null;
  jobGradeId: number;
  code: string;
  title: string;
  description: string;
  /** Approved headcount for this position. */
  headcount: number;
  reportsToPositionId: number | null;
  status: RecordStatus;
  /** Concurrency token (base64) issued by the API. The mock leaves it undefined. */
  rowVersion?: string;
}

// Organization lookup (`GET /api/organization/lookup`): the id, code and name of every organization
// row (inactive ones included), for dropdowns and name columns. Any signed-in user may read it, so
// screens outside the organization module build their lists from it, not from the full lists
// (which need organization:View). Fields such as salary bands or heads are not in it.

export interface CompanyRef extends Entity {
  code: string;
  name: string;
  status: RecordStatus;
}

export interface BranchRef extends Entity {
  companyId: number;
  code: string;
  name: string;
  status: RecordStatus;
}

export interface DepartmentRef extends Entity {
  companyId: number;
  branchId: number;
  code: string;
  name: string;
  status: RecordStatus;
}

export interface SectionRef extends Entity {
  departmentId: number;
  code: string;
  name: string;
  status: RecordStatus;
}

export interface PositionRef extends Entity {
  departmentId: number;
  sectionId: number | null;
  jobGradeId: number;
  code: string;
  title: string;
  status: RecordStatus;
}

export interface JobGradeRef extends Entity {
  code: string;
  name: string;
  /** 1 = most junior. */
  level: number;
  status: RecordStatus;
}

export interface OrganizationLookup {
  companies: CompanyRef[];
  branches: BranchRef[];
  departments: DepartmentRef[];
  sections: SectionRef[];
  positions: PositionRef[];
  jobGrades: JobGradeRef[];
}
