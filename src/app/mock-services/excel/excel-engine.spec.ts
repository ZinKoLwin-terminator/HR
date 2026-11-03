import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { ImportMode, ImportPreview, ImportStrategy } from '../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../core/services/data/excel-data.services';
import { ExcelCatalogMockService, ExcelExportMockService } from './excel-export.mock.service';
import { ExcelFileService } from './excel-file.service';
import { ExcelImportMockService } from './excel-import.mock.service';
import { EXCEL_MODULES } from './excel-module';
import { ActivityMockService } from '../activity/activity-mock.service';
import { COLUMNS, GadgetStore, HEADERS, lib, registration, workbookFile } from './excel-testing';

describe('Excel import/export engine', () => {
  let store: GadgetStore;
  let imports: ExcelImportDataService;
  let exports_: ExcelExportDataService;
  let catalog: ExcelModuleCatalogDataService;
  let files: ExcelFileService;

  function setup(limits = {}) {
    store = new GadgetStore();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: APP_CONFIG,
          useValue: {
            ...DEFAULT_APP_CONFIG,
            mockLatencyMs: 0,
            excel: { ...DEFAULT_APP_CONFIG.excel, ...limits },
          },
        },
        { provide: EXCEL_MODULES, useFactory: () => [registration(store)] },
        { provide: ExcelImportDataService, useExisting: ExcelImportMockService },
        { provide: ExcelExportDataService, useExisting: ExcelExportMockService },
        { provide: ExcelModuleCatalogDataService, useExisting: ExcelCatalogMockService },
      ],
    });
    imports = TestBed.inject(ExcelImportDataService);
    exports_ = TestBed.inject(ExcelExportDataService);
    catalog = TestBed.inject(ExcelModuleCatalogDataService);
    files = TestBed.inject(ExcelFileService);
  }
  beforeEach(() => setup());

  async function expectError(
    promise: Promise<unknown>,
    code: AppError['code'],
    text?: RegExp,
  ): Promise<AppError> {
    try {
      await promise;
    } catch (error) {
      expect((error as AppError).code).toBe(code);
      if (text) expect((error as AppError).message).toMatch(text);
      return error as AppError;
    }
    throw new Error(`Expected ${code}`);
  }
  const inspect = (file: File) => firstValueFrom(imports.inspect('gadgets', file));
  async function preview(
    rows: Parameters<typeof workbookFile>[0],
    strategy: ImportStrategy = 'InsertOrUpdate',
  ): Promise<ImportPreview> {
    const inspection = await inspect(await workbookFile([HEADERS, ...rows]));
    return firstValueFrom(
      imports.validate({ uploadId: inspection.uploadId, strategy, mapping: inspection.mapping }),
    );
  }
  const row = (
    code: string,
    name = 'Gadget',
    status = 'Active',
    extra: Partial<Record<number, string | number | null>> = {},
  ) => {
    const cells: (string | number | null)[] = [
      code,
      name,
      5,
      9.5,
      status,
      '2026-03-31',
      'a@example.com',
      'top secret',
    ];
    Object.entries(extra).forEach(([i, v]) => (cells[Number(i)] = v as string | number | null));
    return cells;
  };

  it('lists registered modules', async () => {
    expect((await firstValueFrom(catalog.getModules())).map((m) => m.code)).toEqual(['gadgets']);
  });

  describe('template', () => {
    it('contains instructions, the data sheet, a field guide, drop-downs and frozen headers', async () => {
      const { blob, fileName } = await firstValueFrom(imports.getTemplate('gadgets'));
      expect(fileName).toBe('gadgets-import-template.xlsx');
      const workbook = new (await lib()).Workbook();
      await workbook.xlsx.load(await blob.arrayBuffer());
      expect(workbook.worksheets.map((w) => w.name)).toEqual([
        'Instructions',
        'Gadgets',
        'Field guide',
        'Lists',
      ]);
      expect(workbook.getWorksheet('Lists')?.state).toBe('hidden');

      const data = workbook.getWorksheet('Gadgets')!;
      expect(HEADERS.map((_, i) => data.getCell(1, i + 1).value)).toEqual(HEADERS);
      expect(data.getCell(1, 1).font?.bold).toBe(true);
      expect(data.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
      const models = (
        data as unknown as { dataValidations: { model: Record<string, { type: string }> } }
      ).dataValidations.model;
      expect(Object.values(models).map((v) => v.type)).toEqual(
        expect.arrayContaining(['list', 'whole', 'decimal', 'date', 'textLength']),
      );
      const guide = workbook.getWorksheet('Field guide')!;
      expect(guide.getCell(2, 1).value).toBe('Gadget Code');
      expect(guide.getCell(2, 2).value).toBe('Yes');
      expect(String(guide.getCell(6, 4).value)).toContain('Active, Inactive');
    });

    it('is not an importable upload by itself (no data rows)', async () => {
      const { blob } = await firstValueFrom(imports.getTemplate('gadgets'));
      await expectError(inspect(new File([blob], 'template.xlsx')), 'Validation', /no data rows/);
    });

    it('refuses unknown or import-less modules', async () => {
      await expectError(firstValueFrom(imports.getTemplate('nope')), 'NotFound');
    });
  });

  describe('file-level validation', () => {
    it('accepts a valid workbook and reports its structure', async () => {
      const inspection = await inspect(await workbookFile([HEADERS, row('G-100')]));
      expect(inspection).toMatchObject({
        sheetName: 'Gadgets',
        rowCount: 1,
        needsMapping: false,
        unknownHeaders: [],
        missingRequired: [],
      });
    });

    it('rejects other extensions, empty files and renamed non-Excel files', async () => {
      await expectError(inspect(new File(['a,b'], 'data.csv')), 'Validation', /\.xlsx/);
      await expectError(inspect(new File(['x'], 'macro.xlsm')), 'Validation', /\.xlsx/);
      await expectError(inspect(new File([], 'empty.xlsx')), 'Validation', /empty/);
      await expectError(
        inspect(new File(['not a zip at all'], 'fake.xlsx')),
        'Validation',
        /does not match/,
      );
      await expectError(
        inspect(new File([new Uint8Array([0x25, 0x50, 0x44, 0x46])], 'doc.xlsx')),
        'Validation',
        /does not match/,
      );
    });

    it('rejects a corrupted workbook', async () => {
      const garbage = new Uint8Array([0x50, 0x4b, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
      await expectError(inspect(new File([garbage], 'broken.xlsx')), 'Validation', /corrupted/);
    });

    it('rejects macros and external links', async () => {
      const zipHead = [0x50, 0x4b, 0x03, 0x04];
      const encode = (text: string) =>
        new Uint8Array([...zipHead, ...new TextEncoder().encode(text)]);
      await expectError(
        inspect(new File([encode('xl/vbaProject.bin')], 'm.xlsx')),
        'Validation',
        /macros/,
      );
      await expectError(
        inspect(new File([encode('xl/externalLinks/externalLink1.xml')], 'l.xlsx')),
        'Validation',
        /links/,
      );
    });

    it('enforces the size, row and worksheet limits', async () => {
      TestBed.resetTestingModule();
      setup({ maxFileBytes: 200, maxRows: 2, maxSheets: 2 });
      await expectError(
        inspect(await workbookFile([HEADERS, row('G-1')])),
        'Validation',
        /too large/,
      );

      TestBed.resetTestingModule();
      setup({ maxRows: 2 });
      await expectError(
        inspect(await workbookFile([HEADERS, row('A'), row('B'), row('C')])),
        'Validation',
        /more than 2 data rows/,
      );

      TestBed.resetTestingModule();
      setup({ maxSheets: 2 });
      await expectError(
        inspect(await workbookFile([HEADERS, row('A')], { extraSheets: 2 })),
        'Validation',
        /3 worksheets/,
      );
    });

    it('rejects an empty worksheet, a missing header row and duplicate headers', async () => {
      await expectError(inspect(await workbookFile([HEADERS])), 'Validation', /no data rows/);
      await expectError(
        inspect(
          await workbookFile([
            [null, null],
            [1, 2],
          ]),
        ),
        'Validation',
        /column headers/,
      );
      await expectError(
        inspect(
          await workbookFile([
            ['Gadget Code', 'gadget code'],
            ['a', 'b'],
          ]),
        ),
        'Validation',
        /more than once/,
      );
    });

    it('falls back to the first visible sheet when the named one is missing', async () => {
      const inspection = await inspect(
        await workbookFile([HEADERS, row('G-9')], { sheet: 'Whatever' }),
      );
      expect(inspection.sheetName).toBe('Whatever');
    });
  });

  describe('column mapping', () => {
    it('recognises headers ignoring case, spacing, asterisks and aliases', async () => {
      const inspection = await inspect(
        await workbookFile([
          ['code *', 'GADGET NAME', 'status', 'Extra'],
          ['G-1', 'n', 'Active', 'x'],
        ]),
      );
      expect(inspection.mapping).toMatchObject({
        'code *': 'code',
        'GADGET NAME': 'name',
        status: 'status',
        Extra: null,
      });
      expect(inspection.unknownHeaders).toEqual(['Extra']);
      expect(inspection.needsMapping).toBe(false);
    });

    it('asks for mapping when a required column is missing or two headers collide', async () => {
      const missing = await inspect(
        await workbookFile([
          ['Gadget Code', 'Status'],
          ['G-1', 'Active'],
        ]),
      );
      expect(missing.missingRequired).toEqual(['name']);
      expect(missing.needsMapping).toBe(true);

      const clash = await inspect(
        await workbookFile([
          ['Gadget Code', 'Code', 'Gadget Name', 'Status'],
          ['G-1', 'x', 'n', 'Active'],
        ]),
      );
      expect(clash.ambiguousHeaders.sort()).toEqual(['Code', 'Gadget Code']);
      expect(clash.needsMapping).toBe(true);
    });

    it('validates with a corrected mapping and refuses an unusable one', async () => {
      const inspection = await inspect(
        await workbookFile([
          ['Id', 'Label', 'State'],
          ['G-5', 'Thing', 'Active'],
        ]),
      );
      await expectError(
        firstValueFrom(
          imports.validate({
            uploadId: inspection.uploadId,
            strategy: 'InsertOnly',
            mapping: inspection.mapping,
          }),
        ),
        'Validation',
        /required column/,
      );
      const fixed = { Id: 'code', Label: 'name', State: 'status' };
      const result = await firstValueFrom(
        imports.validate({ uploadId: inspection.uploadId, strategy: 'InsertOnly', mapping: fixed }),
      );
      expect(result.validRows).toBe(1);
      await expectError(
        firstValueFrom(
          imports.validate({
            uploadId: inspection.uploadId,
            strategy: 'InsertOnly',
            mapping: { Id: 'code', Label: 'code', State: 'status' },
          }),
        ),
        'Validation',
        /both mapped/,
      );
      await expectError(
        firstValueFrom(
          imports.validate({ uploadId: 'UPL-gone', strategy: 'InsertOnly', mapping: fixed }),
        ),
        'NotFound',
      );
    });
  });

  describe('row validation', () => {
    it('classifies rows as valid, invalid, warning and duplicate', async () => {
      const result = await preview([
        row('G-200'), // valid
        row('G-201', 'WARN thing'), // warning (adapter)
        row('G-202', 'Gadget', 'Actve'), // enum typo → invalid with suggestion
        row('G-203', 'forbidden'), // business rule via dry run
        row('G-200', 'Again'), // duplicate within the file
        row('G-001', 'Existing again'), // existing record → update (insert-or-update)
      ]);
      expect(result.rows.map((r) => r.status)).toEqual([
        'Valid',
        'Warning',
        'Invalid',
        'Invalid',
        'Duplicate',
        'Valid',
      ]);
      expect(result.rows.map((r) => r.action)).toEqual([
        'Insert',
        'Insert',
        'None',
        'None',
        'None',
        'Update',
      ]);
      expect(result).toMatchObject({
        totalRows: 6,
        validRows: 3,
        warningRows: 1,
        invalidRows: 2,
        duplicateRows: 1,
      });
      expect(result.rows[2].issues[0].suggestion).toMatch(/Did you mean "Active"/);
      expect(result.rows[3].issues[0]).toMatchObject({
        field: 'Gadget Name',
        message: 'That name is not allowed.',
      });
      expect(result.rows[4].issues[0].message).toMatch(/Duplicate of row 2/);
      expect(result.rows[0].rowNumber).toBe(2);
    });

    it('checks types, formats, lengths and precision per column', async () => {
      const result = await preview([
        row('TOO-LONG-CODE-1', 'n'),
        row('G-1', 'n', 'Active', { 2: 'abc' }),
        row('G-2', 'n', 'Active', { 2: 2.5 }),
        row('G-3', 'n', 'Active', { 2: 500 }),
        row('G-4', 'n', 'Active', { 3: 1.234 }),
        row('G-5', 'n', 'Active', { 5: '31/12/2026' }),
        row('G-6', 'n', 'Active', { 5: '2026-02-30' }),
        row('G-7', 'n', 'Active', { 6: 'not-an-email' }),
        row('G-8', null as unknown as string),
      ]);
      const messages = result.rows.map((r) => r.issues[0]?.message ?? '');
      expect(messages[0]).toMatch(/at most 10 characters/);
      expect(messages[1]).toMatch(/must be a number/);
      expect(messages[2]).toMatch(/whole number/);
      expect(messages[3]).toMatch(/at most 100/);
      expect(messages[4]).toMatch(/2 decimal places/);
      expect(messages[5]).toMatch(/not a valid date/);
      expect(messages[6]).toMatch(/not a valid date/);
      expect(messages[7]).toMatch(/valid email/);
      expect(messages[8]).toMatch(/Gadget Name is required/);
      expect(result.invalidRows).toBe(9);
    });

    it('reads real date cells, accepts dd MMM yyyy text and ignores blank rows', async () => {
      const result = await preview([
        [...row('G-10').slice(0, 5), new Date(Date.UTC(2026, 2, 31)), 'a@example.com', null],
        [null, null, null, null, null, null, null, null],
        row('G-11', 'n', 'Active', { 5: '05 Mar 2026' }),
      ]);
      expect(result.totalRows).toBe(2);
      expect(result.rows[0].values['startDate']).toBe('2026-03-31');
      expect(result.rows[1].values['startDate']).toBe('2026-03-05');
      expect(result.rows.every((r) => r.status === 'Valid')).toBe(true);
    });

    it('never evaluates formulas', async () => {
      const result = await preview([
        [...row('G-20').slice(0, 1), { formula: '1+1' }, 5, 9.5, 'Active', null, null, null],
      ]);
      expect(result.rows[0].status).toBe('Invalid');
      expect(result.rows[0].issues[0].message).toMatch(/Formulas are not allowed/);
    });

    it('applies the import strategy to existing records', async () => {
      const insertOnly = await preview([row('G-001'), row('G-300')], 'InsertOnly');
      expect(insertOnly.rows.map((r) => r.status)).toEqual(['Duplicate', 'Valid']);
      expect(insertOnly.rows[0].issues[0].message).toMatch(/already exists/);
      const updateOnly = await preview([row('G-001'), row('G-300')], 'UpdateOnly');
      expect(updateOnly.rows.map((r) => r.status)).toEqual(['Valid', 'Invalid']);
      expect(updateOnly.rows[0].action).toBe('Update');
      expect(updateOnly.rows[1].issues[0].message).toMatch(/cannot be updated/);
    });

    it('masks sensitive values in previews and issues', async () => {
      const result = await preview([row('G-1', 'n', 'Active', { 7: 'secret!!' })]);
      expect(result.rows[0].values['secret']).toBe('••••');
      expect(JSON.stringify(result)).not.toContain('secret!!');
    });

    it('does not change any data while validating', async () => {
      await preview([row('G-400'), row('G-401')]);
      expect(store.items).toHaveLength(1);
    });
  });

  describe('execution', () => {
    const run = (batchId: string, mode: ImportMode) =>
      firstValueFrom(imports.execute(batchId, mode));

    it('imports only the valid rows in partial mode and reports the rest', async () => {
      const result = await preview([
        row('G-500'),
        row('G-501', 'forbidden'),
        row('G-001', 'Changed'),
      ]);
      const outcome = await run(result.batchId, 'Partial');
      expect(outcome).toMatchObject({
        status: 'CompletedWithErrors',
        inserted: 1,
        updated: 1,
        skipped: 1,
        failed: 0,
        rolledBack: false,
      });
      expect(store.items.map((g) => g.code)).toEqual(['G-001', 'G-500']);
      expect(store.items[0].name).toBe('Changed');
    });

    it('refuses all-or-nothing when any row has a problem, leaving the batch usable', async () => {
      const result = await preview([row('G-510'), row('G-511', 'forbidden')]);
      await expectError(
        run(result.batchId, 'AllOrNothing'),
        'Conflict',
        /1 row\(s\) have problems/,
      );
      expect(store.items).toHaveLength(1);
      expect((await run(result.batchId, 'Partial')).inserted).toBe(1);
    });

    it('commits everything in all-or-nothing mode when all rows are valid', async () => {
      const result = await preview([row('G-520'), row('G-521')]);
      expect(await run(result.batchId, 'AllOrNothing')).toMatchObject({
        status: 'Completed',
        inserted: 2,
        rolledBack: false,
      });
      expect(store.items).toHaveLength(3);
    });

    it('rolls back an all-or-nothing import when a row fails while saving', async () => {
      const result = await preview([row('G-530'), row('G-531'), row('G-532')]);
      store.failOn = 'G-532'; // something changes between validation and import
      const outcome = await run(result.batchId, 'AllOrNothing');
      expect(outcome).toMatchObject({ status: 'Failed', inserted: 0, failed: 1, rolledBack: true });
      expect(outcome.message).toMatch(/rolled back/);
      expect(store.items.map((g) => g.code)).toEqual(['G-001']);
    });

    it('keeps the saved rows in partial mode when one row fails while saving', async () => {
      const result = await preview([row('G-540'), row('G-541')]);
      store.failOn = 'G-541';
      const outcome = await run(result.batchId, 'Partial');
      expect(outcome).toMatchObject({ status: 'CompletedWithErrors', inserted: 1, failed: 1 });
      expect(store.items.map((g) => g.code)).toEqual(['G-001', 'G-540']);
    });

    it('detects records created after validation (concurrent imports)', async () => {
      const a = await preview([row('G-550')], 'InsertOnly');
      const b = await preview([row('G-550')], 'InsertOnly');
      expect((await run(a.batchId, 'Partial')).inserted).toBe(1);
      const second = await run(b.batchId, 'Partial');
      expect(second).toMatchObject({ inserted: 0, failed: 1 });
      expect(store.items.filter((g) => g.code === 'G-550')).toHaveLength(1);
    });

    it('imports a batch only once', async () => {
      const result = await preview([row('G-560')]);
      await run(result.batchId, 'Partial');
      await expectError(run(result.batchId, 'Partial'), 'Conflict', /already been processed/);
      await expectError(run('IMP-unknown', 'Partial'), 'NotFound');
    });

    it('refuses a batch with nothing to import', async () => {
      const result = await preview([row('G-570', 'forbidden')]);
      await expectError(run(result.batchId, 'Partial'), 'Conflict', /no valid rows/);
    });

    it('records history, batch details and an audit entry without file contents', async () => {
      const result = await preview([row('G-580'), row('G-581', 'forbidden')]);
      await run(result.batchId, 'Partial');
      const history = await firstValueFrom(imports.getHistory());
      expect(history[0]).toMatchObject({
        batchId: result.batchId,
        moduleCode: 'gadgets',
        moduleLabel: 'Gadgets',
        fileName: 'gadgets.xlsx',
        mode: 'Partial',
        status: 'CompletedWithErrors',
        totalRows: 2,
        validRows: 1,
        invalidRows: 1,
        successRows: 1,
        skippedRows: 1,
        failedRows: 0,
      });
      expect(history[0].uploadedBy).toBeTruthy();
      expect(history[0].startedAt && history[0].completedAt).toBeTruthy();
      const detail = await firstValueFrom(imports.getBatch(result.batchId));
      expect(detail.issues.map((i) => i.rowNumber)).toEqual([3]);
      const activity = TestBed.inject(ActivityMockService)
        .snapshot(5)
        .map((a) => a.message);
      expect(activity.some((m) => m.includes('Excel import of Gadgets'))).toBe(true);
    });

    it('builds a downloadable validation report', async () => {
      const result = await preview([
        row('G-590', 'forbidden'),
        row('G-591', 'n', 'Actve', { 6: '=HYPERLINK("http://evil")' }),
      ]);
      const { blob, fileName } = await firstValueFrom(imports.getErrorReport(result.batchId));
      expect(fileName).toBe(`gadgets-import-report-${result.batchId}.xlsx`);
      const workbook = new (await lib()).Workbook();
      await workbook.xlsx.load(await blob.arrayBuffer());
      const sheet = workbook.getWorksheet('Validation report')!;
      expect(
        ['Row', 'Field', 'Original value', 'Severity', 'Error', 'Suggested correction'].map(
          (_, i) => sheet.getCell(1, i + 1).value,
        ),
      ).toEqual(['Row', 'Field', 'Original value', 'Severity', 'Error', 'Suggested correction']);
      expect(sheet.getCell(2, 1).value).toBe(2);
      expect(sheet.getCell(2, 2).value).toBe('Gadget Name');
      expect(String(sheet.getCell(2, 5).value)).toBe('That name is not allowed.');
      const echoed = sheet.getRow(4).values as unknown[];
      expect(echoed.map(String).join('|')).toContain("'=HYPERLINK"); // values echoed from the upload are neutralised
    });
  });

  describe('export', () => {
    beforeEach(() => {
      store.items.push(
        {
          id: 2,
          code: 'G-002',
          name: '=CMD("calc")',
          qty: 3,
          price: 1234.5,
          status: 'Inactive',
          startDate: '2026-02-01',
          email: null,
          secret: 'hidden',
        },
        {
          id: 3,
          code: 'G-003',
          name: '-5',
          qty: 7,
          price: null,
          status: 'Active',
          startDate: null,
          email: 'x@example.com',
          secret: null,
        },
      );
    });
    const read = async (request: Parameters<typeof exports_.export>[0]) => {
      const { blob, fileName } = await firstValueFrom(exports_.export(request));
      const workbook = new (await lib()).Workbook();
      await workbook.xlsx.load(await blob.arrayBuffer());
      return { workbook, fileName };
    };

    it('writes a formatted workbook: names, headers, types, formats, filter and frozen header', async () => {
      const { workbook, fileName } = await read({ moduleCode: 'gadgets', scope: 'all' });
      expect(fileName).toMatch(/^gadgets-export-\d{8}-\d{4}\.xlsx$/);
      expect(workbook.worksheets.map((w) => w.name)).toEqual(['Gadgets', 'Summary']);
      const sheet = workbook.getWorksheet('Gadgets')!;
      expect(
        COLUMNS.filter((c) => !c.sensitive).map((_, i) => sheet.getCell(1, i + 1).value),
      ).toEqual([
        'Gadget Code',
        'Gadget Name',
        'Quantity',
        'Price',
        'Status',
        'Start Date',
        'Email',
      ]);
      expect(sheet.getCell(1, 1).font?.bold).toBe(true);
      expect(sheet.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
      expect(sheet.autoFilter).toBeTruthy();
      expect(sheet.rowCount).toBe(4);
      // numbers stay numbers with their format; dates are real dates
      expect(sheet.getCell(3, 4).value).toBe(1234.5);
      expect(sheet.getCell(3, 4).numFmt).toBe('"$"#,##0.00');
      expect(sheet.getCell(3, 3).value).toBe(3);
      expect(sheet.getCell(3, 6).value).toBeInstanceOf(Date);
      expect(sheet.getCell(3, 6).numFmt).toBe('yyyy-mm-dd');
      expect(workbook.getWorksheet('Summary')!.getCell(2, 2).value).toBe(3);
    });

    it('excludes sensitive columns unless explicitly included', async () => {
      const without = (await read({ moduleCode: 'gadgets', scope: 'all' })).workbook.getWorksheet(
        'Gadgets',
      )!;
      expect(without.columnCount).toBe(7);
      const withSecret = (
        await read({ moduleCode: 'gadgets', scope: 'all', includeSensitive: true })
      ).workbook.getWorksheet('Gadgets')!;
      expect(withSecret.columnCount).toBe(8);
      expect(withSecret.getCell(1, 8).value).toBe('Secret Note');
    });

    it('protects against formula injection without corrupting numbers', async () => {
      const sheet = (await read({ moduleCode: 'gadgets', scope: 'all' })).workbook.getWorksheet(
        'Gadgets',
      )!;
      expect(sheet.getCell(3, 2).value).toBe('\'=CMD("calc")'); // dangerous text is neutralised
      expect(sheet.getCell(4, 2).value).toBe('-5'); // numeric-looking text is left alone
      expect(sheet.getCell(3, 3).value).toBe(3); // numeric cells are untouched
    });

    it('supports selected-record export and a custom column choice', async () => {
      const { workbook } = await read({
        moduleCode: 'gadgets',
        scope: 'selected',
        selectedIds: [3],
        columnKeys: ['code', 'status'],
      });
      const sheet = workbook.getWorksheet('Gadgets')!;
      expect(sheet.rowCount).toBe(2);
      expect(sheet.columnCount).toBe(2);
      expect(sheet.getCell(2, 1).value).toBe('G-003');
    });

    it('validates the request and enforces the export limit', async () => {
      await expectError(
        firstValueFrom(
          exports_.export({ moduleCode: 'gadgets', scope: 'selected', selectedIds: [] }),
        ),
        'Validation',
        /Select at least one/,
      );
      await expectError(
        firstValueFrom(
          exports_.export({ moduleCode: 'gadgets', scope: 'all', columnKeys: ['secret'] }),
        ),
        'Validation',
        /at least one column/,
      );
      await expectError(
        firstValueFrom(
          exports_.export({
            moduleCode: 'gadgets',
            scope: 'all',
            dateFrom: '2026-02-01',
            dateTo: '2026-01-01',
          }),
        ),
        'Validation',
        /start date/,
      );
      await expectError(
        firstValueFrom(exports_.export({ moduleCode: 'nope', scope: 'all' })),
        'NotFound',
      );

      TestBed.resetTestingModule();
      setup({ maxExportRows: 2 });
      store.items.push(
        { ...store.items[0], id: 2, code: 'G-2' },
        { ...store.items[0], id: 3, code: 'G-3' },
      );
      await expectError(
        firstValueFrom(exports_.export({ moduleCode: 'gadgets', scope: 'all' })),
        'Validation',
        /more than 2 rows/,
      );
    });

    it('records an audit entry for every export', async () => {
      await read({ moduleCode: 'gadgets', scope: 'all' });
      expect(
        TestBed.inject(ActivityMockService)
          .snapshot(3)
          .some((a) => a.message.includes('Exported 3 Gadgets')),
      ).toBe(true);
    });

    it('exports a large dataset within a reasonable time', async () => {
      store.items = Array.from({ length: 5000 }, (_, i) => ({
        id: i + 1,
        code: `G-${i}`,
        name: `Gadget ${i}`,
        qty: i % 100,
        price: i / 3,
        status: 'Active',
        startDate: '2026-01-15',
        email: `g${i}@example.com`,
        secret: null,
      }));
      const started = performance.now();
      const { workbook } = await read({ moduleCode: 'gadgets', scope: 'all' });
      expect(workbook.getWorksheet('Gadgets')!.rowCount).toBe(5001);
      expect(performance.now() - started).toBeLessThan(15_000);
    });
  });

  it('exposes the file service for adapters', () => {
    expect(files).toBeTruthy();
  });
});
