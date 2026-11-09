import { Entity } from './common.models';

export type Gender = 'Male' | 'Female' | 'Other';
export type MaritalStatus = 'Single' | 'Married' | 'Divorced' | 'Widowed';
export type EmploymentType = 'Full-time' | 'Part-time' | 'Contract' | 'Intern';
export type EmploymentStatus =
  'Active' | 'Probation' | 'On Leave' | 'Suspended' | 'Resigned' | 'Terminated';

export const EMPLOYMENT_TYPES: readonly EmploymentType[] = [
  'Full-time',
  'Part-time',
  'Contract',
  'Intern',
];
export const EMPLOYMENT_STATUSES: readonly EmploymentStatus[] = [
  'Active',
  'Probation',
  'On Leave',
  'Suspended',
  'Resigned',
  'Terminated',
];
/** Statuses that count towards current headcount. */
export const CURRENT_EMPLOYMENT_STATUSES: readonly EmploymentStatus[] = [
  'Active',
  'Probation',
  'On Leave',
  'Suspended',
];

export interface PostalAddress {
  line1: string;
  line2: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

export interface EmergencyContact {
  name: string;
  relationship: string;
  phone: string;
}

export interface BankDetails {
  bankName: string;
  branchName: string;
  accountName: string;
  accountNumber: string;
}

export interface Employee extends Entity {
  employeeCode: string;
  firstName: string;
  lastName: string;
  gender: Gender;
  dateOfBirth: string;
  maritalStatus: MaritalStatus;
  nationality: string;
  nationalId: string;

  workEmail: string;
  personalEmail: string;
  phone: string;
  alternatePhone: string;

  address: PostalAddress;
  emergencyContact: EmergencyContact;

  companyId: number;
  branchId: number;
  departmentId: number;
  sectionId: number | null;
  positionId: number;
  jobGradeId: number;
  managerId: number | null;
  shiftId: number | null;

  employmentType: EmploymentType;
  employmentStatus: EmploymentStatus;
  joiningDate: string;
  probationEndDate: string | null;
  exitDate: string | null;

  bank: BankDetails;
  /** Data URL or relative asset path. Null renders an initials avatar. */
  photoUrl: string | null;
}

/** Flattened row returned by list queries (mirrors a typical API list DTO). */
export interface EmployeeListItem extends Entity {
  employeeCode: string;
  fullName: string;
  firstName: string;
  lastName: string;
  workEmail: string;
  photoUrl: string | null;
  gender: Gender;
  companyId: number;
  branchId: number;
  branchName: string;
  departmentId: number;
  departmentName: string;
  positionId: number;
  positionTitle: string;
  employmentType: EmploymentType;
  employmentStatus: EmploymentStatus;
  joiningDate: string;
  shiftId: number | null;
  shiftName: string;
}

export interface EmployeeFilter {
  departmentId: number | null;
  branchId: number | null;
  shiftId: number | null;
  employmentStatus: EmploymentStatus | null;
  employmentType: EmploymentType | null;
  joiningFrom: string | null;
  joiningTo: string | null;
}

export function fullName(person: { firstName: string; lastName: string }): string {
  return `${person.firstName} ${person.lastName}`.trim();
}
