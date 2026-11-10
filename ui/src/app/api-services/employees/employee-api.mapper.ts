import { AppError } from '../../core/models/common.models';
import {
  Employee,
  EmployeeListItem,
  EmploymentStatus,
  EmploymentType,
  Gender,
  MaritalStatus,
} from '../../core/models/employee.models';

// Wire shapes of /api/employees (see api/src/Apex.Hrms.Application/Employees/EmployeeModels.cs) and the
// pure functions turning them into the UI models. No HTTP here.

/** Shown instead of a value the signed-in user may not read (the API answered 403). */
export const NOT_PERMITTED = 'Not permitted';

export interface EmployeeListItemApi {
  id: number;
  employeeCode: string;
  fullName: string;
  firstName: string;
  lastName: string;
  workEmail: string;
  photoUrl?: string | null;
  gender: Gender;
  companyId: number;
  branchId: number;
  departmentId: number;
  positionId: number;
  employmentType: EmploymentType;
  employmentStatus: EmploymentStatus;
  joiningDate: string;
  shiftId?: number | null;
  rowVersion: string;
}

/** The employment record without personal data. */
export interface EmployeeApi extends EmployeeListItemApi {
  sectionId?: number | null;
  jobGradeId: number;
  managerId?: number | null;
  probationEndDate?: string | null;
  exitDate?: string | null;
  bankAccountMasked?: string | null;
}

export type PersonalValues = Pick<
  Employee,
  | 'dateOfBirth'
  | 'maritalStatus'
  | 'nationality'
  | 'nationalId'
  | 'personalEmail'
  | 'phone'
  | 'alternatePhone'
  | 'address'
  | 'emergencyContact'
>;

export interface EmployeePersonalApi extends PersonalValues {
  rowVersion: string;
}

export interface BankAccountApi {
  employeeId: number;
  bankName: string;
  branchName?: string | null;
  accountName: string;
  accountNumber: string;
  rowVersion: string;
}

export interface BankValues {
  bankName: string;
  branchName: string;
  accountName: string;
  /** Masked ("******7890") except while the user types a new number on the edit form. */
  accountNumber: string;
}

export interface HistoryEntryApi {
  id: number;
  employeeId: number;
  date: string;
  type: string;
  title: string;
  details?: string | null;
  recordedBy?: string | null;
}

/** Employment fields the PUT /{id} body carries (no status, no personal, no bank). */
export const EMPLOYMENT_KEYS = [
  'firstName',
  'lastName',
  'gender',
  'workEmail',
  'photoUrl',
  'companyId',
  'branchId',
  'departmentId',
  'sectionId',
  'positionId',
  'jobGradeId',
  'managerId',
  'shiftId',
  'employmentType',
  'joiningDate',
  'probationEndDate',
] as const satisfies readonly (keyof Employee)[];

export const PERSONAL_KEYS = [
  'dateOfBirth',
  'maritalStatus',
  'nationality',
  'nationalId',
  'personalEmail',
  'phone',
  'alternatePhone',
  'address',
  'emergencyContact',
] as const satisfies readonly (keyof Employee)[];

export const emptyPersonal = (): PersonalValues => ({
  dateOfBirth: '',
  maritalStatus: 'Single',
  nationality: '',
  nationalId: '',
  personalEmail: '',
  phone: '',
  alternatePhone: '',
  address: { line1: '', line2: '', city: '', state: '', postalCode: '', country: '' },
  emergencyContact: { name: '', relationship: '', phone: '' },
});

/** Placeholder personal data for a user without ViewSensitive: the text says so, nothing real. */
export const forbiddenPersonal = (): PersonalValues => ({
  ...emptyPersonal(),
  nationality: NOT_PERMITTED,
  nationalId: NOT_PERMITTED,
  phone: NOT_PERMITTED,
  address: {
    line1: NOT_PERMITTED,
    line2: '',
    city: NOT_PERMITTED,
    state: '',
    postalCode: '',
    country: NOT_PERMITTED,
  },
  emergencyContact: {
    name: NOT_PERMITTED,
    relationship: NOT_PERMITTED,
    phone: NOT_PERMITTED,
  },
});

export function maskAccount(accountNumber: string): string {
  return accountNumber ? `******${accountNumber.slice(-4)}` : '';
}

export function isMasked(accountNumber: string): boolean {
  return accountNumber.includes('*');
}

/** List row from the list DTO. Copies named fields only, so nothing else can leak into a table row. */
export function toListItem(
  dto: EmployeeListItemApi,
  names: { branch: string; department: string; position: string },
): EmployeeListItem {
  return {
    id: dto.id,
    employeeCode: dto.employeeCode,
    fullName: dto.fullName,
    firstName: dto.firstName,
    lastName: dto.lastName,
    workEmail: dto.workEmail,
    photoUrl: dto.photoUrl || null,
    gender: dto.gender,
    companyId: dto.companyId,
    branchId: dto.branchId,
    branchName: names.branch,
    departmentId: dto.departmentId,
    departmentName: names.department,
    positionId: dto.positionId,
    positionTitle: names.position,
    employmentType: dto.employmentType,
    employmentStatus: dto.employmentStatus,
    joiningDate: dto.joiningDate,
    shiftId: dto.shiftId ?? null,
    // No HTTP shift adapter yet: show the id, never a mock name for a real id.
    shiftName: dto.shiftId ? `Shift #${dto.shiftId}` : '—',
  };
}

/** Normalises the optional members of the employment DTO. */
export function normalizeEmployee(dto: EmployeeApi): EmployeeApi {
  return {
    ...dto,
    photoUrl: dto.photoUrl || null,
    sectionId: dto.sectionId ?? null,
    managerId: dto.managerId ?? null,
    shiftId: dto.shiftId ?? null,
    probationEndDate: dto.probationEndDate || null,
    exitDate: dto.exitDate || null,
  };
}

/** The single mock-style `Employee` from the pieces the API serves separately. */
export function assembleEmployee(
  dto: EmployeeApi,
  personal: PersonalValues,
  bank: BankValues,
): Employee {
  return {
    id: dto.id,
    employeeCode: dto.employeeCode,
    firstName: dto.firstName,
    lastName: dto.lastName,
    gender: dto.gender,
    dateOfBirth: personal.dateOfBirth,
    maritalStatus: personal.maritalStatus as MaritalStatus,
    nationality: personal.nationality,
    nationalId: personal.nationalId,
    workEmail: dto.workEmail,
    personalEmail: personal.personalEmail,
    phone: personal.phone,
    alternatePhone: personal.alternatePhone,
    address: { ...personal.address },
    emergencyContact: { ...personal.emergencyContact },
    companyId: dto.companyId,
    branchId: dto.branchId,
    departmentId: dto.departmentId,
    sectionId: dto.sectionId ?? null,
    positionId: dto.positionId,
    jobGradeId: dto.jobGradeId,
    managerId: dto.managerId ?? null,
    shiftId: dto.shiftId ?? null,
    employmentType: dto.employmentType,
    employmentStatus: dto.employmentStatus,
    joiningDate: dto.joiningDate,
    probationEndDate: dto.probationEndDate ?? null,
    exitDate: dto.exitDate ?? null,
    bank: { ...bank },
    photoUrl: dto.photoUrl ?? null,
  };
}

/** Personal values from the API (optional members arrive as "" or missing). */
export function toPersonalValues(api: EmployeePersonalApi): PersonalValues {
  return {
    dateOfBirth: api.dateOfBirth,
    maritalStatus: api.maritalStatus,
    nationality: api.nationality ?? '',
    nationalId: api.nationalId ?? '',
    personalEmail: api.personalEmail ?? '',
    phone: api.phone ?? '',
    alternatePhone: api.alternatePhone ?? '',
    address: {
      line1: api.address?.line1 ?? '',
      line2: api.address?.line2 ?? '',
      city: api.address?.city ?? '',
      state: api.address?.state ?? '',
      postalCode: api.address?.postalCode ?? '',
      country: api.address?.country ?? '',
    },
    emergencyContact: {
      name: api.emergencyContact?.name ?? '',
      relationship: api.emergencyContact?.relationship ?? '',
      phone: api.emergencyContact?.phone ?? '',
    },
  };
}

// ---- Error field names: the API reports paths such as "address.city"; the form uses flat keys ----

export const PERSONAL_FIELD_MAP: Record<string, string> = {
  'address.line1': 'addressLine1',
  'address.line2': 'addressLine2',
  'address.city': 'city',
  'address.state': 'state',
  'address.postalCode': 'postalCode',
  'address.country': 'country',
  'emergencyContact.name': 'emergencyName',
  'emergencyContact.relationship': 'emergencyRelationship',
  'emergencyContact.phone': 'emergencyPhone',
};

export const BANK_FIELD_MAP: Record<string, string> = {
  bankName: 'bankName',
  branchName: 'bankBranchName',
  accountName: 'bankAccountName',
  accountNumber: 'bankAccountNumber',
};

export const STATUS_FIELD_MAP: Record<string, string> = { status: 'employmentStatus' };

/** Renames field-error keys and prefixes the message with the part of the record that failed. */
export function describeFailure(
  error: AppError,
  label: string,
  fieldMap: Record<string, string> = {},
): AppError {
  const result: AppError = { ...error, message: `${label} not saved. ${error.message}` };
  if (error.fieldErrors) {
    result.fieldErrors = Object.fromEntries(
      Object.entries(error.fieldErrors).map(([key, message]) => [fieldMap[key] ?? key, message]),
    );
  }
  return result;
}

const blank = (value: unknown): unknown => (value === '' || value === undefined ? null : value);

/** Deep equality that treats "", null and undefined as the same "no value". */
export function sameValue(a: unknown, b: unknown): boolean {
  const x = blank(a);
  const y = blank(b);
  if (typeof x === 'object' && x !== null && typeof y === 'object' && y !== null) {
    const keys = new Set([...Object.keys(x), ...Object.keys(y)]);
    return [...keys].every((k) =>
      sameValue((x as Record<string, unknown>)[k], (y as Record<string, unknown>)[k]),
    );
  }
  return x === y;
}
