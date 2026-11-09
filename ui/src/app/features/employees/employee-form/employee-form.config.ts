import { Shift } from '../../../core/models/attendance.models';
import {
  CURRENT_EMPLOYMENT_STATUSES,
  EMPLOYMENT_STATUSES,
  EMPLOYMENT_TYPES,
} from '../../../core/models/employee.models';
import { FieldConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import { OrgLookups, employeeOptions, options } from '../../organization/organization-lookups';

export interface FormSection {
  id: string;
  title: string;
  description: string;
  icon: string;
  fields: FieldConfig[];
}

export interface EmployeeFormLookups {
  org: OrgLookups;
  shifts: Shift[];
}

const toOptions = (values: readonly string[]): FieldOption[] =>
  values.map((v) => ({ value: v, label: v }));

const GENDERS = toOptions(['Male', 'Female', 'Other']);
const MARITAL = toOptions(['Single', 'Married', 'Divorced', 'Widowed']);
const RELATIONSHIPS = toOptions([
  'Spouse',
  'Father',
  'Mother',
  'Sibling',
  'Child',
  'Friend',
  'Other',
]);

export function buildEmployeeSections(l: EmployeeFormLookups): FormSection[] {
  const { org } = l;
  const people = org.employees.filter((e) =>
    CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus),
  );

  return [
    {
      id: 'personal',
      title: 'Personal information',
      description: 'Legal name and identity details.',
      icon: 'person',
      fields: [
        { key: 'firstName', label: 'First name', type: 'text', required: true, maxLength: 60 },
        { key: 'lastName', label: 'Last name', type: 'text', required: true, maxLength: 60 },
        { key: 'gender', label: 'Gender', type: 'select', required: true, options: () => GENDERS },
        {
          key: 'dateOfBirth',
          label: 'Date of birth',
          type: 'date',
          required: true,
          notInFuture: true,
          minAge: 18,
          hint: 'Employees must be at least 18',
        },
        {
          key: 'maritalStatus',
          label: 'Marital status',
          type: 'select',
          required: true,
          options: () => MARITAL,
        },
        { key: 'nationality', label: 'Nationality', type: 'text', required: true, maxLength: 60 },
        {
          key: 'nationalId',
          label: 'National ID / passport no.',
          type: 'text',
          required: true,
          maxLength: 40,
          fullWidth: true,
        },
      ],
    },
    {
      id: 'contact',
      title: 'Contact information',
      description: 'How to reach the employee.',
      icon: 'contact_mail',
      fields: [
        { key: 'workEmail', label: 'Work email', type: 'email', required: true, maxLength: 120 },
        { key: 'personalEmail', label: 'Personal email', type: 'email', maxLength: 120 },
        {
          key: 'phone',
          label: 'Mobile phone',
          type: 'tel',
          required: true,
          hint: 'e.g. +95 9 123 456 789',
        },
        { key: 'alternatePhone', label: 'Alternate phone', type: 'tel' },
      ],
    },
    {
      id: 'address',
      title: 'Address information',
      description: 'Current residential address.',
      icon: 'home',
      fields: [
        {
          key: 'addressLine1',
          label: 'Address line 1',
          type: 'text',
          required: true,
          maxLength: 120,
          fullWidth: true,
        },
        {
          key: 'addressLine2',
          label: 'Address line 2',
          type: 'text',
          maxLength: 120,
          fullWidth: true,
        },
        { key: 'city', label: 'City', type: 'text', required: true, maxLength: 60 },
        { key: 'state', label: 'State / region', type: 'text', maxLength: 60 },
        { key: 'postalCode', label: 'Postal code', type: 'text', maxLength: 12 },
        { key: 'country', label: 'Country', type: 'text', required: true, maxLength: 60 },
      ],
    },
    {
      id: 'emergency',
      title: 'Emergency contact',
      description: 'Who to contact in an emergency.',
      icon: 'emergency',
      fields: [
        {
          key: 'emergencyName',
          label: 'Contact name',
          type: 'text',
          required: true,
          maxLength: 80,
        },
        {
          key: 'emergencyRelationship',
          label: 'Relationship',
          type: 'select',
          required: true,
          options: () => RELATIONSHIPS,
        },
        { key: 'emergencyPhone', label: 'Contact phone', type: 'tel', required: true },
      ],
    },
    {
      id: 'employment',
      title: 'Employment information',
      description: 'Contract type, status and key dates.',
      icon: 'work_history',
      fields: [
        {
          key: 'employeeCode',
          label: 'Employee code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 12,
          lockedOnEdit: true,
          hint: 'Suggested automatically',
        },
        {
          key: 'employmentType',
          label: 'Employment type',
          type: 'select',
          required: true,
          options: () => toOptions(EMPLOYMENT_TYPES),
        },
        {
          key: 'employmentStatus',
          label: 'Employment status',
          type: 'select',
          required: true,
          options: () => toOptions(EMPLOYMENT_STATUSES),
        },
        { key: 'joiningDate', label: 'Joining date', type: 'date', required: true },
        { key: 'probationEndDate', label: 'Probation end date', type: 'date' },
        {
          key: 'exitDate',
          label: 'Exit date',
          type: 'date',
          hint: 'Required for resigned or terminated employees',
        },
      ],
    },
    {
      id: 'organization',
      title: 'Organization assignment',
      description: 'Where the employee sits in the organization.',
      icon: 'corporate_fare',
      fields: [
        {
          key: 'companyId',
          label: 'Company',
          type: 'select',
          required: true,
          options: () => options(org.companies, (c) => c.name),
        },
        {
          key: 'branchId',
          label: 'Branch',
          type: 'select',
          required: true,
          dependsOn: 'companyId',
          options: (v) =>
            options(
              org.branches.filter((b) => b.companyId === v['companyId']),
              (b) => b.name,
            ),
        },
        {
          key: 'departmentId',
          label: 'Department',
          type: 'select',
          required: true,
          dependsOn: 'branchId',
          options: (v) =>
            options(
              org.departments.filter((d) => d.branchId === v['branchId']),
              (d) => d.name,
            ),
        },
        {
          key: 'sectionId',
          label: 'Section',
          type: 'select',
          nullable: true,
          dependsOn: 'departmentId',
          options: (v) =>
            options(
              org.sections.filter((s) => s.departmentId === v['departmentId']),
              (s) => s.name,
            ),
        },
        {
          key: 'positionId',
          label: 'Position',
          type: 'select',
          required: true,
          dependsOn: 'departmentId',
          options: (v) =>
            options(
              org.positions.filter((p) => p.departmentId === v['departmentId']),
              (p) => p.title,
            ),
        },
        {
          key: 'jobGradeId',
          label: 'Job grade',
          type: 'select',
          required: true,
          hint: 'Defaults from the chosen position',
          options: () => options(org.grades, (g) => `${g.code} · ${g.name}`),
        },
        {
          key: 'managerId',
          label: 'Reporting manager',
          type: 'select',
          nullable: true,
          options: (_v, { id }) => employeeOptions(people.filter((e) => e.id !== id)),
        },
        {
          key: 'shiftId',
          label: 'Work shift',
          type: 'select',
          nullable: true,
          options: () =>
            options(
              l.shifts.filter((s) => s.status === 'Active'),
              (s) => `${s.name} (${s.startTime}–${s.endTime})`,
            ),
        },
      ],
    },
    {
      id: 'bank',
      title: 'Bank information',
      description: 'Where salary is paid (sample data only).',
      icon: 'account_balance',
      fields: [
        { key: 'bankName', label: 'Bank name', type: 'text', required: true, maxLength: 60 },
        { key: 'bankBranchName', label: 'Bank branch', type: 'text', maxLength: 60 },
        {
          key: 'bankAccountName',
          label: 'Account holder name',
          type: 'text',
          required: true,
          maxLength: 80,
        },
        {
          key: 'bankAccountNumber',
          label: 'Account number',
          type: 'text',
          required: true,
          pattern: 'digits',
          minLength: 8,
          maxLength: 20,
          hint: 'Digits only, 8–20 characters',
        },
      ],
    },
  ];
}
