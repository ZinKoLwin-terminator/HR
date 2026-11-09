import { inject } from '@angular/core';
import { AppError, CreateModel } from '../../../core/models/common.models';
import {
  CURRENT_EMPLOYMENT_STATUSES,
  EMPLOYMENT_STATUSES,
  EMPLOYMENT_TYPES,
  Employee,
  EmployeeFilter,
  EmploymentStatus,
  EmploymentType,
  Gender,
  MaritalStatus,
  fullName,
} from '../../../core/models/employee.models';
import { ExcelColumn, ExcelModuleInfo, ImportIssue } from '../../../core/models/excel.models';
import { isDateInRange } from '../../../shared/utilities/date.util';
import { ShiftMockService } from '../../attendance/attendance-mock.services';
import { EmployeeMockService } from '../../employees/employee-mock.service';
import { EmploymentHistoryMockService } from '../../employees/employee-extras-mock.services';
import {
  BranchMockService,
  CompanyMockService,
  DepartmentMockService,
  JobGradeMockService,
  PositionMockService,
  SectionMockService,
} from '../../organization/organization-mock.services';
import { ParsedRow } from '../excel-coercion';
import {
  ExcelExportAdapter,
  ExcelImportAdapter,
  ExcelModuleRegistration,
  ExportRecord,
  ExportSheet,
} from '../excel-module';

const GENDERS: readonly Gender[] = ['Male', 'Female', 'Other'];
const MARITAL: readonly MaritalStatus[] = ['Single', 'Married', 'Divorced', 'Widowed'];
const RELATIONSHIPS = [
  'Spouse',
  'Father',
  'Mother',
  'Sibling',
  'Child',
  'Friend',
  'Other',
] as const;
const CODE = '^[A-Z0-9][A-Z0-9-]*$';
const CODE_MESSAGE = 'must use capital letters, numbers and dashes only.';

const text = (
  key: string,
  header: string,
  description: string,
  example: string,
  extra: Partial<ExcelColumn> = {},
): ExcelColumn => ({
  key,
  header,
  description,
  example,
  required: false,
  type: 'text',
  ...extra,
});

/** The import template: one row per employee, organization references by business code. */
const IMPORT_COLUMNS: readonly ExcelColumn[] = [
  text(
    'employeeCode',
    'Employee Code',
    'Unique code of the employee. Existing employees are matched by this code.',
    'EMP061',
    {
      required: true,
      maxLength: 12,
      uppercase: true,
      pattern: CODE,
      patternMessage: `Employee Code ${CODE_MESSAGE}`,
      aliases: ['Employee ID', 'Emp Code'],
    },
  ),
  text('firstName', 'First Name', 'Given name.', 'Mia', { required: true, maxLength: 60 }),
  text('lastName', 'Last Name', 'Family name.', 'Hartley', { required: true, maxLength: 60 }),
  {
    ...text('gender', 'Gender', 'Gender.', 'Female', { required: true }),
    type: 'enum',
    allowed: GENDERS,
  },
  {
    ...text(
      'dateOfBirth',
      'Date of Birth',
      'Employees must be at least 18 years old.',
      '1992-04-18',
      { required: true },
    ),
    type: 'date',
  },
  {
    ...text('maritalStatus', 'Marital Status', 'Marital status.', 'Single', { required: true }),
    type: 'enum',
    allowed: MARITAL,
  },
  text('nationality', 'Nationality', 'Nationality.', 'Myanmar', { required: true, maxLength: 60 }),
  text('nationalId', 'National ID', 'National ID or passport number.', 'NRC-123456', {
    required: true,
    maxLength: 40,
    sensitive: true,
  }),
  {
    ...text(
      'workEmail',
      'Work Email',
      'Company email address (must be unique).',
      'mia.hartley@apexhorizon.example',
      { required: true, maxLength: 120 },
    ),
    type: 'email',
  },
  {
    ...text(
      'personalEmail',
      'Personal Email',
      'Private email address (optional).',
      'mia@example.com',
      { maxLength: 120, sensitive: true },
    ),
    type: 'email',
  },
  {
    ...text(
      'phone',
      'Mobile Phone',
      'Mobile number, e.g. +95 9 123 456 789.',
      '+95 9 123 456 789',
      { required: true },
    ),
    type: 'phone',
  },
  {
    ...text(
      'alternatePhone',
      'Alternate Phone',
      'Second phone number (optional).',
      '+95 1 234 567',
      { sensitive: true },
    ),
    type: 'phone',
  },
  text('addressLine1', 'Address Line 1', 'Street address.', '12 Park Road', {
    required: true,
    maxLength: 120,
    sensitive: true,
  }),
  text('addressLine2', 'Address Line 2', 'Apartment, floor or landmark (optional).', 'Floor 3', {
    maxLength: 120,
    sensitive: true,
  }),
  text('city', 'City', 'City.', 'Yangon', { required: true, maxLength: 60 }),
  text('state', 'State / Region', 'State or region (optional).', 'Yangon Region', {
    maxLength: 60,
  }),
  text('postalCode', 'Postal Code', 'Postal code (optional).', '11181', { maxLength: 12 }),
  text('country', 'Country', 'Country.', 'Myanmar', { required: true, maxLength: 60 }),
  text(
    'emergencyName',
    'Emergency Contact Name',
    'Who to contact in an emergency.',
    'John Hartley',
    { required: true, maxLength: 80, sensitive: true },
  ),
  {
    ...text(
      'emergencyRelationship',
      'Emergency Contact Relationship',
      'Relationship to the employee.',
      'Spouse',
      { required: true, sensitive: true },
    ),
    type: 'enum',
    allowed: RELATIONSHIPS,
  },
  {
    ...text(
      'emergencyPhone',
      'Emergency Contact Phone',
      'Phone number of the emergency contact.',
      '+95 9 987 654 321',
      { required: true, sensitive: true },
    ),
    type: 'phone',
  },
  {
    ...text('employmentType', 'Employment Type', 'Type of employment.', 'Full-time', {
      required: true,
    }),
    type: 'enum',
    allowed: EMPLOYMENT_TYPES,
  },
  {
    ...text(
      'employmentStatus',
      'Employment Status',
      'Current status. Resigned and Terminated employees need an exit date.',
      'Active',
      { required: true },
    ),
    type: 'enum',
    allowed: EMPLOYMENT_STATUSES,
  },
  {
    ...text('joiningDate', 'Joining Date', 'First day of employment.', '2026-01-05', {
      required: true,
    }),
    type: 'date',
  },
  {
    ...text(
      'probationEndDate',
      'Probation End Date',
      'End of probation (optional, not before the joining date).',
      '2026-04-05',
    ),
    type: 'date',
  },
  {
    ...text(
      'exitDate',
      'Exit Date',
      'Last day of employment. Required for Resigned or Terminated.',
      '',
    ),
    type: 'date',
    example: null,
  },
  text(
    'companyCode',
    'Company Code',
    'Code of the company (see the "Organization codes" sheet).',
    'AHT',
    { required: true, maxLength: 20 },
  ),
  text('branchCode', 'Branch Code', 'Code of a branch of that company.', 'YGN-HQ', {
    required: true,
    maxLength: 20,
  }),
  text('departmentCode', 'Department Code', 'Code of a department of that branch.', 'ENG', {
    required: true,
    maxLength: 20,
  }),
  text('sectionCode', 'Section Code', 'Code of a section of that department (optional).', '', {
    maxLength: 20,
    example: null,
  }),
  text('positionCode', 'Position Code', 'Code of a position in that department.', 'ENG-SE', {
    required: true,
    maxLength: 20,
  }),
  text(
    'jobGradeCode',
    'Job Grade Code',
    'Job grade code. Leave blank to use the position’s grade.',
    'G3',
    { maxLength: 20, example: null },
  ),
  text(
    'managerCode',
    'Manager Employee Code',
    'Employee Code of the reporting manager (optional).',
    'EMP003',
    { maxLength: 12, example: null },
  ),
  text('shiftCode', 'Shift Code', 'Code of the work shift (optional).', 'GEN', {
    maxLength: 20,
    example: null,
  }),
  text('bankName', 'Bank Name', 'Bank where salary is paid (sample data only).', 'Sample Bank', {
    required: true,
    maxLength: 60,
    sensitive: true,
  }),
  text('bankBranchName', 'Bank Branch', 'Bank branch (optional).', 'Main Branch', {
    maxLength: 60,
    sensitive: true,
  }),
  text('bankAccountName', 'Bank Account Holder', 'Name on the bank account.', 'Mia Hartley', {
    required: true,
    maxLength: 80,
    sensitive: true,
  }),
  text(
    'bankAccountNumber',
    'Bank Account Number',
    'Digits only, 8 to 20 characters.',
    '1234567890',
    {
      required: true,
      minLength: 8,
      maxLength: 20,
      pattern: '^\\d+$',
      patternMessage: 'Bank Account Number must contain digits only.',
      sensitive: true,
    },
  ),
];

const exportColumn = (
  key: string,
  header: string,
  type: ExcelColumn['type'] = 'text',
  sensitive = false,
  width?: number,
): ExcelColumn => ({
  key,
  header,
  type,
  sensitive,
  width,
  description: '',
  required: false,
  example: null,
});

/** The export: names instead of codes for readability; personal and financial columns are sensitive. */
const EXPORT_COLUMNS: readonly ExcelColumn[] = [
  exportColumn('employeeCode', 'Employee Code', 'text', false, 14),
  exportColumn('fullName', 'Employee Name', 'text', false, 26),
  exportColumn('gender', 'Gender'),
  exportColumn('workEmail', 'Work Email', 'text', false, 32),
  exportColumn('phone', 'Mobile Phone'),
  exportColumn('company', 'Company', 'text', false, 28),
  exportColumn('branch', 'Branch', 'text', false, 24),
  exportColumn('department', 'Department', 'text', false, 28),
  exportColumn('section', 'Section', 'text', false, 22),
  exportColumn('position', 'Position', 'text', false, 28),
  exportColumn('jobGrade', 'Job Grade'),
  exportColumn('manager', 'Manager', 'text', false, 24),
  exportColumn('shift', 'Shift', 'text', false, 18),
  exportColumn('employmentType', 'Employment Type'),
  exportColumn('employmentStatus', 'Employment Status'),
  exportColumn('joiningDate', 'Joining Date', 'date'),
  exportColumn('probationEndDate', 'Probation End Date', 'date'),
  exportColumn('exitDate', 'Exit Date', 'date'),
  exportColumn('dateOfBirth', 'Date of Birth', 'date', true),
  exportColumn('nationalId', 'National ID', 'text', true),
  exportColumn('personalEmail', 'Personal Email', 'text', true, 30),
  exportColumn('alternatePhone', 'Alternate Phone', 'text', true),
  exportColumn('addressLine1', 'Address Line 1', 'text', true, 30),
  exportColumn('addressLine2', 'Address Line 2', 'text', true, 24),
  exportColumn('city', 'City', 'text', true),
  exportColumn('state', 'State / Region', 'text', true),
  exportColumn('postalCode', 'Postal Code', 'text', true),
  exportColumn('country', 'Country', 'text', true),
  exportColumn('emergencyName', 'Emergency Contact Name', 'text', true, 24),
  exportColumn('emergencyRelationship', 'Emergency Contact Relationship', 'text', true),
  exportColumn('emergencyPhone', 'Emergency Contact Phone', 'text', true),
  exportColumn('bankName', 'Bank Name', 'text', true),
  exportColumn('bankBranchName', 'Bank Branch', 'text', true),
  exportColumn('bankAccountName', 'Bank Account Holder', 'text', true, 24),
  exportColumn('bankAccountNumber', 'Bank Account Number', 'text', true, 22),
];

export const EMPLOYEE_MODULE_INFO: ExcelModuleInfo = {
  code: 'employees',
  label: 'Employees',
  sheetName: 'Employees',
  description:
    'Import employee records: personal, contact, employment and bank details with the organization assignment given by business codes (company, branch, department, position).',
  columns: IMPORT_COLUMNS,
  exportColumns: EXPORT_COLUMNS,
  matchKey: ['employeeCode'],
  matchKeyLabel: 'Employee Code',
  canImport: true,
  canExport: true,
  exportDateLabel: 'Joining date',
};

/** Which import column a model field named in a validation error belongs to. */
const FIELD_TO_COLUMN: Record<string, string> = {
  companyId: 'companyCode',
  branchId: 'branchCode',
  departmentId: 'departmentCode',
  sectionId: 'sectionCode',
  positionId: 'positionCode',
  jobGradeId: 'jobGradeCode',
  managerId: 'managerCode',
  shiftId: 'shiftCode',
};

const lower = (value: unknown): string =>
  String(value ?? '')
    .trim()
    .toLowerCase();
const present = (value: unknown): boolean => value !== null && value !== undefined && value !== '';

function blankEmployee(): Employee {
  return {
    id: 0,
    employeeCode: '',
    firstName: '',
    lastName: '',
    gender: 'Male',
    dateOfBirth: '',
    maritalStatus: 'Single',
    nationality: '',
    nationalId: '',
    workEmail: '',
    personalEmail: '',
    phone: '',
    alternatePhone: '',
    address: { line1: '', line2: '', city: '', state: '', postalCode: '', country: '' },
    emergencyContact: { name: '', relationship: '', phone: '' },
    companyId: 0,
    branchId: 0,
    departmentId: 0,
    sectionId: null,
    positionId: 0,
    jobGradeId: 0,
    managerId: null,
    shiftId: null,
    employmentType: 'Full-time',
    employmentStatus: 'Active',
    joiningDate: '',
    probationEndDate: null,
    exitDate: null,
    bank: { bankName: '', branchName: '', accountName: '', accountNumber: '' },
    photoUrl: null,
  };
}

/** Wires the employee data service into the shared Excel engine. Call within an injection context. */
export function employeeExcelRegistration(): ExcelModuleRegistration {
  const employees = inject(EmployeeMockService);
  const companies = inject(CompanyMockService);
  const branches = inject(BranchMockService);
  const departments = inject(DepartmentMockService);
  const sections = inject(SectionMockService);
  const positions = inject(PositionMockService);
  const grades = inject(JobGradeMockService);
  const shifts = inject(ShiftMockService);
  const history = inject(EmploymentHistoryMockService);

  const headerOf = (key: string) => IMPORT_COLUMNS.find((c) => c.key === key)?.header ?? key;

  const importAdapter: ExcelImportAdapter = {
    findExisting: (row: ParsedRow) =>
      employees.records().find((e) => lower(e.employeeCode) === lower(row['employeeCode']))?.id ??
      null,

    columnForField: (field) => FIELD_TO_COLUMN[field],

    toModel: (row, existingId) => {
      const issues: ImportIssue[] = [];
      const problem = (key: string, message: string) =>
        issues.push({
          field: headerOf(key),
          value: String(row[key] ?? ''),
          message,
          severity: 'Error',
        });

      // Organization references are business codes, resolved within their scope.
      const company = companies.records().find((c) => lower(c.code) === lower(row['companyCode']));
      if (!company) problem('companyCode', `Company "${row['companyCode']}" does not exist.`);
      const branch = company
        ? branches
            .records()
            .find((b) => b.companyId === company.id && lower(b.code) === lower(row['branchCode']))
        : undefined;
      if (company && !branch) {
        problem(
          'branchCode',
          `Branch "${row['branchCode']}" does not exist in company ${company.name}.`,
        );
      }
      const department = branch
        ? departments
            .records()
            .find((d) => d.branchId === branch.id && lower(d.code) === lower(row['departmentCode']))
        : undefined;
      if (branch && !department) {
        problem(
          'departmentCode',
          `Department "${row['departmentCode']}" does not exist in branch ${branch.name}.`,
        );
      }
      const section =
        department && present(row['sectionCode'])
          ? sections
              .records()
              .find(
                (s) =>
                  s.departmentId === department.id && lower(s.code) === lower(row['sectionCode']),
              )
          : undefined;
      if (department && present(row['sectionCode']) && !section) {
        problem(
          'sectionCode',
          `Section "${row['sectionCode']}" does not exist in department ${department.name}.`,
        );
      }
      const position = department
        ? positions
            .records()
            .find(
              (p) =>
                p.departmentId === department.id && lower(p.code) === lower(row['positionCode']),
            )
        : undefined;
      if (department && !position) {
        problem(
          'positionCode',
          `Position "${row['positionCode']}" does not exist in department ${department.name}.`,
        );
      }
      let gradeId = position?.jobGradeId ?? 0;
      if (present(row['jobGradeCode'])) {
        const grade = grades.records().find((g) => lower(g.code) === lower(row['jobGradeCode']));
        if (grade) gradeId = grade.id;
        else problem('jobGradeCode', `Job grade "${row['jobGradeCode']}" does not exist.`);
      }
      let managerId: number | null | undefined;
      if (present(row['managerCode'])) {
        const manager = employees
          .records()
          .find((e) => lower(e.employeeCode) === lower(row['managerCode']));
        if (!manager) problem('managerCode', `Manager "${row['managerCode']}" does not exist.`);
        else if (!CURRENT_EMPLOYMENT_STATUSES.includes(manager.employmentStatus)) {
          problem(
            'managerCode',
            `Manager "${manager.employeeCode}" is no longer a current employee.`,
          );
        } else managerId = manager.id;
      }
      let shiftId: number | null | undefined;
      if (present(row['shiftCode'])) {
        const shift = shifts.records().find((s) => lower(s.code) === lower(row['shiftCode']));
        if (!shift) problem('shiftCode', `Shift "${row['shiftCode']}" does not exist.`);
        else shiftId = shift.id;
      }
      if (issues.length > 0 || !company || !branch || !department || !position) {
        return { model: null, issues };
      }

      // Blank optional cells keep the current value when updating (and are empty for new records).
      const existing = existingId === null ? undefined : employees.findById(existingId);
      const model = existing ? structuredClone(existing) : blankEmployee();
      const str = (key: string): string | null => (present(row[key]) ? String(row[key]) : null);
      const assign = (key: string, apply: (value: string) => void) => {
        const value = str(key);
        if (value !== null) apply(value);
      };
      if (!existing) model.employeeCode = String(row['employeeCode']);
      assign('firstName', (v) => (model.firstName = v));
      assign('lastName', (v) => (model.lastName = v));
      assign('gender', (v) => (model.gender = v as Gender));
      assign('dateOfBirth', (v) => (model.dateOfBirth = v));
      assign('maritalStatus', (v) => (model.maritalStatus = v as MaritalStatus));
      assign('nationality', (v) => (model.nationality = v));
      assign('nationalId', (v) => (model.nationalId = v));
      assign('workEmail', (v) => (model.workEmail = v));
      assign('personalEmail', (v) => (model.personalEmail = v));
      assign('phone', (v) => (model.phone = v));
      assign('alternatePhone', (v) => (model.alternatePhone = v));
      assign('addressLine1', (v) => (model.address.line1 = v));
      assign('addressLine2', (v) => (model.address.line2 = v));
      assign('city', (v) => (model.address.city = v));
      assign('state', (v) => (model.address.state = v));
      assign('postalCode', (v) => (model.address.postalCode = v));
      assign('country', (v) => (model.address.country = v));
      assign('emergencyName', (v) => (model.emergencyContact.name = v));
      assign('emergencyRelationship', (v) => (model.emergencyContact.relationship = v));
      assign('emergencyPhone', (v) => (model.emergencyContact.phone = v));
      assign('employmentType', (v) => (model.employmentType = v as EmploymentType));
      assign('employmentStatus', (v) => (model.employmentStatus = v as EmploymentStatus));
      assign('joiningDate', (v) => (model.joiningDate = v));
      assign('probationEndDate', (v) => (model.probationEndDate = v));
      assign('exitDate', (v) => (model.exitDate = v));
      assign('bankName', (v) => (model.bank.bankName = v));
      assign('bankBranchName', (v) => (model.bank.branchName = v));
      assign('bankAccountName', (v) => (model.bank.accountName = v));
      assign('bankAccountNumber', (v) => (model.bank.accountNumber = v));
      model.companyId = company.id;
      model.branchId = branch.id;
      model.departmentId = department.id;
      model.positionId = position.id;
      model.jobGradeId = gradeId;
      if (present(row['sectionCode'])) model.sectionId = section?.id ?? null;
      else if (!existing || existing.departmentId !== department.id) model.sectionId = null;
      if (managerId !== undefined) model.managerId = managerId;
      if (shiftId !== undefined) model.shiftId = shiftId;

      const warnings: ImportIssue[] = [];
      if (model.employmentStatus === 'Probation' && !model.probationEndDate) {
        warnings.push({
          field: headerOf('probationEndDate'),
          value: '',
          message: 'The employee is on probation but has no probation end date.',
          suggestion: 'Add the probation end date.',
          severity: 'Warning',
        });
      }
      const { id: _id, ...withoutId } = model;
      void _id;
      return { model: withoutId as unknown as Record<string, unknown>, issues: warnings };
    },

    dryRun: (model, existingId): AppError | null =>
      employees.dryRun(model as unknown as CreateModel<Employee>, existingId),

    apply: (model, existingId) => {
      if (existingId === null)
        employees.createNow(model as unknown as CreateModel<Employee>, false);
      else employees.updateNow(existingId, model as unknown as CreateModel<Employee>, false);
    },

    // Roll back the employees and the history entries created for them.
    snapshot: () => {
      const restoreEmployees = employees.snapshot();
      const restoreHistory = history.snapshot();
      return () => {
        restoreEmployees();
        restoreHistory();
      };
    },
  };

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filtered = request.scope === 'filtered';
      let items = employees.filterNow(
        filtered ? request.search : undefined,
        filtered ? ((request.filters ?? {}) as Partial<EmployeeFilter>) : undefined,
      );
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      items = items
        .filter((i) => isDateInRange(i.joiningDate, request.dateFrom, request.dateTo))
        .sort((a, b) => a.employeeCode.localeCompare(b.employeeCode))
        .slice(0, limit + 1);

      return items.map((item): ExportRecord => {
        const e = employees.findById(item.id) as Employee;
        const manager = e.managerId ? employees.findById(e.managerId) : undefined;
        return {
          employeeCode: e.employeeCode,
          fullName: fullName(e),
          gender: e.gender,
          workEmail: e.workEmail,
          phone: e.phone,
          company: companies.findById(e.companyId)?.name ?? '',
          branch: item.branchName,
          department: item.departmentName,
          section: e.sectionId ? (sections.findById(e.sectionId)?.name ?? '') : '',
          position: item.positionTitle,
          jobGrade: grades.findById(e.jobGradeId)?.code ?? '',
          manager: manager ? fullName(manager) : '',
          shift: e.shiftId ? item.shiftName : '',
          employmentType: e.employmentType,
          employmentStatus: e.employmentStatus,
          joiningDate: e.joiningDate,
          probationEndDate: e.probationEndDate,
          exitDate: e.exitDate,
          dateOfBirth: e.dateOfBirth,
          nationalId: e.nationalId,
          personalEmail: e.personalEmail,
          alternatePhone: e.alternatePhone,
          addressLine1: e.address.line1,
          addressLine2: e.address.line2,
          city: e.address.city,
          state: e.address.state,
          postalCode: e.address.postalCode,
          country: e.address.country,
          emergencyName: e.emergencyContact.name,
          emergencyRelationship: e.emergencyContact.relationship,
          emergencyPhone: e.emergencyContact.phone,
          bankName: e.bank.bankName,
          bankBranchName: e.bank.branchName,
          bankAccountName: e.bank.accountName,
          bankAccountNumber: e.bank.accountNumber,
        };
      });
    },

    // Headcount per department and branch (of the exported employees).
    extraSheets: (_request, rows) => {
      const count = (key: string) => {
        const totals = new Map<string, number>();
        for (const row of rows) {
          const name = String(row[key] ?? '') || '—';
          totals.set(name, (totals.get(name) ?? 0) + 1);
        }
        return [...totals.entries()].sort((a, b) => a[0].localeCompare(b[0]));
      };
      const sheet = (name: string, label: string, key: string): ExportSheet => ({
        name,
        columns: [
          {
            key: 'name',
            header: label,
            type: 'text',
            description: '',
            required: false,
            example: null,
            width: 34,
          },
          {
            key: 'employees',
            header: 'Employees',
            type: 'integer',
            description: '',
            required: false,
            example: null,
          },
        ],
        rows: count(key).map(([name, employees]) => ({ name, employees })),
      });
      return [
        sheet('Department Summary', 'Department', 'department'),
        sheet('Branch Summary', 'Branch', 'branch'),
      ];
    },
  };

  /** Lookup sheets for the template: every valid organization code, and job grade / shift codes. */
  const referenceSheets = (): ExportSheet[] => {
    const col = (key: string, header: string, width = 24): ExcelColumn => ({
      key,
      header,
      type: 'text',
      description: '',
      required: false,
      example: null,
      width,
    });
    const orgRows: ExportRecord[] = positions.records().flatMap((position) => {
      const department = departments.findById(position.departmentId);
      const branch = department ? branches.findById(department.branchId) : undefined;
      const company = branch ? companies.findById(branch.companyId) : undefined;
      if (!department || !branch || !company) return [];
      return [
        {
          companyCode: company.code,
          company: company.name,
          branchCode: branch.code,
          branch: branch.name,
          departmentCode: department.code,
          department: department.name,
          positionCode: position.code,
          position: position.title,
        },
      ];
    });
    return [
      {
        name: 'Organization codes',
        columns: [
          col('companyCode', 'Company Code', 14),
          col('company', 'Company', 28),
          col('branchCode', 'Branch Code', 14),
          col('branch', 'Branch', 28),
          col('departmentCode', 'Department Code', 16),
          col('department', 'Department', 28),
          col('positionCode', 'Position Code', 16),
          col('position', 'Position', 32),
        ],
        rows: orgRows,
      },
      {
        name: 'Grade and shift codes',
        columns: [col('type', 'Type', 12), col('code', 'Code', 14), col('name', 'Name', 32)],
        rows: [
          ...grades
            .records()
            .map((g): ExportRecord => ({ type: 'Job grade', code: g.code, name: g.name })),
          ...shifts
            .records()
            .map((s): ExportRecord => ({ type: 'Shift', code: s.code, name: s.name })),
        ],
      },
    ];
  };

  return { info: EMPLOYEE_MODULE_INFO, importAdapter, exportAdapter, referenceSheets };
}
