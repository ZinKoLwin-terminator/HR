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
