import { inject } from '@angular/core';
import { Entity, RecordStatus } from '../../../core/models/common.models';
import { ExcelColumn, ExcelModuleInfo, ImportIssue } from '../../../core/models/excel.models';
import {
  Branch,
  BranchType,
  Company,
  Department,
  JobGrade,
  Position,
  Section,
} from '../../../core/models/organization.models';
import { EmployeeMockService } from '../../employees/employee-mock.service';
import { InMemoryCrudService } from '../../in-memory-crud.service';
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
import {
  codeColumn,
  column,
  lower,
  outColumn,
  present,
  referenceColumn,
  statusColumn,
} from './excel-columns';

/**
 * Excel modules of the organization resources (companies, branches, departments, sections,
 * positions, job grades). They mirror the API's modules: records are matched by their business code,
 * parents are given by code, a blank optional cell keeps the current value and an optional Status
 * column activates or deactivates. Rows are applied through the same mock services the screens use,
 * so uniqueness and parent/child rules are the screens' own.
 */

const BRANCH_TYPES: readonly BranchType[] = [
  'Head Office',
  'Branch',
  'Regional Office',
  'Warehouse',
];

type Coded = Entity & { code: string; status: RecordStatus };
type Problem = (key: string, message: string) => void;

interface CodedSpec<T extends Coded> {
  info: ExcelModuleInfo;
  service: InMemoryCrudService<T>;
  /** Model field a validation error names → the column the user corrects. */
  fieldToColumn: Record<string, string>;
  /** Defaults of a new record. */
  blank: () => Omit<T, 'id'>;
  /** Columns copied as they are (key = model field). */
  textKeys: readonly string[];
  numberKeys?: readonly string[];
  /** Resolves the code columns into ids; reports each unknown code through `problem`. */
  references: (row: ParsedRow, existing: T | undefined, problem: Problem) => Partial<T>;
  /** The export row of a record (every column key). */
  record: (entity: T) => ExportRecord;
  referenceSheets?: () => ExportSheet[];
}

const sheetColumn = (key: string, header: string, width = 24): ExcelColumn => ({
  key,
  header,
  type: 'text',
  width,
  description: '',
  required: false,
  example: null,
});

function codedRegistration<T extends Coded>(spec: CodedSpec<T>): ExcelModuleRegistration {
  const { info, service } = spec;
  const headerOf = (key: string) => info.columns.find((c) => c.key === key)?.header ?? key;

  const importAdapter: ExcelImportAdapter = {
    findExisting: (row) =>
      service.records().find((r) => lower(r.code) === lower(row['code']))?.id ?? null,

    columnForField: (field) => spec.fieldToColumn[field],

    toModel: (row, existingId) => {
      const issues: ImportIssue[] = [];
      const existing = existingId === null ? undefined : service.findById(existingId);
      const references = spec.references(row, existing, (key, message) =>
        issues.push({
          field: headerOf(key),
          value: String(row[key] ?? ''),
          message,
          severity: 'Error',
        }),
      );
      if (issues.length > 0) return { model: null, issues };

      const { id: _id, ...base } = existing
        ? structuredClone(existing)
        : { ...spec.blank(), id: 0 };
      void _id;
      const model = base as Record<string, unknown>;
      if (!existing) model['code'] = String(row['code']);
      for (const key of spec.textKeys) if (present(row[key])) model[key] = String(row[key]);
      for (const key of spec.numberKeys ?? []) if (present(row[key])) model[key] = Number(row[key]);
      if (present(row['status'])) model['status'] = String(row['status']);
      Object.assign(model, references);
      return { model, issues: [] };
    },

    dryRun: (model, existingId) => service.dryRun(model as never, existingId),

    apply: (model, existingId) => {
      if (existingId === null) service.createNow(model as never, false);
      else service.updateNow(existingId, model as never, false);
    },

    snapshot: () => service.snapshot(),
  };

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filtered = request.scope === 'filtered';
      let items = service.filterNow(
        filtered ? request.search : undefined,
        filtered ? (request.filters ?? {}) : undefined,
      );
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      return items
        .sort((a, b) => a.code.localeCompare(b.code))
        .slice(0, limit + 1)
        .map((item) => spec.record(item));
    },
  };

  return {
    info,
    importAdapter,
    exportAdapter,
    referenceSheets: spec.referenceSheets,
  };
}

function moduleInfo(
  code: string,
  label: string,
  description: string,
  columns: readonly ExcelColumn[],
  exportColumns: readonly ExcelColumn[] | undefined,
  matchKeyLabel: string,
): ExcelModuleInfo {
  return {
    code,
    label,
    sheetName: label,
    description,
    columns,
    exportColumns,
    matchKey: ['code'],
    matchKeyLabel,
    canImport: true,
    canExport: true,
  };
}

const text = (key: string, header: string, description: string, example: string, max: number) =>
  column(key, header, description, example, { required: true, maxLength: max });
const optionalText = (
  key: string,
  header: string,
  description: string,
  example: string,
  max: number,
) => column(key, header, description, example, { maxLength: max });

// ---- Companies ----------------------------------------------------------------------------

const COMPANY_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Company', 'AHT'),
  text('name', 'Company Name', 'Short name of the company (must be unique).', 'Apex Horizon', 100),
  text('legalName', 'Legal Name', 'Registered legal name.', 'Apex Horizon Trading Co., Ltd.', 150),
  optionalText(
    'registrationNumber',
    'Registration Number',
    'Company registration number (optional).',
    'REG-0001',
    40,
  ),
  optionalText('taxId', 'Tax ID', 'Tax identification number (optional).', 'TAX-0001', 40),
  text('industry', 'Industry', 'Line of business.', 'Trading', 80),
  column('email', 'Email', 'Company email address.', 'info@apexhorizon.example', {
    required: true,
    maxLength: 120,
    type: 'email',
  }),
  column('phone', 'Phone', 'Company phone number.', '+95 1 234 567', {
    required: true,
    type: 'phone',
  }),
  optionalText('website', 'Website', 'Website address (optional).', 'www.apexhorizon.example', 150),
  optionalText('address', 'Address', 'Street address (optional).', '12 Park Road', 200),
  text('city', 'City', 'City.', 'Yangon', 60),
  text('country', 'Country', 'Country.', 'Myanmar', 60),
  column('foundedDate', 'Founded Date', 'Date the company was founded.', '2015-06-01', {
    required: true,
    type: 'date',
  }),
  statusColumn(),
];

export const COMPANY_MODULE_INFO = moduleInfo(
  'companies',
  'Companies',
  'Import company records. Existing companies are matched by Company Code.',
  COMPANY_COLUMNS,
  undefined,
  'Company Code',
);

export function companyExcelRegistration(): ExcelModuleRegistration {
  return codedRegistration<Company>({
    info: COMPANY_MODULE_INFO,
    service: inject(CompanyMockService),
    fieldToColumn: { code: 'code' },
    blank: () => ({
      code: '',
      name: '',
      legalName: '',
      registrationNumber: '',
      taxId: '',
      industry: '',
      email: '',
      phone: '',
      website: '',
      address: '',
      city: '',
      country: '',
      foundedDate: '',
      status: 'Active',
    }),
    textKeys: [
      'name',
      'legalName',
      'registrationNumber',
      'taxId',
      'industry',
      'email',
      'phone',
      'website',
      'address',
      'city',
      'country',
      'foundedDate',
    ],
    references: () => ({}),
    record: (c) => ({ ...c }),
  });
}

// ---- Branches -----------------------------------------------------------------------------

const BRANCH_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Branch', 'YGN-HQ'),
  text('name', 'Branch Name', 'Name of the branch (must be unique).', 'Yangon Head Office', 100),
  referenceColumn(
    'companyCode',
    'Company Code',
    'Code of the company the branch belongs to (see the "Company codes" sheet).',
    'AHT',
  ),
  column('type', 'Branch Type', 'Kind of branch.', 'Head Office', {
    required: true,
    type: 'enum',
    allowed: BRANCH_TYPES,
  }),
  column('email', 'Email', 'Branch email address (optional).', 'yangon@apexhorizon.example', {
    maxLength: 120,
    type: 'email',
  }),
  column('phone', 'Phone', 'Branch phone number (optional).', '+95 1 234 567', { type: 'phone' }),
  optionalText('address', 'Address', 'Street address (optional).', '12 Park Road', 200),
  text('city', 'City', 'City.', 'Yangon', 60),
  text('country', 'Country', 'Country.', 'Myanmar', 60),
  column(
    'managerCode',
    'Manager Employee Code',
    'Employee Code of the branch manager (optional).',
    null,
    {
      maxLength: 12,
    },
  ),
  column('openedDate', 'Opened Date', 'Date the branch opened.', '2018-01-15', {
    required: true,
    type: 'date',
  }),
  statusColumn(),
];

export const BRANCH_MODULE_INFO = moduleInfo(
  'branches',
  'Branches',
  'Import branch records. Existing branches are matched by Branch Code; the company is given by its code.',
  BRANCH_COLUMNS,
  [...BRANCH_COLUMNS, outColumn('company', 'Company', 'text', 28)],
  'Branch Code',
);

/** Looks an employee up by code, as the "manager / head / supervisor" columns do. */
function employeeIdByCode(
  employees: EmployeeMockService,
  row: ParsedRow,
  key: string,
  label: string,
  problem: Problem,
): number | null | undefined {
  if (!present(row[key])) return undefined;
  const found = employees.records().find((e) => lower(e.employeeCode) === lower(row[key]));
  if (!found) {
    problem(key, `${label} "${row[key]}" does not exist.`);
    return undefined;
  }
  return found.id;
}

export function branchExcelRegistration(): ExcelModuleRegistration {
  const companies = inject(CompanyMockService);
  const employees = inject(EmployeeMockService);
  const code = (id: number | null) =>
    id === null ? '' : (employees.findById(id)?.employeeCode ?? '');
  return codedRegistration<Branch>({
    info: BRANCH_MODULE_INFO,
    service: inject(BranchMockService),
    fieldToColumn: {
      code: 'code',
      companyId: 'companyCode',
      managerEmployeeId: 'managerCode',
      type: 'type',
    },
    blank: () => ({
      companyId: 0,
      code: '',
      name: '',
      type: 'Branch',
      email: '',
      phone: '',
      address: '',
      city: '',
      country: '',
      managerEmployeeId: null,
      openedDate: '',
      status: 'Active',
    }),
    textKeys: ['name', 'type', 'email', 'phone', 'address', 'city', 'country', 'openedDate'],
    references: (row, _existing, problem) => {
      const company = companies.records().find((c) => lower(c.code) === lower(row['companyCode']));
      if (!company) problem('companyCode', `Company "${row['companyCode']}" does not exist.`);
      const manager = employeeIdByCode(employees, row, 'managerCode', 'Manager', problem);
      return {
        ...(company ? { companyId: company.id } : {}),
        ...(manager === undefined ? {} : { managerEmployeeId: manager }),
      };
    },
    record: (b) => ({
      ...b,
      companyCode: companies.findById(b.companyId)?.code ?? '',
      managerCode: code(b.managerEmployeeId),
      company: companies.findById(b.companyId)?.name ?? '',
    }),
    referenceSheets: () => [
      {
        name: 'Company codes',
        columns: [sheetColumn('code', 'Company Code', 16), sheetColumn('name', 'Company', 30)],
        rows: companies.records().map((c) => ({ code: c.code, name: c.name })),
      },
    ],
  });
}

// ---- Departments --------------------------------------------------------------------------

const DEPARTMENT_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Department', 'ENG'),
  text('name', 'Department Name', 'Name of the department.', 'Engineering', 100),
  referenceColumn(
    'companyCode',
    'Company Code',
    'Code of the company (see the "Organization codes" sheet).',
    'AHT',
  ),
  referenceColumn('branchCode', 'Branch Code', 'Code of a branch of that company.', 'YGN-HQ'),
  optionalText(
    'description',
    'Description',
    'What the department does (optional).',
    'Software development',
    300,
  ),
  column(
    'headCode',
    'Head Employee Code',
    'Employee Code of the department head (optional).',
    null,
    {
      maxLength: 12,
    },
  ),
  optionalText('costCenter', 'Cost Center', 'Cost center code (optional).', 'CC-100', 20),
  statusColumn(),
];

export const DEPARTMENT_MODULE_INFO = moduleInfo(
  'departments',
  'Departments',
  'Import department records. Existing departments are matched by Department Code; company and branch are given by their codes.',
  DEPARTMENT_COLUMNS,
  [
    ...DEPARTMENT_COLUMNS,
    outColumn('company', 'Company', 'text', 28),
    outColumn('branch', 'Branch', 'text', 24),
  ],
  'Department Code',
);

export function departmentExcelRegistration(): ExcelModuleRegistration {
  const companies = inject(CompanyMockService);
  const branches = inject(BranchMockService);
  const employees = inject(EmployeeMockService);
  return codedRegistration<Department>({
    info: DEPARTMENT_MODULE_INFO,
    service: inject(DepartmentMockService),
    fieldToColumn: {
      code: 'code',
      companyId: 'companyCode',
      branchId: 'branchCode',
      headEmployeeId: 'headCode',
    },
    blank: () => ({
      companyId: 0,
      branchId: 0,
      code: '',
      name: '',
      description: '',
      headEmployeeId: null,
      costCenter: '',
      status: 'Active',
    }),
    textKeys: ['name', 'description', 'costCenter'],
    references: (row, _existing, problem) => {
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
      const head = employeeIdByCode(employees, row, 'headCode', 'Head', problem);
      return {
        ...(company ? { companyId: company.id } : {}),
        ...(branch ? { branchId: branch.id } : {}),
        ...(head === undefined ? {} : { headEmployeeId: head }),
      };
    },
    record: (d) => ({
      ...d,
      companyCode: companies.findById(d.companyId)?.code ?? '',
      branchCode: branches.findById(d.branchId)?.code ?? '',
      headCode: d.headEmployeeId ? (employees.findById(d.headEmployeeId)?.employeeCode ?? '') : '',
      company: companies.findById(d.companyId)?.name ?? '',
      branch: branches.findById(d.branchId)?.name ?? '',
    }),
    referenceSheets: () => [
      {
        name: 'Organization codes',
        columns: [
          sheetColumn('companyCode', 'Company Code', 14),
          sheetColumn('company', 'Company', 28),
          sheetColumn('branchCode', 'Branch Code', 14),
          sheetColumn('branch', 'Branch', 28),
        ],
        rows: branches.records().flatMap((b) => {
          const company = companies.findById(b.companyId);
          return company
            ? [
                {
                  companyCode: company.code,
                  company: company.name,
                  branchCode: b.code,
                  branch: b.name,
                },
              ]
            : [];
        }),
      },
    ],
  });
}

// ---- Sections -----------------------------------------------------------------------------

const SECTION_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Section', 'ENG-BE'),
  text('name', 'Section Name', 'Name of the section.', 'Back-end', 100),
  referenceColumn(
    'departmentCode',
    'Department Code',
    'Code of the department the section belongs to (see the "Department codes" sheet).',
    'ENG',
  ),
  optionalText(
    'description',
    'Description',
    'What the section does (optional).',
    'Server-side services',
    300,
  ),
  column(
    'supervisorCode',
    'Supervisor Employee Code',
    'Employee Code of the section supervisor (optional).',
    null,
    { maxLength: 12 },
  ),
  statusColumn(),
];

export const SECTION_MODULE_INFO = moduleInfo(
  'sections',
  'Sections',
  'Import section records. Existing sections are matched by Section Code; the department is given by its code.',
  SECTION_COLUMNS,
  [...SECTION_COLUMNS, outColumn('department', 'Department', 'text', 28)],
  'Section Code',
);

const departmentCodesSheet = (departments: DepartmentMockService): ExportSheet => ({
  name: 'Department codes',
  columns: [sheetColumn('code', 'Department Code', 18), sheetColumn('name', 'Department', 30)],
  rows: departments.records().map((d) => ({ code: d.code, name: d.name })),
});

export function sectionExcelRegistration(): ExcelModuleRegistration {
  const departments = inject(DepartmentMockService);
  const employees = inject(EmployeeMockService);
  return codedRegistration<Section>({
    info: SECTION_MODULE_INFO,
    service: inject(SectionMockService),
    fieldToColumn: {
      code: 'code',
      departmentId: 'departmentCode',
      supervisorEmployeeId: 'supervisorCode',
    },
    blank: () => ({
      departmentId: 0,
      code: '',
      name: '',
      description: '',
      supervisorEmployeeId: null,
      status: 'Active',
    }),
    textKeys: ['name', 'description'],
    references: (row, _existing, problem) => {
      const department = departments
        .records()
        .find((d) => lower(d.code) === lower(row['departmentCode']));
      if (!department) {
        problem('departmentCode', `Department "${row['departmentCode']}" does not exist.`);
      }
      const supervisor = employeeIdByCode(employees, row, 'supervisorCode', 'Supervisor', problem);
      return {
        ...(department ? { departmentId: department.id } : {}),
        ...(supervisor === undefined ? {} : { supervisorEmployeeId: supervisor }),
      };
    },
    record: (s) => ({
      ...s,
      departmentCode: departments.findById(s.departmentId)?.code ?? '',
      supervisorCode: s.supervisorEmployeeId
        ? (employees.findById(s.supervisorEmployeeId)?.employeeCode ?? '')
        : '',
      department: departments.findById(s.departmentId)?.name ?? '',
    }),
    referenceSheets: () => [departmentCodesSheet(departments)],
  });
}

// ---- Job grades ---------------------------------------------------------------------------

const GRADE_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Grade', 'G3'),
  text('name', 'Grade Name', 'Name of the job grade.', 'Senior', 60),
  column('level', 'Level', 'Grade level, 1 or higher.', 3, {
    required: true,
    type: 'integer',
    min: 1,
    max: 32767,
  }),
  column('minSalary', 'Minimum Salary', 'Lowest monthly salary of the band (sample data).', 1000, {
    required: true,
    type: 'decimal',
    min: 0,
    decimals: 2,
  }),
  column(
    'maxSalary',
    'Maximum Salary',
    'Highest monthly salary of the band (not below the minimum).',
    2500,
    { required: true, type: 'decimal', min: 0, decimals: 2 },
  ),
  optionalText(
    'description',
    'Description',
    'Description of the grade (optional).',
    'Senior individual contributor',
    300,
  ),
  statusColumn(),
];

export const JOB_GRADE_MODULE_INFO = moduleInfo(
  'job-grades',
  'Job grades',
  'Import job grades with their salary bands. Existing grades are matched by Grade Code.',
  GRADE_COLUMNS,
  undefined,
  'Grade Code',
);

export function jobGradeExcelRegistration(): ExcelModuleRegistration {
  return codedRegistration<JobGrade>({
    info: JOB_GRADE_MODULE_INFO,
    service: inject(JobGradeMockService),
    fieldToColumn: { code: 'code' },
    blank: () => ({
      code: '',
      name: '',
      level: 1,
      minSalary: 0,
      maxSalary: 0,
      description: '',
      status: 'Active',
    }),
    textKeys: ['name', 'description'],
    numberKeys: ['level', 'minSalary', 'maxSalary'],
    references: () => ({}),
    record: (g) => ({ ...g }),
  });
}

// ---- Positions (job titles) ---------------------------------------------------------------

const POSITION_COLUMNS: readonly ExcelColumn[] = [
  codeColumn('Position', 'ENG-SE'),
  column('title', 'Position Title', 'Title of the position.', 'Software Engineer', {
    required: true,
    maxLength: 100,
    aliases: ['Job Title', 'Title'],
  }),
  referenceColumn(
    'departmentCode',
    'Department Code',
    'Code of the department (see the "Department codes" sheet).',
    'ENG',
  ),
  referenceColumn(
    'sectionCode',
    'Section Code',
    'Code of a section of that department (optional).',
    null,
    false,
  ),
  referenceColumn(
    'jobGradeCode',
    'Job Grade Code',
    'Code of the job grade (see the "Grade codes" sheet).',
    'G3',
  ),
  optionalText(
    'description',
    'Description',
    'What the position does (optional).',
    'Builds and maintains services',
    300,
  ),
  column('headcount', 'Headcount', 'Number of people the position can hold, 1 or more.', 2, {
    required: true,
    type: 'integer',
    min: 1,
    max: 32767,
  }),
  referenceColumn(
    'reportsToCode',
    'Reports To Position Code',
    'Code of the position this one reports to (optional).',
    null,
    false,
  ),
  statusColumn(),
];

export const POSITION_MODULE_INFO = moduleInfo(
  'positions',
  'Positions',
  'Import job positions (titles). Existing positions are matched by Position Code; department, section and grade are given by their codes.',
  POSITION_COLUMNS,
  [
    ...POSITION_COLUMNS,
    outColumn('department', 'Department', 'text', 28),
    outColumn('jobGrade', 'Job Grade'),
  ],
  'Position Code',
);

export function positionExcelRegistration(): ExcelModuleRegistration {
  const departments = inject(DepartmentMockService);
  const sections = inject(SectionMockService);
  const grades = inject(JobGradeMockService);
  const positions = inject(PositionMockService);
  return codedRegistration<Position>({
    info: POSITION_MODULE_INFO,
    service: positions,
    fieldToColumn: {
      code: 'code',
      departmentId: 'departmentCode',
      sectionId: 'sectionCode',
      jobGradeId: 'jobGradeCode',
      reportsToPositionId: 'reportsToCode',
    },
    blank: () => ({
      departmentId: 0,
      sectionId: null,
      jobGradeId: 0,
      code: '',
      title: '',
      description: '',
      headcount: 1,
      reportsToPositionId: null,
      status: 'Active',
    }),
    textKeys: ['title', 'description'],
    numberKeys: ['headcount'],
    references: (row, _existing, problem) => {
      const result: Partial<Position> = {};
      const department = departments
        .records()
        .find((d) => lower(d.code) === lower(row['departmentCode']));
      if (!department) {
        problem('departmentCode', `Department "${row['departmentCode']}" does not exist.`);
      } else result.departmentId = department.id;
      if (department && present(row['sectionCode'])) {
        const section = sections
          .records()
          .find(
            (s) => s.departmentId === department.id && lower(s.code) === lower(row['sectionCode']),
          );
        if (!section) {
          problem(
            'sectionCode',
            `Section "${row['sectionCode']}" does not exist in department ${department.name}.`,
          );
        } else result.sectionId = section.id;
      }
      const grade = grades.records().find((g) => lower(g.code) === lower(row['jobGradeCode']));
      if (!grade) problem('jobGradeCode', `Job grade "${row['jobGradeCode']}" does not exist.`);
      else result.jobGradeId = grade.id;
      if (present(row['reportsToCode'])) {
        const boss = positions.records().find((p) => lower(p.code) === lower(row['reportsToCode']));
        if (!boss) problem('reportsToCode', `Position "${row['reportsToCode']}" does not exist.`);
        else result.reportsToPositionId = boss.id;
      }
      return result;
    },
    record: (p) => ({
      ...p,
      departmentCode: departments.findById(p.departmentId)?.code ?? '',
      sectionCode: p.sectionId ? (sections.findById(p.sectionId)?.code ?? '') : '',
      jobGradeCode: grades.findById(p.jobGradeId)?.code ?? '',
      reportsToCode: p.reportsToPositionId
        ? (positions.findById(p.reportsToPositionId)?.code ?? '')
        : '',
      department: departments.findById(p.departmentId)?.name ?? '',
      jobGrade: grades.findById(p.jobGradeId)?.code ?? '',
    }),
    referenceSheets: () => [
      departmentCodesSheet(departments),
      {
        name: 'Grade codes',
        columns: [sheetColumn('code', 'Job Grade Code', 16), sheetColumn('name', 'Job Grade', 30)],
        rows: grades.records().map((g) => ({ code: g.code, name: g.name })),
      },
    ],
  });
}

export const ORGANIZATION_EXCEL_REGISTRATIONS: readonly (() => ExcelModuleRegistration)[] = [
  companyExcelRegistration,
  branchExcelRegistration,
  departmentExcelRegistration,
  sectionExcelRegistration,
  positionExcelRegistration,
  jobGradeExcelRegistration,
];
