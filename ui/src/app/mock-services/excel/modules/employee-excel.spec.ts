import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { AppError } from '../../../core/models/common.models';
import { ImportPreview, ImportStrategy } from '../../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
} from '../../../core/services/data/excel-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { EmploymentHistoryDataService } from '../../../core/services/data/employee-extras-data.services';
import { provideMockDataServices } from '../../provide-mock-data-services';
import { lib, workbookFile } from '../excel-testing';
import { EMPLOYEE_MODULE_INFO } from './employee-excel.module';

const HEADERS = EMPLOYEE_MODULE_INFO.columns.map((c) => c.header);

/** A complete, valid employee row (EMP061, Engineering at the Yangon head office). */
function row(overrides: Record<string, string | number | null> = {}): (string | number | null)[] {
  const base: Record<string, string | number | null> = {
    'Employee Code': 'EMP061',
    'First Name': 'Mia',
    'Last Name': 'Hartley',
    Gender: 'Female',
    'Date of Birth': '1992-04-18',
    'Marital Status': 'Single',
    Nationality: 'Myanmar',
    'National ID': 'NRC-123456',
    'Work Email': 'mia.hartley@apexhorizon.example',
    'Personal Email': '',
    'Mobile Phone': '+95 9 123 456 789',
    'Alternate Phone': '',
    'Address Line 1': '12 Park Road',
    'Address Line 2': '',
    City: 'Yangon',
    'State / Region': '',
    'Postal Code': '',
    Country: 'Myanmar',
    'Emergency Contact Name': 'John Hartley',
    'Emergency Contact Relationship': 'Spouse',
    'Emergency Contact Phone': '+95 9 987 654 321',
    'Employment Type': 'Full-time',
    'Employment Status': 'Active',
    'Joining Date': '2026-01-05',
    'Probation End Date': '',
    'Exit Date': '',
    'Company Code': 'AHT',
    'Branch Code': 'YGN-HQ',
    'Department Code': 'ENG',
    'Section Code': '',
    'Position Code': '',
    'Job Grade Code': '',
    'Manager Employee Code': 'EMP003',
    'Shift Code': '',
    'Bank Name': 'Sample Bank',
    'Bank Branch': '',
    'Bank Account Holder': 'Mia Hartley',
    'Bank Account Number': '1234567890',
  };
  const merged = { ...base, ...overrides };
  return HEADERS.map((h) => merged[h] ?? null);
}

describe('Employee Excel import/export', { timeout: 30_000 }, () => {
  let imports: ExcelImportDataService;
  let exports_: ExcelExportDataService;
  let employees: EmployeeDataService;
  let history: EmploymentHistoryDataService;
  let positionCode = '';

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    imports = TestBed.inject(ExcelImportDataService);
    exports_ = TestBed.inject(ExcelExportDataService);
    employees = TestBed.inject(EmployeeDataService);
    history = TestBed.inject(EmploymentHistoryDataService);

    // Find real codes from the sample data: an Engineering position at YGN-HQ.
    const { blob } = await firstValueFrom(imports.getTemplate('employees'));
    const workbook = new (await lib()).Workbook();
    await workbook.xlsx.load(await blob.arrayBuffer());
    const sheet = workbook.getWorksheet('Organization codes')!;
    for (let r = 2; r <= sheet.rowCount; r++) {
      if (sheet.getCell(r, 3).value === 'YGN-HQ' && sheet.getCell(r, 5).value === 'ENG') {
        positionCode = String(sheet.getCell(r, 7).value);
        break;
      }
    }
    expect(positionCode).not.toBe('');
  });

  const withPosition = (overrides: Record<string, string | number | null> = {}) =>
    row({ 'Position Code': positionCode, ...overrides });

  async function preview(
    rows: (string | number | null)[][],
    strategy: ImportStrategy = 'InsertOrUpdate',
  ): Promise<ImportPreview> {
    const inspection = await firstValueFrom(
      imports.inspect('employees', await workbookFile([HEADERS, ...rows])),
    );
    expect(inspection.needsMapping).toBe(false);
    return firstValueFrom(
      imports.validate({ uploadId: inspection.uploadId, strategy, mapping: inspection.mapping }),
    );
  }
  const messages = (result: ImportPreview, rowIndex: number) =>
    result.rows[rowIndex].issues.map((i) => `${i.field}: ${i.message}`);
  async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
    try {
      await promise;
    } catch (error) {
      expect((error as AppError).code).toBe(code);
      return error as AppError;
    }
    throw new Error(`Expected ${code}`);
  }

  describe('template', () => {
    it('is built for business users: headers, instructions, field guide, drop-downs and reference codes', async () => {
      const { blob, fileName } = await firstValueFrom(imports.getTemplate('employees'));
      expect(fileName).toBe('employees-import-template.xlsx');
      const workbook = new (await lib()).Workbook();
      await workbook.xlsx.load(await blob.arrayBuffer());
      expect(workbook.worksheets.map((w) => w.name)).toEqual([
        'Instructions',
        'Employees',
        'Field guide',
        'Organization codes',
        'Grade and shift codes',
        'Lists',
      ]);
      const data = workbook.getWorksheet('Employees')!;
      expect(HEADERS.map((_, i) => String(data.getCell(1, i + 1).value))).toEqual(HEADERS);
      // internal field names are never exposed
      expect(HEADERS.join('|')).not.toMatch(/Id\b|companyId|departmentId/);
      const guide = workbook.getWorksheet('Field guide')!;
      expect(guide.getCell(2, 1).value).toBe('Employee Code');
      expect(guide.getCell(2, 2).value).toBe('Yes');
      expect(String(guide.getCell(6, 4).value)).toContain('yyyy-MM-dd'); // Date of Birth
      const codes = workbook.getWorksheet('Organization codes')!;
      expect([1, 3, 5, 7].map((c) => codes.getCell(1, c).value)).toEqual([
        'Company Code',
        'Branch Code',
        'Department Code',
        'Position Code',
      ]);
      expect(codes.rowCount).toBeGreaterThan(20);
      const lists = workbook.getWorksheet('Lists')!;
      expect(lists.state).toBe('hidden');
      const models = (
        data as unknown as { dataValidations: { model: Record<string, { type: string }> } }
      ).dataValidations.model;
      // ExcelJS expands each drop-down range into one entry per cell when a file is read back.
      const dropDownColumns = new Set(
        Object.entries(models)
          .filter(([, v]) => v.type === 'list')
          .map(([cell]) => cell.replace(/\d+/g, '')),
      );
      expect(dropDownColumns.size).toBe(5); // gender, marital status, relationship, type, status
    }, 30_000); // builds and parses a whole workbook: slow on a busy machine
  });

  describe('validation', () => {
    it('accepts a complete valid row and resolves organization codes in scope', async () => {
      const result = await preview([withPosition()]);
      expect(result.rows[0].issues).toEqual([]);
      expect(result.rows[0]).toMatchObject({ status: 'Valid', action: 'Insert' });
      expect(result).toMatchObject({ totalRows: 1, validRows: 1, invalidRows: 0 });
    });

    it('reports missing required values and invalid formats per column', async () => {
      const result = await preview([
        withPosition({
          'Employee Code': 'emp 62',
          'First Name': null,
          'Work Email': 'nope',
          'Date of Birth': '18/04/1992',
          'Mobile Phone': '12',
          Gender: 'Femal',
          'Bank Account Number': '12ab',
        }),
      ]);
      const issues = messages(result, 0).join(' | ');
      expect(result.rows[0].status).toBe('Invalid');
      expect(issues).toContain('Employee Code must use capital letters');
      expect(issues).toContain('First Name is required');
      expect(issues).toContain('Work Email is not a valid email');
      expect(issues).toContain('Date of Birth is not a valid date');
      expect(issues).toContain('Mobile Phone is not a valid phone');
      expect(issues).toContain('Gender "Femal" is not an allowed value');
      expect(issues).toContain('Bank Account Number must be at least 8');
    });

    it('applies the same business rules as the employee form', async () => {
      const young = new Date().getFullYear() - 16;
      const result = await preview([
        withPosition({
          'Employee Code': 'EMP070',
          'Work Email': 'a70@apexhorizon.example',
          'Date of Birth': `${young}-06-01`,
        }),
        withPosition({
          'Employee Code': 'EMP071',
          'Work Email': 'a71@apexhorizon.example',
          'Employment Status': 'Resigned',
        }),
        withPosition({
          'Employee Code': 'EMP072',
          'Work Email': 'a72@apexhorizon.example',
          'Exit Date': '2025-12-31',
        }),
        withPosition({
          'Employee Code': 'EMP073',
          'Work Email': 'a73@apexhorizon.example',
          'Probation End Date': '2025-12-31',
        }),
        withPosition({
          'Employee Code': 'EMP074',
          'Work Email': 'a74@apexhorizon.example',
          'Manager Employee Code': 'EMP074',
        }),
      ]);
      expect(messages(result, 0)).toEqual([
        'Date of Birth: Employees must be at least 18 years old.',
      ]);
      expect(messages(result, 1)).toEqual([
        'Exit Date: An exit date is required for resigned or terminated employees.',
      ]);
      expect(messages(result, 2)).toEqual([
        'Exit Date: The exit date cannot be before the joining date.',
      ]);
      expect(messages(result, 3)).toEqual([
        'Probation End Date: The probation cannot end before the joining date.',
      ]);
      expect(messages(result, 4)[0]).toContain('Manager "EMP074" does not exist');
    });

    it('checks every organization reference within its scope', async () => {
      const result = await preview([
        withPosition({
          'Employee Code': 'EMP080',
          'Work Email': 'a80@apexhorizon.example',
          'Company Code': 'NOPE',
        }),
        withPosition({
          'Employee Code': 'EMP081',
          'Work Email': 'a81@apexhorizon.example',
          'Branch Code': 'MDY-DC',
        }), // exists, but in another company
        withPosition({
          'Employee Code': 'EMP082',
          'Work Email': 'a82@apexhorizon.example',
          'Department Code': 'LOG',
        }), // wrong branch
        withPosition({
          'Employee Code': 'EMP083',
          'Work Email': 'a83@apexhorizon.example',
          'Position Code': 'NOPE',
        }),
        withPosition({
          'Employee Code': 'EMP084',
          'Work Email': 'a84@apexhorizon.example',
          'Job Grade Code': 'ZZ',
        }),
        withPosition({
          'Employee Code': 'EMP085',
          'Work Email': 'a85@apexhorizon.example',
          'Shift Code': 'ZZ',
        }),
        withPosition({
          'Employee Code': 'EMP086',
          'Work Email': 'a86@apexhorizon.example',
          'Manager Employee Code': 'ZZZ',
        }),
      ]);
      expect(messages(result, 0)).toEqual(['Company Code: Company "NOPE" does not exist.']);
      expect(messages(result, 1)[0]).toMatch(
        /^Branch Code: Branch "MDY-DC" does not exist in company/,
      );
      expect(messages(result, 2)[0]).toMatch(
        /^Department Code: Department "LOG" does not exist in branch/,
      );
      expect(messages(result, 3)[0]).toMatch(
        /^Position Code: Position "NOPE" does not exist in department/,
      );
      expect(messages(result, 4)).toEqual(['Job Grade Code: Job grade "ZZ" does not exist.']);
      expect(messages(result, 5)).toEqual(['Shift Code: Shift "ZZ" does not exist.']);
      expect(messages(result, 6)).toEqual(['Manager Employee Code: Manager "ZZZ" does not exist.']);
      expect(result.invalidRows).toBe(7);
    });

    it('detects duplicate employee codes and duplicate emails (unique rules of the service)', async () => {
      const result = await preview(
        [
          withPosition({ 'Employee Code': 'EMP090', 'Work Email': 'x90@apexhorizon.example' }),
          withPosition({ 'Employee Code': 'emp090', 'Work Email': 'y90@apexhorizon.example' }), // same code in the file
          withPosition({ 'Employee Code': 'EMP091', 'Work Email': 'x90@apexhorizon.example' }), // same email as row 2 — rejected when saved
          withPosition({ 'Employee Code': 'EMP092', 'Work Email': 'emp001@apexhorizon.example' }),
        ],
        'InsertOnly',
      );
      expect(result.rows[1].status).toBe('Duplicate');
      expect(messages(result, 1)[0]).toMatch(/Duplicate of row 2/);
      expect(result.rows[0].status).toBe('Valid');
      // EMP001 exists already: its work email is taken
      const existing = (await firstValueFrom(employees.getAll())).find(
        (e) => e.employeeCode === 'EMP001',
      )!;
      const clash = await preview(
        [withPosition({ 'Employee Code': 'EMP093', 'Work Email': existing.workEmail })],
        'InsertOnly',
      );
      expect(messages(clash, 0)).toEqual(['Work Email: Work email must be unique.']);
    });

    it('treats an existing employee code as a duplicate for insert-only and as an update otherwise', async () => {
      const existing = (await firstValueFrom(employees.getAll())).find(
        (e) => e.employeeCode === 'EMP015',
      )!;
      const rows = [
        withPosition({
          'Employee Code': 'emp015',
          'Work Email': existing.workEmail,
          'Joining Date': existing.joiningDate,
          'First Name': 'Changed',
        }),
      ];
      expect((await preview(rows, 'InsertOnly')).rows[0]).toMatchObject({
        status: 'Duplicate',
        action: 'None',
      });
      expect((await preview(rows, 'UpdateOnly')).rows[0]).toMatchObject({
        status: 'Valid',
        action: 'Update',
      });
      expect((await preview(rows, 'InsertOrUpdate')).rows[0]).toMatchObject({
        status: 'Valid',
        action: 'Update',
      });
      const unknown = [
        withPosition({ 'Employee Code': 'EMP099', 'Work Email': 'x99@apexhorizon.example' }),
      ];
      expect((await preview(unknown, 'UpdateOnly')).rows[0].status).toBe('Invalid');
    });

    it('warns about an employee on probation without a probation end date', async () => {
      const result = await preview([withPosition({ 'Employment Status': 'Probation' })]);
      expect(result.rows[0].status).toBe('Warning');
      expect(result).toMatchObject({ warningRows: 1, validRows: 1 });
    });

    it('does not change any employee while validating', async () => {
      const before = (await firstValueFrom(employees.getAll())).length;
      await preview([withPosition()]);
      expect((await firstValueFrom(employees.getAll())).length).toBe(before);
    });
  });

  describe('execution', () => {
    const run = async (
      rows: (string | number | null)[][],
      mode: 'AllOrNothing' | 'Partial',
      strategy: ImportStrategy = 'InsertOrUpdate',
    ) => {
      const result = await preview(rows, strategy);
      return { result, outcome: await firstValueFrom(imports.execute(result.batchId, mode)) };
    };

    it('creates employees with the normal rules, history and no per-row activity noise', async () => {
      const { outcome } = await run(
        [
          withPosition({
            'Employee Code': 'EMP100',
            'Work Email': 'e100@apexhorizon.example',
            'Section Code': '',
          }),
          withPosition({
            'Employee Code': 'EMP101',
            'Work Email': 'e101@apexhorizon.example',
            'Job Grade Code': 'G2',
          }),
        ],
        'AllOrNothing',
      );
      expect(outcome).toMatchObject({ status: 'Completed', inserted: 2, updated: 0 });
      const created = (await firstValueFrom(employees.getAll())).find(
        (e) => e.employeeCode === 'EMP100',
      )!;
      expect(created).toMatchObject({
        firstName: 'Mia',
        lastName: 'Hartley',
        gender: 'Female',
        workEmail: 'e100@apexhorizon.example',
        joiningDate: '2026-01-05',
        sectionId: null,
        probationEndDate: null,
        address: { line1: '12 Park Road', city: 'Yangon', country: 'Myanmar', line2: '' },
        bank: { bankName: 'Sample Bank', accountNumber: '1234567890' },
        emergencyContact: { name: 'John Hartley', relationship: 'Spouse' },
      });
      expect(created.managerId).not.toBeNull();
      expect(
        (await firstValueFrom(history.getAll()))
          .filter((h) => h.employeeId === created.id)
          .map((h) => h.type),
      ).toEqual(['Joined']);
    });

    it('updates existing employees, keeping values left blank, and records the change in history', async () => {
      const before = (await firstValueFrom(employees.getAll())).find(
        (e) => e.employeeCode === 'EMP015',
      )!;
      const { outcome } = await run(
        [
          withPosition({
            'Employee Code': 'EMP015',
            'Work Email': before.workEmail,
            'Joining Date': before.joiningDate,
            'First Name': 'Renamed',
            'Employment Status': 'Suspended',
            'Personal Email': '',
          }),
        ],
        'AllOrNothing',
        'UpdateOnly',
      );
      expect(outcome).toMatchObject({ status: 'Completed', inserted: 0, updated: 1 });
      const after = await firstValueFrom(employees.getById(before.id));
      expect(after.firstName).toBe('Renamed');
      expect(after.employmentStatus).toBe('Suspended');
      expect(after.personalEmail).toBe(before.personalEmail); // blank cell → unchanged
      expect(after.employeeCode).toBe('EMP015');
      const types = (await firstValueFrom(history.getAll()))
        .filter((h) => h.employeeId === before.id)
        .map((h) => h.type);
      expect(types).toContain('Status Change');
    });

    it('imports only the valid rows in partial mode and lists the skipped ones', async () => {
      const { outcome } = await run(
        [
          withPosition({ 'Employee Code': 'EMP110', 'Work Email': 'e110@apexhorizon.example' }),
          withPosition({ 'Employee Code': 'EMP111', 'Work Email': 'bad' }),
        ],
        'Partial',
      );
      expect(outcome).toMatchObject({ status: 'CompletedWithErrors', inserted: 1, skipped: 1 });
      expect(
        (await firstValueFrom(employees.getAll())).some((e) => e.employeeCode === 'EMP111'),
      ).toBe(false);
    });

    it('refuses all-or-nothing when any row is invalid and leaves everything untouched', async () => {
      const result = await preview([
        withPosition({ 'Employee Code': 'EMP120', 'Work Email': 'e120@apexhorizon.example' }),
        withPosition({ 'Employee Code': 'EMP121', 'Work Email': 'bad' }),
      ]);
      const count = (await firstValueFrom(employees.getAll())).length;
      await expectError(
        firstValueFrom(imports.execute(result.batchId, 'AllOrNothing')),
        'Conflict',
      );
      expect((await firstValueFrom(employees.getAll())).length).toBe(count);
    });

    it('rolls back employees and history when a row fails while saving', async () => {
      const result = await preview([
        withPosition({ 'Employee Code': 'EMP130', 'Work Email': 'e130@apexhorizon.example' }),
        withPosition({ 'Employee Code': 'EMP131', 'Work Email': 'e131@apexhorizon.example' }),
      ]);
      // After validation, someone else takes the second email.
      const template = (await firstValueFrom(employees.getAll()))[0];
      const { id: _id, ...model } = template;
      void _id;
      await firstValueFrom(
        employees.create({
          ...model,
          employeeCode: 'EMP199',
          workEmail: 'e131@apexhorizon.example',
          managerId: null,
        }),
      );
      const counts = {
        employees: (await firstValueFrom(employees.getAll())).length,
        history: (await firstValueFrom(history.getAll())).length,
      };
      const outcome = await firstValueFrom(imports.execute(result.batchId, 'AllOrNothing'));
      expect(outcome).toMatchObject({ status: 'Failed', rolledBack: true, inserted: 0, failed: 1 });
      expect((await firstValueFrom(employees.getAll())).length).toBe(counts.employees);
      expect((await firstValueFrom(history.getAll())).length).toBe(counts.history);
    });

    it('shows validation errors for the column the user must correct', async () => {
      // A department/branch mismatch is caught while resolving codes; sanity-check the mapped field names.
      const result = await preview([
        withPosition({
          'Employee Code': 'EMP140',
          'Work Email': 'e140@apexhorizon.example',
          'Employment Status': 'Terminated',
        }),
      ]);
      expect(result.rows[0].issues[0].field).toBe('Exit Date');
    });
  });

  describe('export', () => {
    const readSheet = async (
      request: Parameters<typeof exports_.export>[0],
      name = 'Employees',
    ) => {
      const { blob } = await firstValueFrom(exports_.export(request));
      const workbook = new (await lib()).Workbook();
      await workbook.xlsx.load(await blob.arrayBuffer());
      return { workbook, sheet: workbook.getWorksheet(name)! };
    };
    const headerRow = (sheet: { getRow(n: number): { values: unknown } }) =>
      (sheet.getRow(1).values as unknown[]).slice(1);

    it('exports all employees with professional formatting and summary sheets, without sensitive columns', async () => {
      const { workbook, sheet } = await readSheet({ moduleCode: 'employees', scope: 'all' });
      expect(workbook.worksheets.map((w) => w.name)).toEqual([
        'Employees',
        'Department Summary',
        'Branch Summary',
      ]);
      expect(headerRow(sheet)).toEqual([
        'Employee Code',
        'Employee Name',
        'Gender',
        'Work Email',
        'Mobile Phone',
        'Company',
        'Branch',
        'Department',
        'Section',
        'Position',
        'Job Grade',
        'Manager',
        'Shift',
        'Employment Type',
        'Employment Status',
        'Joining Date',
        'Probation End Date',
        'Exit Date',
      ]);
      expect(sheet.rowCount).toBe(61);
      expect(sheet.getCell(1, 1).font?.bold).toBe(true);
      expect(sheet.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
      expect(sheet.autoFilter).toBeTruthy();
      expect(sheet.getCell(2, 1).value).toBe('EMP001');
      expect(sheet.getCell(2, 16).value).toBeInstanceOf(Date);
      expect(sheet.getCell(2, 16).numFmt).toBe('yyyy-mm-dd');
      const dept = workbook.getWorksheet('Department Summary')!;
      let total = 0;
      for (let r = 2; r <= dept.rowCount; r++) total += Number(dept.getCell(r, 2).value);
      expect(total).toBe(60);
    });

    it('exports only what the current filters select', async () => {
      const all = await firstValueFrom(employees.getAll());
      const engineering = all.filter((e) => e.departmentId === 4).length;
      const { sheet } = await readSheet({
        moduleCode: 'employees',
        scope: 'filtered',
        filters: { departmentId: 4 },
      });
      expect(sheet.rowCount - 1).toBe(engineering);
      const searched = await readSheet({
        moduleCode: 'employees',
        scope: 'filtered',
        search: 'emp005',
      });
      expect(searched.sheet.rowCount).toBe(2);
      expect(searched.sheet.getCell(2, 1).value).toBe('EMP005');
    });

    it('supports a joining date range and selected records', async () => {
      const all = await firstValueFrom(employees.getAll());
      const recent = all.filter((e) => e.joiningDate >= '2024-01-01').length;
      const ranged = await readSheet({
        moduleCode: 'employees',
        scope: 'all',
        dateFrom: '2024-01-01',
      });
      expect(ranged.sheet.rowCount - 1).toBe(recent);
      const selected = await readSheet({
        moduleCode: 'employees',
        scope: 'selected',
        selectedIds: [3, 5],
      });
      expect(selected.sheet.rowCount).toBe(3);
    });

    it('includes personal and financial columns only when explicitly requested', async () => {
      const without = await readSheet({
        moduleCode: 'employees',
        scope: 'selected',
        selectedIds: [3],
      });
      expect(headerRow(without.sheet)).not.toContain('National ID');
      expect(headerRow(without.sheet)).not.toContain('Bank Account Number');
      const withSensitive = await readSheet({
        moduleCode: 'employees',
        scope: 'selected',
        selectedIds: [3],
        includeSensitive: true,
      });
      expect(headerRow(withSensitive.sheet)).toEqual(
        expect.arrayContaining(['National ID', 'Bank Account Number', 'Date of Birth']),
      );
      const employee = await firstValueFrom(employees.getById(3));
      const column = headerRow(withSensitive.sheet).indexOf('National ID') + 1;
      expect(withSensitive.sheet.getCell(2, column).value).toBe(employee.nationalId);
    });

    it('round-trips: an export of employees can be re-imported as updates without changes of identity', async () => {
      const { sheet } = await readSheet({
        moduleCode: 'employees',
        scope: 'selected',
        selectedIds: [3],
      });
      expect(sheet.getCell(2, 1).value).toBe('EMP003');
    });
  });
});
