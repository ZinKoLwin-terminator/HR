import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { ExcelModuleInfo } from '../../../core/models/excel.models';
import { ExcelModuleCatalogDataService } from '../../../core/services/data/excel-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { ExcelActionsComponent } from './excel-actions.component';
import { ExcelExportService } from './excel-export.service';

const info = (code: string, canImport: boolean, canExport: boolean): ExcelModuleInfo => ({
  code,
  label: code,
  sheetName: code,
  description: '',
  columns: [],
  matchKey: [],
  matchKeyLabel: '',
  canImport,
  canExport,
});

describe('ExcelActionsComponent', () => {
  let fixture: ComponentFixture<ExcelActionsComponent>;
  let granted: Set<string>;
  const opened: unknown[] = [];

  function create(
    code: string,
    allowImport = true,
    modules: ExcelModuleInfo[] = [],
    keys = ['excel:Import', 'excel:Export'],
  ) {
    granted = new Set(keys);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ExcelModuleCatalogDataService, useValue: { getModules: () => of(modules) } },
        { provide: PermissionService, useValue: { can: (key: string) => granted.has(key) } },
        {
          provide: ExcelExportService,
          useValue: { open: (data: unknown) => (opened.push(data), of(true)) },
        },
      ],
    });
    fixture = TestBed.createComponent(ExcelActionsComponent);
    fixture.componentRef.setInput('moduleCode', code);
    fixture.componentRef.setInput('allowImport', allowImport);
    fixture.componentRef.setInput('filters', { departmentId: 3 });
    fixture.detectChanges();
    return fixture;
  }
  const labels = () =>
    [...(fixture.nativeElement as HTMLElement).querySelectorAll('button, a')].map((e) =>
      e.textContent?.replace(/\s+/g, ' ').trim(),
    );

  it('shows Export and Import when the catalog allows both and the role holds both keys', async () => {
    create('departments', true, [info('departments', true, true)]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(labels()).toEqual(['downloadExport', 'upload_fileImport']);
  });

  it('hides Import for an export-only module and Export when the catalog refuses it', async () => {
    create('leave-balances', true, [info('leave-balances', false, true), info('x', true, false)]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(labels()).toEqual(['downloadExport']);
  });

  it('shows nothing for a module that is not in the catalog', async () => {
    create('departments', true, [info('companies', true, true)]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(labels()).toEqual([]);
  });

  it('follows the excel:Import and excel:Export permission keys', async () => {
    create('departments', true, [info('departments', true, true)], ['excel:Export']);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(labels()).toEqual(['downloadExport']);
  });

  it('hides Import on a screen that only exports even if the module can import', async () => {
    create('attendance-records', false, [info('attendance-records', true, true)]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(labels()).toEqual(['downloadExport']);
  });

  it('opens the export dialog with the module code and the applied filters', async () => {
    create('departments', true, [info('departments', true, true)]);
    await fixture.whenStable();
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')!.click();
    expect(opened.at(-1)).toMatchObject({
      moduleCode: 'departments',
      filters: { departmentId: 3 },
    });
  });
});
