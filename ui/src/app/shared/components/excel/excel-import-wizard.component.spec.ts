import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { ColumnMapping, ImportMode, ImportStrategy } from '../../../core/models/excel.models';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../../../core/services/data/excel-data.services';
import {
  ExcelCatalogMockService,
  ExcelExportMockService,
} from '../../../mock-services/excel/excel-export.mock.service';
import { ExcelImportMockService } from '../../../mock-services/excel/excel-import.mock.service';
import { EXCEL_MODULES } from '../../../mock-services/excel/excel-module';
import {
  GadgetStore,
  HEADERS,
  registration,
  workbookFile,
} from '../../../mock-services/excel/excel-testing';
import { ExcelImportWizardComponent } from './excel-import-wizard.component';

interface WizardInternals {
  moduleCode(): string | null;
  stepIndex(): number;
  inspection(): { needsMapping: boolean; missingRequired: string[] } | null;
  preview(): { validRows: number; invalidRows: number; batchId: string } | null;
  result(): { status: string; inserted: number } | null;
  mode(): ImportMode;
  mappingBlocked(): boolean;
  uploadError(): string;
  setFile(file: File | null): void;
  checkFile(): void;
  setMapping(mapping: ColumnMapping): void;
  validate(): void;
  chooseStrategy(strategy: ImportStrategy): void;
  goTo(step: number): void;
  execute(): void;
}

const row = (code: string, name = 'Gadget', status = 'Active') => [
  code,
  name,
  5,
  9.5,
  status,
  '2026-03-31',
  'a@example.com',
  'x',
];

// Real workbooks are parsed and generated here; the first ExcelJS load can be slow on a busy machine.
describe('ExcelImportWizardComponent', { timeout: 30_000 }, () => {
  let fixture: ComponentFixture<ExcelImportWizardComponent>;
  let wizard: WizardInternals;
  let store: GadgetStore;

  async function settle(done: () => boolean): Promise<void> {
    for (let i = 0; i < 200 && !done(); i++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      fixture.detectChanges();
    }
    fixture.detectChanges();
    expect(done()).toBe(true);
  }
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(async () => {
    store = new GadgetStore();
    TestBed.configureTestingModule({
      imports: [ExcelImportWizardComponent],
      providers: [
        provideRouter([]),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
        { provide: EXCEL_MODULES, useFactory: () => [registration(store)] },
        { provide: ExcelImportDataService, useExisting: ExcelImportMockService },
        { provide: ExcelExportDataService, useExisting: ExcelExportMockService },
        { provide: ExcelModuleCatalogDataService, useExisting: ExcelCatalogMockService },
      ],
    });
    fixture = TestBed.createComponent(ExcelImportWizardComponent);
    fixture.detectChanges();
    wizard = fixture.componentInstance as unknown as WizardInternals;
    await settle(() => wizard.moduleCode() !== null);
  });

  it('preselects the only importable module and offers the template download first', () => {
    expect(wizard.stepIndex()).toBe(0);
    expect(text()).toContain('Choose what to import');
    expect(text()).toContain('Download template');
    expect(text()).toContain('Gadget Code');
  });

  it('shows an honest message when no module can be imported', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ExcelImportWizardComponent],
      providers: [
        provideRouter([]),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
        { provide: ExcelImportDataService, useExisting: ExcelImportMockService },
        { provide: ExcelModuleCatalogDataService, useExisting: ExcelCatalogMockService },
      ],
    });
    fixture = TestBed.createComponent(ExcelImportWizardComponent);
    await settle(() => text().includes('No modules can be imported yet'));
  });

  it('walks through upload, validation, confirmation and the result', async () => {
    wizard.setFile(
      await workbookFile([HEADERS, row('G-100'), row('G-101', 'forbidden'), row('G-100', 'dup')]),
    );
    wizard.checkFile();
    await settle(() => wizard.inspection() !== null);
    expect(wizard.stepIndex()).toBe(2);
    expect(wizard.inspection()?.needsMapping).toBe(false);
    expect(text()).toContain('All columns match the template');

    wizard.validate();
    await settle(() => wizard.preview() !== null);
    expect(wizard.stepIndex()).toBe(3);
    expect(wizard.preview()).toMatchObject({ validRows: 1, invalidRows: 1 });
    expect(wizard.mode()).toBe('Partial'); // all-or-nothing is not offered when rows have problems
    expect(text()).toContain('2 row(s) have problems');
    expect(text()).toContain('That name is not allowed.');
    expect(text()).toContain('Duplicate of row 2');

    wizard.goTo(4);
    fixture.detectChanges();
    expect(text()).toContain('Confirm the import');
    expect(text()).toContain('Not available: some rows have problems.');

    wizard.execute();
    await settle(() => wizard.result() !== null);
    expect(wizard.stepIndex()).toBe(5);
    expect(wizard.result()).toMatchObject({ status: 'CompletedWithErrors', inserted: 1 });
    expect(text()).toContain('Import completed with some rows not imported');
    expect(store.items.map((g) => g.code)).toEqual(['G-001', 'G-100']);
  });

  it('defaults to all-or-nothing for a clean file', async () => {
    wizard.setFile(await workbookFile([HEADERS, row('G-200'), row('G-201')]));
    wizard.checkFile();
    await settle(() => wizard.inspection() !== null);
    wizard.validate();
    await settle(() => wizard.preview() !== null);
    expect(wizard.mode()).toBe('AllOrNothing');
    wizard.goTo(4);
    wizard.execute();
    await settle(() => wizard.result() !== null);
    expect(wizard.result()).toMatchObject({ status: 'Completed', inserted: 2 });
    expect(text()).toContain('Import completed');
  });

  it('asks for column mapping and blocks validation until it is complete', async () => {
    wizard.setFile(
      await workbookFile([
        ['Id', 'Label', 'State'],
        ['G-300', 'Thing', 'Active'],
      ]),
    );
    wizard.checkFile();
    await settle(() => wizard.inspection() !== null);
    expect(wizard.inspection()?.needsMapping).toBe(true);
    expect(wizard.mappingBlocked()).toBe(true);
    fixture.detectChanges();
    expect(text()).toContain('Some columns need your attention');
    expect(text()).toContain('The required field "Gadget Code" is not mapped to any column.');

    wizard.setMapping({ Id: 'code', Label: 'name', State: 'status' });
    expect(wizard.mappingBlocked()).toBe(false);
    wizard.validate();
    await settle(() => wizard.preview() !== null);
    expect(wizard.preview()?.validRows).toBe(1);
  });

  it('shows the reason when the file is refused', async () => {
    wizard.setFile(new File(['plain text'], 'fake.xlsx'));
    wizard.checkFile();
    await settle(() => wizard.uploadError() !== '');
    expect(wizard.stepIndex()).toBe(0); // stays on the upload step (index advances only on success)
    expect(wizard.uploadError()).toMatch(/does not match/);
    expect(wizard.inspection()).toBeNull();
  });

  it('applies the chosen strategy when validating', async () => {
    wizard.chooseStrategy('InsertOnly');
    wizard.setFile(await workbookFile([HEADERS, row('G-001'), row('G-400')]));
    wizard.checkFile();
    await settle(() => wizard.inspection() !== null);
    wizard.validate();
    await settle(() => wizard.preview() !== null);
    expect(wizard.preview()).toMatchObject({ validRows: 1, invalidRows: 0 });
    expect(text()).toContain('already exists');
  });
});
