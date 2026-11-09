import { CreateModel } from '../../../core/models/common.models';
import {
  Employee,
  EmploymentStatus,
  EmploymentType,
  Gender,
  MaritalStatus,
} from '../../../core/models/employee.models';
import { FormValues } from '../../../shared/components/crud/crud.models';
import { addMonths, todayIso } from '../../../shared/utilities/date.util';

/** Employee → flat form values (the form uses flat keys; the model nests address/bank/etc.). */
export function toFormValues(employee: Employee | null, suggestedCode: string | null): FormValues {
  if (!employee) {
    const today = todayIso();
    return {
      employeeCode: suggestedCode ?? '',
      nationality: 'Myanmar',
      country: 'Myanmar',
      employmentType: 'Full-time',
      employmentStatus: 'Probation',
      joiningDate: today,
      probationEndDate: addMonths(today, 3),
    };
  }
  return {
    firstName: employee.firstName,
    lastName: employee.lastName,
    gender: employee.gender,
    dateOfBirth: employee.dateOfBirth,
    maritalStatus: employee.maritalStatus,
    nationality: employee.nationality,
    nationalId: employee.nationalId,
    workEmail: employee.workEmail,
    personalEmail: employee.personalEmail,
    phone: employee.phone,
    alternatePhone: employee.alternatePhone,
    addressLine1: employee.address.line1,
    addressLine2: employee.address.line2,
    city: employee.address.city,
    state: employee.address.state,
    postalCode: employee.address.postalCode,
    country: employee.address.country,
    emergencyName: employee.emergencyContact.name,
    emergencyRelationship: employee.emergencyContact.relationship,
    emergencyPhone: employee.emergencyContact.phone,
    employeeCode: employee.employeeCode,
    employmentType: employee.employmentType,
    employmentStatus: employee.employmentStatus,
    joiningDate: employee.joiningDate,
    probationEndDate: employee.probationEndDate ?? '',
    exitDate: employee.exitDate ?? '',
    companyId: employee.companyId,
    branchId: employee.branchId,
    departmentId: employee.departmentId,
    sectionId: employee.sectionId,
    positionId: employee.positionId,
    jobGradeId: employee.jobGradeId,
    managerId: employee.managerId,
    shiftId: employee.shiftId,
    bankName: employee.bank.bankName,
    bankBranchName: employee.bank.branchName,
    bankAccountName: employee.bank.accountName,
    bankAccountNumber: employee.bank.accountNumber,
  };
}

const text = (value: unknown): string => String(value ?? '').trim();
const optionalText = (value: unknown): string | null => text(value) || null;
const id = (value: unknown): number => Number(value);
const optionalId = (value: unknown): number | null =>
  value === null || value === undefined || value === '' ? null : Number(value);

/** Flat form values → employee model. */
export function toEmployeeModel(v: FormValues, photoUrl: string | null): CreateModel<Employee> {
  return {
    employeeCode: text(v['employeeCode']),
    firstName: text(v['firstName']),
    lastName: text(v['lastName']),
    gender: v['gender'] as Gender,
    dateOfBirth: text(v['dateOfBirth']),
    maritalStatus: v['maritalStatus'] as MaritalStatus,
    nationality: text(v['nationality']),
    nationalId: text(v['nationalId']),
    workEmail: text(v['workEmail']),
    personalEmail: text(v['personalEmail']),
    phone: text(v['phone']),
    alternatePhone: text(v['alternatePhone']),
    address: {
      line1: text(v['addressLine1']),
      line2: text(v['addressLine2']),
      city: text(v['city']),
      state: text(v['state']),
      postalCode: text(v['postalCode']),
      country: text(v['country']),
    },
    emergencyContact: {
      name: text(v['emergencyName']),
      relationship: text(v['emergencyRelationship']),
      phone: text(v['emergencyPhone']),
    },
    companyId: id(v['companyId']),
    branchId: id(v['branchId']),
    departmentId: id(v['departmentId']),
    sectionId: optionalId(v['sectionId']),
    positionId: id(v['positionId']),
    jobGradeId: id(v['jobGradeId']),
    managerId: optionalId(v['managerId']),
    shiftId: optionalId(v['shiftId']),
    employmentType: v['employmentType'] as EmploymentType,
    employmentStatus: v['employmentStatus'] as EmploymentStatus,
    joiningDate: text(v['joiningDate']),
    probationEndDate: optionalText(v['probationEndDate']),
    exitDate: optionalText(v['exitDate']),
    bank: {
      bankName: text(v['bankName']),
      branchName: text(v['bankBranchName']),
      accountName: text(v['bankAccountName']),
      accountNumber: text(v['bankAccountNumber']),
    },
    photoUrl,
  };
}
