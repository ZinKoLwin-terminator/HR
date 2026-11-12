import { inject } from '@angular/core';
import { ExcelColumn, ExcelModuleInfo, ImportIssue } from '../../../core/models/excel.models';
import { fullName } from '../../../core/models/employee.models';
import { SalaryAssignment } from '../../../core/models/payroll.models';
import { isDateInRange } from '../../../shared/utilities/date.util';
import {
  AttendanceRecordMockService,
  ShiftMockService,
} from '../../attendance/attendance-mock.services';
import { EmploymentHistoryMockService } from '../../employees/employee-extras-mock.services';
import { EmployeeMockService } from '../../employees/employee-mock.service';
import {
  LeaveApplicationMockService,
  LeaveBalanceMockService,
  LeaveTypeMockService,
} from '../../leave/leave-mock.services';
import {
  DepartmentMockService,
  JobGradeMockService,
} from '../../organization/organization-mock.services';
import {
  SalaryAssignmentMockService,
  SalaryStructureMockService,
} from '../../payroll/payroll-setup-mock.services';
import { mockError } from '../../mock-response';
import { ParsedRow } from '../excel-coercion';
import {
  ExcelExportAdapter,
  ExcelImportAdapter,
  ExcelModuleRegistration,
  ExportRecord,
  ExportSheet,
} from '../excel-module';
import {
  column,
  employeeCodeColumn,
  lower,
  outColumn,
  present,
  referenceColumn,
} from './excel-columns';

/**
 * Excel modules of the leave, attendance and payroll data, mirroring the API's modules:
 * leave applications, leave balances and attendance records are export only; shift assignments and
 * salary assignments can also be imported, through the mock services' own rules. The figures are
 * demonstration figures.
 */

/** A module that only exports: it has no import columns and no match key. */
function exportOnlyInfo(
  code: string,
  label: string,
  description: string,
  exportColumns: readonly ExcelColumn[],
  exportDateLabel?: string,
): ExcelModuleInfo {
  return {
    code,
    label,
    sheetName: label,
    description,
    columns: [],
    exportColumns,
    matchKey: [],
    matchKeyLabel: '',
    canImport: false,
    canExport: true,
    exportDateLabel,
  };
}

const sheetColumn = (key: string, header: string, width: number): ExcelColumn => ({
  key,
  header,
  type: 'text',
  width,
  description: '',
  required: false,
  example: null,
});

// ---- Leave applications (export only) -----------------------------------------------------

export const LEAVE_APPLICATION_MODULE_INFO = exportOnlyInfo(
  'leave-applications',
  'Leave applications',
  'Export leave applications as on the Leave screen. Import is not available: it would bypass the submit and approve workflow.',
  [
    outColumn('applicationNo', 'Application No', 'text', 16),
    outColumn('employeeCode', 'Employee Code', 'text', 14),
    outColumn('employeeName', 'Employee Name', 'text', 26),
    outColumn('department', 'Department', 'text', 24),
    outColumn('leaveTypeCode', 'Leave Type Code', 'text', 16),
    outColumn('leaveType', 'Leave Type', 'text', 22),
    outColumn('startDate', 'Start Date', 'date'),
    outColumn('endDate', 'End Date', 'date'),
    outColumn('days', 'Days', 'decimal'),
    outColumn('halfDay', 'Half Day', 'boolean'),
    outColumn('reason', 'Reason', 'text', 36),
    outColumn('status', 'Status'),
    outColumn('appliedOn', 'Applied On', 'date'),
    outColumn('actionedBy', 'Actioned By', 'text', 24),
    outColumn('actionedOn', 'Actioned On', 'date'),
    outColumn('approverRemarks', 'Approver Remarks', 'text', 30),
    outColumn('documentName', 'Supporting Document', 'text', 26),
  ],
  'Leave dates',
);

export function leaveApplicationExcelRegistration(): ExcelModuleRegistration {
  const applications = inject(LeaveApplicationMockService);
  const employees = inject(EmployeeMockService);
  const departments = inject(DepartmentMockService);
  const types = inject(LeaveTypeMockService);

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filtered = request.scope === 'filtered';
      const filters: Record<string, unknown> = filtered ? { ...request.filters } : {};
      // The date range of the dialog wins over the one of the list (they overlap the leave dates).
      if (request.dateFrom) filters['dateFrom'] = request.dateFrom;
      if (request.dateTo) filters['dateTo'] = request.dateTo;
      let items = applications.filterNow(filtered ? request.search : undefined, filters);
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      return items
        .sort((a, b) => a.applicationNo.localeCompare(b.applicationNo))
        .slice(0, limit + 1)
        .map((a): ExportRecord => {
          const person = employees.findById(a.employeeId);
          const type = types.findById(a.leaveTypeId);
          const approver = a.approverId ? employees.findById(a.approverId) : undefined;
          return {
            applicationNo: a.applicationNo,
            employeeCode: person?.employeeCode ?? '',
            employeeName: person ? fullName(person) : '',
            department: person ? (departments.findById(person.departmentId)?.name ?? '') : '',
            leaveTypeCode: type?.code ?? '',
            leaveType: type?.name ?? '',
            startDate: a.startDate,
            endDate: a.endDate,
            days: a.days,
            halfDay: a.halfDay,
            reason: a.reason,
            status: a.status,
            appliedOn: a.appliedOn,
            actionedBy: approver ? fullName(approver) : '',
            actionedOn: a.actionedOn,
            approverRemarks: a.approverRemarks,
            documentName: a.documentName,
          };
        });
    },
  };
  return { info: LEAVE_APPLICATION_MODULE_INFO, exportAdapter };
}

// ---- Leave balances (export only) ---------------------------------------------------------

export const LEAVE_BALANCE_MODULE_INFO = exportOnlyInfo(
  'leave-balances',
  'Leave balances',
  'Export each employee’s leave entitlement, days taken, pending days and available days for one year (the filter "year", the year of the start date, or the current year). Balances are worked out, not stored, so import is not available.',
  [
    outColumn('employeeCode', 'Employee Code', 'text', 14),
    outColumn('employeeName', 'Employee Name', 'text', 26),
    outColumn('department', 'Department', 'text', 24),
    outColumn('year', 'Year', 'integer'),
    outColumn('leaveTypeCode', 'Leave Type Code', 'text', 16),
    outColumn('leaveType', 'Leave Type', 'text', 22),
    outColumn('entitled', 'Entitled', 'decimal'),
    outColumn('taken', 'Taken', 'decimal'),
    outColumn('pending', 'Pending', 'decimal'),
    outColumn('available', 'Available', 'decimal'),
  ],
);

const FIRST_YEAR = 2000;
const LAST_YEAR = 2100;

export function leaveBalanceExcelRegistration(): ExcelModuleRegistration {
  const balances = inject(LeaveBalanceMockService);

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filters = request.scope === 'filtered' ? (request.filters ?? {}) : {};
      let year = new Date().getFullYear();
      if (present(filters['year'])) year = Number(filters['year']);
      else if (request.dateFrom) year = Number(request.dateFrom.slice(0, 4));
      if (!Number.isInteger(year) || year < FIRST_YEAR || year > LAST_YEAR) {
        const message = `Year must be between ${FIRST_YEAR} and ${LAST_YEAR}.`;
        throw mockError('Validation', message, { year: message });
      }
      const idOf = (value: unknown) => (present(value) ? Number(value) : null);
      let people = balances.rowsNow({
        year,
        search: request.scope === 'filtered' ? (request.search ?? '') : '',
        departmentId: idOf(filters['departmentId']),
        branchId: idOf(filters['branchId']),
      });
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        people = people.filter((p) => ids.has(p.employeeId));
      }
      const rows: ExportRecord[] = [];
      for (const person of people.sort((a, b) => a.employeeCode.localeCompare(b.employeeCode))) {
        for (const line of person.balances) {
          rows.push({
            employeeCode: person.employeeCode,
            employeeName: person.employeeName,
            department: person.departmentName,
            year,
            leaveTypeCode: line.code,
            leaveType: line.name,
            entitled: line.entitled,
            taken: line.taken,
            pending: line.pending,
            available: line.available,
          });
          if (rows.length > limit) return rows;
        }
      }
      return rows;
    },
  };
  return { info: LEAVE_BALANCE_MODULE_INFO, exportAdapter };
}

// ---- Attendance records (export only) -----------------------------------------------------

export const ATTENDANCE_RECORD_MODULE_INFO = exportOnlyInfo(
  'attendance-records',
  'Attendance records',
  'Export attendance records as on the Attendance screen. Import is not available: records come from punches and approved adjustments.',
  [
    outColumn('date', 'Date', 'date'),
    outColumn('employeeCode', 'Employee Code', 'text', 14),
    outColumn('employeeName', 'Employee Name', 'text', 26),
    outColumn('department', 'Department', 'text', 24),
    outColumn('branch', 'Branch', 'text', 22),
    outColumn('shift', 'Shift', 'text', 18),
    outColumn('checkIn', 'Check In'),
    outColumn('checkOut', 'Check Out'),
    outColumn('workedMinutes', 'Worked Minutes', 'integer'),
    outColumn('lateMinutes', 'Late Minutes', 'integer'),
    outColumn('earlyLeaveMinutes', 'Early Leave Minutes', 'integer'),
    outColumn('overtimeMinutes', 'Overtime Minutes', 'integer'),
    outColumn('status', 'Status'),
    outColumn('source', 'Source'),
    outColumn('remarks', 'Remarks', 'text', 30),
  ],
  'Date',
);

export function attendanceRecordExcelRegistration(): ExcelModuleRegistration {
  const records = inject(AttendanceRecordMockService);

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filtered = request.scope === 'filtered';
      const filters: Record<string, unknown> = filtered ? { ...request.filters } : {};
      if (request.dateFrom) filters['dateFrom'] = request.dateFrom;
      if (request.dateTo) filters['dateTo'] = request.dateTo;
      let items = records.filterNow(filtered ? request.search : undefined, filters);
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      const none = (value: string) => (value === '—' ? '' : value);
      return items
        .sort(
          (a, b) => b.date.localeCompare(a.date) || a.employeeCode.localeCompare(b.employeeCode),
        )
        .slice(0, limit + 1)
        .map((r): ExportRecord => ({
          date: r.date,
          employeeCode: none(r.employeeCode),
          employeeName: r.employeeName,
          department: none(r.departmentName),
          branch: none(r.branchName),
          shift: none(r.shiftName),
          checkIn: r.checkIn,
          checkOut: r.checkOut,
          workedMinutes: r.workedMinutes,
          lateMinutes: r.lateMinutes,
          earlyLeaveMinutes: r.earlyLeaveMinutes,
          overtimeMinutes: r.overtimeMinutes,
          status: r.status,
          source: r.source,
          remarks: r.remarks,
        }));
    },
  };
  return { info: ATTENDANCE_RECORD_MODULE_INFO, exportAdapter };
}

// ---- Shift assignments --------------------------------------------------------------------

const SHIFT_COLUMNS: readonly ExcelColumn[] = [
  employeeCodeColumn('Code of the employee. Existing employees are matched by this code.'),
  referenceColumn(
    'shiftCode',
    'Shift Code',
    'Code of the work shift to assign (see the "Shift codes" sheet).',
    'GEN',
  ),
];

export const SHIFT_ASSIGNMENT_MODULE_INFO: ExcelModuleInfo = {
  code: 'shift-assignments',
  label: 'Shift assignments',
  sheetName: 'Shift assignments',
  description:
    'Assign work shifts to employees. Each row sets the shift of the employee with that Employee Code.',
  columns: SHIFT_COLUMNS,
  exportColumns: [
    outColumn('employeeCode', 'Employee Code', 'text', 14),
    outColumn('employeeName', 'Employee Name', 'text', 26),
    outColumn('department', 'Department', 'text', 24),
    outColumn('branch', 'Branch', 'text', 22),
    outColumn('employmentStatus', 'Employment Status'),
    outColumn('shiftCode', 'Shift Code', 'text', 14),
    outColumn('shift', 'Shift', 'text', 20),
  ],
  matchKey: ['employeeCode'],
  matchKeyLabel: 'Employee Code',
  canImport: true,
  canExport: true,
};

export function shiftAssignmentExcelRegistration(): ExcelModuleRegistration {
  const employees = inject(EmployeeMockService);
  const shifts = inject(ShiftMockService);
  const history = inject(EmploymentHistoryMockService);

  const employeeByCode = (value: unknown) =>
    employees.records().find((e) => lower(e.employeeCode) === lower(value));
  const headerOf = (key: string) => SHIFT_COLUMNS.find((c) => c.key === key)?.header ?? key;

  const importAdapter: ExcelImportAdapter = {
    // An employee who already has a shift counts as an existing assignment.
    findExisting: (row: ParsedRow) => {
      const employee = employeeByCode(row['employeeCode']);
      return employee && employee.shiftId !== null ? employee.id : null;
    },
    columnForField: (field) => (field === 'shiftId' ? 'shiftCode' : undefined),
    toModel: (row) => {
      const issues: ImportIssue[] = [];
      const problem = (key: string, message: string) =>
        issues.push({
          field: headerOf(key),
          value: String(row[key] ?? ''),
          message,
          severity: 'Error',
        });
      const employee = employeeByCode(row['employeeCode']);
      if (!employee) problem('employeeCode', `Employee "${row['employeeCode']}" does not exist.`);
      const shift = shifts.records().find((s) => lower(s.code) === lower(row['shiftCode']));
      if (!shift) problem('shiftCode', `Shift "${row['shiftCode']}" does not exist.`);
      if (!employee || !shift) return { model: null, issues };
      return { model: { employeeId: employee.id, shiftId: shift.id }, issues: [] };
    },
    // The employee is updated with the same rules as the employee form (only the shift changes).
    dryRun: (model) =>
      employees.dryRun({ shiftId: model['shiftId'] } as never, Number(model['employeeId'])),
    apply: (model) => {
      employees.updateNow(
        Number(model['employeeId']),
        { shiftId: model['shiftId'] } as never,
        false,
      );
    },
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
        filtered ? (request.filters ?? {}) : undefined,
      );
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      return items
        .sort((a, b) => a.employeeCode.localeCompare(b.employeeCode))
        .slice(0, limit + 1)
        .map((item): ExportRecord => {
          const shift = item.shiftId ? shifts.findById(item.shiftId) : undefined;
          return {
            employeeCode: item.employeeCode,
            employeeName: item.fullName,
            department: item.departmentName,
            branch: item.branchName,
            employmentStatus: item.employmentStatus,
            shiftCode: shift?.code ?? '',
            shift: shift?.name ?? '',
          };
        });
    },
  };

  const referenceSheets = (): ExportSheet[] => [
    {
      name: 'Shift codes',
      columns: [
        sheetColumn('code', 'Shift Code', 14),
        sheetColumn('name', 'Shift', 28),
        sheetColumn('hours', 'Hours', 14),
      ],
      rows: shifts
        .records()
        .map((s) => ({ code: s.code, name: s.name, hours: `${s.startTime}-${s.endTime}` })),
    },
  ];

  return { info: SHIFT_ASSIGNMENT_MODULE_INFO, importAdapter, exportAdapter, referenceSheets };
}

// ---- Salary assignments -------------------------------------------------------------------

const SALARY_COLUMNS: readonly ExcelColumn[] = [
  employeeCodeColumn('Code of the employee. Part of the key that matches an existing assignment.'),
  column(
    'effectiveFrom',
    'Effective From',
    'First day the salary applies, not before the joining date. Part of the matching key.',
    '2026-01-01',
    { required: true, type: 'date' },
  ),
  referenceColumn(
    'structureCode',
    'Salary Structure Code',
    'Code of an active salary structure.',
    'STAFF',
  ),
  column(
    'basicSalary',
    'Basic Salary',
    'Monthly basic salary (sample data), inside the band of the employee’s job grade.',
    1500,
    { required: true, type: 'decimal', min: 0, decimals: 2, sensitive: true },
  ),
  column('remarks', 'Remarks', 'Note about the change (optional).', 'Annual review', {
    maxLength: 250,
  }),
];

export const SALARY_ASSIGNMENT_MODULE_INFO: ExcelModuleInfo = {
  code: 'salary-assignments',
  label: 'Salary assignments',
  sheetName: 'Salary assignments',
  description:
    'Import and export salary assignments (basic salary by employee and start date). The basic salary must be inside the employee’s job grade band and an employee has one assignment per start date. Figures are demonstration figures.',
  columns: SALARY_COLUMNS,
  exportColumns: [
    outColumn('employeeCode', 'Employee Code', 'text', 14),
    outColumn('employeeName', 'Employee Name', 'text', 26),
    outColumn('jobGrade', 'Job Grade'),
    outColumn('structureCode', 'Salary Structure Code', 'text', 18),
    outColumn('structure', 'Salary Structure', 'text', 24),
    outColumn('effectiveFrom', 'Effective From', 'date'),
    outColumn('basicSalary', 'Basic Salary', 'decimal'),
    outColumn('remarks', 'Remarks', 'text', 30),
  ],
  matchKey: ['employeeCode', 'effectiveFrom'],
  matchKeyLabel: 'Employee Code + Effective From',
  canImport: true,
  canExport: true,
  exportDateLabel: 'Effective date',
};

export function salaryAssignmentExcelRegistration(): ExcelModuleRegistration {
  const assignments = inject(SalaryAssignmentMockService);
  const structures = inject(SalaryStructureMockService);
  const employees = inject(EmployeeMockService);
  const grades = inject(JobGradeMockService);

  const employeeByCode = (value: unknown) =>
    employees.records().find((e) => lower(e.employeeCode) === lower(value));
  const headerOf = (key: string) => SALARY_COLUMNS.find((c) => c.key === key)?.header ?? key;
  type Model = Omit<SalaryAssignment, 'id'>;

  const importAdapter: ExcelImportAdapter = {
    findExisting: (row) => {
      const employee = employeeByCode(row['employeeCode']);
      if (!employee || !present(row['effectiveFrom'])) return null;
      return (
        assignments
          .records()
          .find(
            (a) => a.employeeId === employee.id && a.effectiveFrom === String(row['effectiveFrom']),
          )?.id ?? null
      );
    },
    columnForField: (field) =>
      ({ employeeId: 'employeeCode', structureId: 'structureCode' })[field] ?? field,
    toModel: (row, existingId) => {
      const issues: ImportIssue[] = [];
      const problem = (key: string, message: string) =>
        issues.push({
          field: headerOf(key),
          value: String(row[key] ?? ''),
          message,
          severity: 'Error',
        });
      const employee = employeeByCode(row['employeeCode']);
      if (!employee) problem('employeeCode', `Employee "${row['employeeCode']}" does not exist.`);
      const structure = structures
        .records()
        .find((s) => lower(s.code) === lower(row['structureCode']));
      if (!structure) {
        problem('structureCode', `Salary structure "${row['structureCode']}" does not exist.`);
      }
      if (!employee || !structure) return { model: null, issues };
      const current = existingId === null ? undefined : assignments.findById(existingId);
      const model: Model = {
        employeeId: employee.id,
        structureId: structure.id,
        basicSalary: Number(row['basicSalary']),
        effectiveFrom: String(row['effectiveFrom']),
        // A blank remark keeps the current one.
        remarks: present(row['remarks']) ? String(row['remarks']) : (current?.remarks ?? ''),
      };
      return { model: model as unknown as Record<string, unknown>, issues: [] };
    },
    dryRun: (model, existingId) => assignments.dryRun(model as never, existingId),
    // Created without an activity entry: amounts never go to the activity feed.
    apply: (model, existingId) => {
      if (existingId === null) assignments.createNow(model as never, false);
      else assignments.updateNow(existingId, model as never, false);
    },
    snapshot: () => assignments.snapshot(),
  };

  const exportAdapter: ExcelExportAdapter = {
    rows: (request, limit) => {
      const filtered = request.scope === 'filtered';
      let items = assignments.filterNow(
        filtered ? request.search : undefined,
        filtered ? (request.filters ?? {}) : undefined,
      );
      if (request.scope === 'selected') {
        const ids = new Set(request.selectedIds ?? []);
        items = items.filter((i) => ids.has(i.id));
      }
      return items
        .filter((a) => isDateInRange(a.effectiveFrom, request.dateFrom, request.dateTo))
        .sort(
          (a, b) =>
            (employees.findById(a.employeeId)?.employeeCode ?? '').localeCompare(
              employees.findById(b.employeeId)?.employeeCode ?? '',
            ) || b.effectiveFrom.localeCompare(a.effectiveFrom),
        )
        .slice(0, limit + 1)
        .map((a): ExportRecord => {
          const person = employees.findById(a.employeeId);
          const structure = structures.findById(a.structureId);
          return {
            employeeCode: person?.employeeCode ?? '',
            employeeName: person ? fullName(person) : '',
            jobGrade: person ? (grades.findById(person.jobGradeId)?.code ?? '') : '',
            structureCode: structure?.code ?? '',
            structure: structure?.name ?? '',
            effectiveFrom: a.effectiveFrom,
            basicSalary: a.basicSalary,
            remarks: a.remarks,
          };
        });
    },
  };

  const referenceSheets = (): ExportSheet[] => [
    {
      name: 'Structure codes',
      columns: [
        sheetColumn('code', 'Salary Structure Code', 22),
        sheetColumn('name', 'Salary Structure', 30),
      ],
      rows: structures
        .records()
        .filter((s) => s.status === 'Active')
        .map((s) => ({ code: s.code, name: s.name })),
    },
  ];

  return { info: SALARY_ASSIGNMENT_MODULE_INFO, importAdapter, exportAdapter, referenceSheets };
}

export const OPERATIONS_EXCEL_REGISTRATIONS: readonly (() => ExcelModuleRegistration)[] = [
  leaveApplicationExcelRegistration,
  leaveBalanceExcelRegistration,
  attendanceRecordExcelRegistration,
  shiftAssignmentExcelRegistration,
  salaryAssignmentExcelRegistration,
];
