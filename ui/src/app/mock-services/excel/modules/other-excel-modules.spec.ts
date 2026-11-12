import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { ExcelModuleInfo, ImportPreview, ImportStrategy } from '../../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../../core/services/data/excel-data.services';
import { ShiftDataService } from '../../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
} from '../../../core/services/data/organization-data.services';
import {
  SalaryAssignmentDataService,
  SalaryStructureDataService,
} from '../../../core/services/data/payroll-data.services';
import { provideMockDataServices } from '../../provide-mock-data-services';
import { lib, workbookFile } from '../excel-testing';

const IMPORTABLE = [
  'companies',
  'branches',
  'departments',
  'sections',
  'positions',
  'job-grades',
  'shift-assignments',
  'salary-assignments',
];
const EXPORT_ONLY = ['leave-applications', 'leave-balances', 'attendance-records'];

describe(
  'Excel modules for the organization, leave, attendance and payroll screens',
  {
    timeout: 30_000,
  },
  () => {
    let imports: ExcelImportDataService;
    let exports_: ExcelExportDataService;
    let infos: Map<string, ExcelModuleInfo>;

    beforeEach(async () => {
      TestBed.configureTestingModule({
        providers: [
          provideMockDataServices(),
          { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
        ],
      });
      imports = TestBed.inject(ExcelImportDataService);
      exports_ = TestBed.inject(ExcelExportDataService);
      const modules = await firstValueFrom(
        TestBed.inject(ExcelModuleCatalogDataService).getModules(),
      );
      infos = new Map(modules.map((m) => [m.code, m]));
    });

    const headers = (code: string) => infos.get(code)!.columns.map((c) => c.header);

    /** One row from header → value pairs (other columns blank). */
    const rowOf = (code: string, values: Record<string, string | number>) =>
      headers(code).map((h) => values[h] ?? null);

    async function preview(
      code: string,
      rows: (string | number | null)[][],
      strategy: ImportStrategy = 'InsertOrUpdate',
    ): Promise<ImportPreview> {
      const inspection = await firstValueFrom(
        imports.inspect(
          code,
          await workbookFile([headers(code), ...rows], { sheet: infos.get(code)!.sheetName }),
        ),
      );
      expect(inspection.needsMapping).toBe(false);
      return firstValueFrom(
        imports.validate({ uploadId: inspection.uploadId, strategy, mapping: inspection.mapping }),
      );
    }
    const messages = (result: ImportPreview, index = 0) =>
      result.rows[index].issues.map((i) => `${i.field}: ${i.message}`).join(' | ');

    async function importNow(code: string, rows: (string | number | null)[][]) {
      const checked = await preview(code, rows);
      expect(checked.invalidRows).toBe(0);
      return firstValueFrom(imports.execute(checked.batchId, 'AllOrNothing'));
    }

    // Real codes from the sample data.
    async function sample() {
      const companies = await firstValueFrom(TestBed.inject(CompanyDataService).getAll());
      const departments = await firstValueFrom(TestBed.inject(DepartmentDataService).getAll());
      const grades = await firstValueFrom(TestBed.inject(JobGradeDataService).getAll());
      const employees = await firstValueFrom(TestBed.inject(EmployeeDataService).getAll());
      const shifts = await firstValueFrom(TestBed.inject(ShiftDataService).getAll());
      const structures = await firstValueFrom(TestBed.inject(SalaryStructureDataService).getAll());
      return { companies, departments, grades, employees, shifts, structures };
    }

    describe('catalog', () => {
      it('offers import and export for the organization, shift and salary modules', () => {
        for (const code of IMPORTABLE) {
          expect(infos.get(code), code).toMatchObject({ canImport: true, canExport: true });
          expect(infos.get(code)!.columns.length, code).toBeGreaterThan(1);
        }
      });

      it('offers only export for leave applications, leave balances and attendance records', () => {
        for (const code of EXPORT_ONLY) {
          expect(infos.get(code), code).toMatchObject({ canImport: false, canExport: true });
          expect(infos.get(code)!.exportColumns?.length, code).toBeGreaterThan(5);
        }
      });

      it('has readable labels and the keys of the API modules', () => {
        expect(
          [...infos.values()].filter((m) => m.code !== 'employees').map((m) => m.label),
        ).toEqual(
          expect.arrayContaining(['Companies', 'Job grades', 'Positions', 'Salary assignments']),
        );
        expect(infos.get('departments')!.matchKey).toEqual(['code']);
        expect(infos.get('shift-assignments')!.matchKey).toEqual(['employeeCode']);
        expect(infos.get('salary-assignments')!.matchKey).toEqual([
          'employeeCode',
          'effectiveFrom',
        ]);
        expect(
          infos.get('salary-assignments')!.columns.find((c) => c.key === 'basicSalary'),
        ).toMatchObject({
          sensitive: true,
        });
      });

      it('builds an import template with the column headers for every importable module', async () => {
        for (const code of IMPORTABLE) {
          const { blob, fileName } = await firstValueFrom(imports.getTemplate(code));
          expect(fileName).toBe(`${code}-import-template.xlsx`);
          const workbook = new (await lib()).Workbook();
          await workbook.xlsx.load(await blob.arrayBuffer());
          const data = workbook.getWorksheet(infos.get(code)!.sheetName)!;
          expect(headers(code).map((_, i) => String(data.getCell(1, i + 1).value))).toEqual(
            headers(code),
          );
        }
      });
    });

    describe('organization family', () => {
      it('imports a department and rejects an unknown company code', async () => {
        const s = await sample();
        const company = s.companies[0];
        const department = s.departments.find((d) => d.companyId === company.id)!;
        const branchCode = (await firstValueFrom(TestBed.inject(BranchDataService).getAll())).find(
          (b) => b.id === department.branchId,
        )!.code;
        const good = rowOf('departments', {
          'Department Code': 'QA-XL',
          'Department Name': 'Quality Excel',
          'Company Code': company.code,
          'Branch Code': branchCode,
        });
        const result = await importNow('departments', [good]);
        expect(result).toMatchObject({ inserted: 1, failed: 0, rolledBack: false });
        const all = await firstValueFrom(TestBed.inject(DepartmentDataService).getAll());
        expect(all.find((d) => d.code === 'QA-XL')).toMatchObject({
          name: 'Quality Excel',
          status: 'Active',
        });

        const bad = await preview('departments', [
          rowOf('departments', {
            'Department Code': 'QA-XM',
            'Department Name': 'Quality Two',
            'Company Code': 'NOPE',
            'Branch Code': branchCode,
          }),
        ]);
        expect(bad.rows[0].status).toBe('Invalid');
        expect(messages(bad)).toContain('Company "NOPE" does not exist.');
      });

      it('updates an existing company by code, keeps blank optional cells and changes status', async () => {
        const [company] = (await sample()).companies;
        const result = await importNow('companies', [
          rowOf('companies', {
            'Company Code': company.code,
            'Company Name': company.name,
            'Legal Name': 'Renamed Legal Name Ltd',
            Industry: company.industry,
            Email: company.email,
            Phone: company.phone,
            City: company.city,
            Country: company.country,
            'Founded Date': company.foundedDate,
            Status: 'Inactive',
          }),
        ]);
        expect(result).toMatchObject({ updated: 1, inserted: 0 });
        const saved = (await firstValueFrom(TestBed.inject(CompanyDataService).getAll())).find(
          (c) => c.id === company.id,
        )!;
        expect(saved).toMatchObject({
          legalName: 'Renamed Legal Name Ltd',
          status: 'Inactive',
          website: company.website,
          address: company.address,
        });
      });

      it('rejects a company whose name is already used', async () => {
        const companies = (await sample()).companies;
        const result = await preview('companies', [
          rowOf('companies', {
            'Company Code': 'NEWCO',
            'Company Name': companies[0].name,
            'Legal Name': 'New Co Ltd',
            Industry: 'Trading',
            Email: 'info@newco.example',
            Phone: '+95 1 234 567',
            City: 'Yangon',
            Country: 'Myanmar',
            'Founded Date': '2020-01-01',
          }),
        ]);
        expect(result.rows[0].status).toBe('Invalid');
        expect(messages(result)).toContain('must be unique');
      });

      it('imports a job grade and rejects a band whose maximum is below its minimum', async () => {
        const ok = await importNow('job-grades', [
          rowOf('job-grades', {
            'Grade Code': 'GX1',
            'Grade Name': 'Excel grade',
            Level: 9,
            'Minimum Salary': 1000,
            'Maximum Salary': 2000,
          }),
        ]);
        expect(ok.inserted).toBe(1);
        const bad = await preview('job-grades', [
          rowOf('job-grades', {
            'Grade Code': 'GX2',
            'Grade Name': 'Bad band',
            Level: 10,
            'Minimum Salary': 3000,
            'Maximum Salary': 2000,
          }),
        ]);
        expect(bad.rows[0].status).toBe('Invalid');
        expect(messages(bad)).toContain('Maximum Salary: Must be ≥ minimum salary.');
      });

      it('imports a position (job title) by department and grade code, and reports unknown codes', async () => {
        const s = await sample();
        const department = s.departments[0];
        const grade = s.grades[0];
        const ok = await importNow('positions', [
          rowOf('positions', {
            'Position Code': 'PX-1',
            'Position Title': 'Excel Analyst',
            'Department Code': department.code,
            'Job Grade Code': grade.code,
            Headcount: 2,
          }),
        ]);
        expect(ok.inserted).toBe(1);
        const saved = (await firstValueFrom(TestBed.inject(PositionDataService).getAll())).find(
          (p) => p.code === 'PX-1',
        )!;
        expect(saved).toMatchObject({
          title: 'Excel Analyst',
          departmentId: department.id,
          jobGradeId: grade.id,
        });
        const bad = await preview('positions', [
          rowOf('positions', {
            'Position Code': 'PX-2',
            'Position Title': 'Nobody',
            'Department Code': department.code,
            'Job Grade Code': 'ZZZ',
            Headcount: 1,
          }),
          rowOf('positions', {
            'Position Code': 'PX-3',
            'Position Title': 'Nobody',
            'Department Code': department.code,
            'Job Grade Code': grade.code,
            Headcount: 0,
          }),
        ]);
        expect(messages(bad)).toContain('Job grade "ZZZ" does not exist.');
        expect(messages(bad, 1)).toContain('Headcount must be at least 1');
      });

      it('imports a section and a branch with their parents given by code', async () => {
        const s = await sample();
        const department = s.departments[0];
        expect(
          (
            await importNow('sections', [
              rowOf('sections', {
                'Section Code': 'SX-1',
                'Section Name': 'Excel section',
                'Department Code': department.code,
              }),
            ])
          ).inserted,
        ).toBe(1);
        const bad = await preview('branches', [
          rowOf('branches', {
            'Branch Code': 'BX-1',
            'Branch Name': 'Excel branch',
            'Company Code': s.companies[0].code,
            'Branch Type': 'Garage',
            City: 'Yangon',
            Country: 'Myanmar',
            'Opened Date': '2020-01-01',
          }),
        ]);
        expect(messages(bad)).toContain('Branch Type "Garage" is not an allowed value');
      });

      it('exports the organization modules with their columns, parents by code, and honours the filters', async () => {
        const { blob } = await firstValueFrom(
          exports_.export({ moduleCode: 'departments', scope: 'all' }),
        );
        const workbook = new (await lib()).Workbook();
        await workbook.xlsx.load(await blob.arrayBuffer());
        const sheet = workbook.getWorksheet('Departments')!;
        const head = (sheet.getRow(1).values as unknown[]).slice(1).map(String);
        expect(head).toEqual(
          expect.arrayContaining([
            'Department Code',
            'Company Code',
            'Branch Code',
            'Company',
            'Branch',
          ]),
        );
        expect(sheet.rowCount).toBeGreaterThan(2);
        const filtered = await firstValueFrom(
          exports_.export({ moduleCode: 'departments', scope: 'filtered', search: 'zzz-no-match' }),
        );
        const empty = new (await lib()).Workbook();
        await empty.xlsx.load(await filtered.blob.arrayBuffer());
        expect(empty.getWorksheet('Departments')!.rowCount).toBe(1);
      });
    });

    describe('shift assignments', () => {
      it('sets the shift of an employee and rejects unknown employee and shift codes', async () => {
        const s = await sample();
        const employee = s.employees.find((e) => e.employmentStatus === 'Active')!;
        const shift = s.shifts.find((x) => x.id !== employee.shiftId)!;
        const result = await importNow('shift-assignments', [
          rowOf('shift-assignments', {
            'Employee Code': employee.employeeCode,
            'Shift Code': shift.code,
          }),
        ]);
        expect(result.inserted + result.updated).toBe(1);
        const saved = (await firstValueFrom(TestBed.inject(EmployeeDataService).getAll())).find(
          (e) => e.id === employee.id,
        )!;
        expect(saved.shiftId).toBe(shift.id);

        const bad = await preview('shift-assignments', [
          rowOf('shift-assignments', { 'Employee Code': 'NOPE1', 'Shift Code': 'NOSHIFT' }),
        ]);
        expect(bad.rows[0].status).toBe('Invalid');
        expect(messages(bad)).toContain('Employee "NOPE1" does not exist.');
        expect(messages(bad)).toContain('Shift "NOSHIFT" does not exist.');
      });

      it('exports every employee with the shift code', async () => {
        const { blob } = await firstValueFrom(
          exports_.export({ moduleCode: 'shift-assignments', scope: 'all' }),
        );
        const workbook = new (await lib()).Workbook();
        await workbook.xlsx.load(await blob.arrayBuffer());
        const sheet = workbook.getWorksheet('Shift assignments')!;
        expect((sheet.getRow(1).values as unknown[]).slice(1).map(String)).toContain('Shift Code');
        expect(sheet.rowCount).toBeGreaterThan(5);
      });
    });

    describe('salary assignments', () => {
      it('adds an assignment inside the grade band and masks the salary in the preview', async () => {
        const s = await sample();
        const employee = s.employees.find((e) => e.employmentStatus === 'Active')!;
        const grade = s.grades.find((g) => g.id === employee.jobGradeId)!;
        const row = rowOf('salary-assignments', {
          'Employee Code': employee.employeeCode,
          'Effective From': '2099-01-01',
          'Salary Structure Code': s.structures.find((x) => x.status === 'Active')!.code,
          'Basic Salary': grade.minSalary,
          Remarks: 'Excel test',
        });
        const checked = await preview('salary-assignments', [row]);
        expect(checked.rows[0]).toMatchObject({ status: 'Valid', action: 'Insert' });
        expect(checked.rows[0].values['basicSalary']).not.toContain(String(grade.minSalary));
        const result = await firstValueFrom(imports.execute(checked.batchId, 'AllOrNothing'));
        expect(result.inserted).toBe(1);
        const saved = (
          await firstValueFrom(TestBed.inject(SalaryAssignmentDataService).getAll())
        ).find((a) => a.employeeId === employee.id && a.effectiveFrom === '2099-01-01')!;
        expect(saved.basicSalary).toBe(grade.minSalary);
      });

      it('rejects a salary outside the band and an unknown structure', async () => {
        const s = await sample();
        const employee = s.employees.find((e) => e.employmentStatus === 'Active')!;
        const grade = s.grades.find((g) => g.id === employee.jobGradeId)!;
        const result = await preview('salary-assignments', [
          rowOf('salary-assignments', {
            'Employee Code': employee.employeeCode,
            'Effective From': '2099-02-01',
            'Salary Structure Code': s.structures[0].code,
            'Basic Salary': grade.maxSalary + 1,
          }),
          rowOf('salary-assignments', {
            'Employee Code': employee.employeeCode,
            'Effective From': '2099-03-01',
            'Salary Structure Code': 'NOPE',
            'Basic Salary': grade.minSalary,
          }),
        ]);
        expect(result.rows[0].status).toBe('Invalid');
        expect(messages(result, 0)).toContain('band');
        expect(messages(result, 1)).toContain('Salary structure "NOPE" does not exist.');
      });

      it('updates the assignment that has the same employee and start date', async () => {
        const s = await sample();
        const existing = (
          await firstValueFrom(TestBed.inject(SalaryAssignmentDataService).getAll())
        )[0];
        const employee = s.employees.find((e) => e.id === existing.employeeId)!;
        const structure = s.structures.find((x) => x.id === existing.structureId)!;
        const checked = await preview('salary-assignments', [
          rowOf('salary-assignments', {
            'Employee Code': employee.employeeCode,
            'Effective From': existing.effectiveFrom,
            'Salary Structure Code': structure.code,
            'Basic Salary': s.grades.find((g) => g.id === employee.jobGradeId)!.minSalary,
          }),
        ]);
        expect(messages(checked)).toBe('');
        expect(checked.rows[0]).toMatchObject({ status: 'Valid', action: 'Update' });
      });
    });

    describe('export only modules', () => {
      it.each(EXPORT_ONLY)(
        'exports %s with the columns of the module and at least one row',
        async (code) => {
          const { blob, fileName } = await firstValueFrom(
            exports_.export({ moduleCode: code, scope: 'all', dateFrom: null, dateTo: null }),
          );
          expect(fileName).toContain(`${code}-export`);
          const workbook = new (await lib()).Workbook();
          await workbook.xlsx.load(await blob.arrayBuffer());
          const sheet = workbook.getWorksheet(infos.get(code)!.sheetName)!;
          const expected = infos.get(code)!.exportColumns!.map((c) => c.header);
          expect((sheet.getRow(1).values as unknown[]).slice(1).map(String)).toEqual(expected);
          expect(sheet.rowCount).toBeGreaterThan(1);
        },
      );

      it('refuses to import them', async () => {
        await expect(firstValueFrom(imports.getTemplate('leave-balances'))).rejects.toBeDefined();
      });

      it('limits leave balances to the year of the filter and rejects an impossible year', async () => {
        const year = new Date().getFullYear() - 1;
        const { blob } = await firstValueFrom(
          exports_.export({ moduleCode: 'leave-balances', scope: 'filtered', filters: { year } }),
        );
        const workbook = new (await lib()).Workbook();
        await workbook.xlsx.load(await blob.arrayBuffer());
        const sheet = workbook.getWorksheet('Leave balances')!;
        expect(Number(sheet.getCell(2, 4).value)).toBe(year);
        await expect(
          firstValueFrom(
            exports_.export({
              moduleCode: 'leave-balances',
              scope: 'filtered',
              filters: { year: 1900 },
            }),
          ),
        ).rejects.toMatchObject({ code: 'Validation' });
      });
    });
  },
);
