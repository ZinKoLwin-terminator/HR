import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { AppError } from '../../../core/models/common.models';
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
    keys = ['excel:View', 'excel:Import', 'excel:Export'],
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
    create('departments', true, [info('departments', true, true)], ['excel:View', 'excel:Export']);
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

describe('ExcelActionsComponent without Excel access or with a failing catalog', () => {
  function setup(
    keys: string[],
    getModules: () => Observable<ExcelModuleInfo[]>,
    allowImport = true,
  ) {
    const granted = new Set(keys);
    let calls = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ExcelModuleCatalogDataService,
          useValue: { getModules: () => (calls++, getModules()) },
        },
        { provide: PermissionService, useValue: { can: (key: string) => granted.has(key) } },
        { provide: ExcelExportService, useValue: { open: () => of(true) } },
      ],
    });
    const fixture = TestBed.createComponent(ExcelActionsComponent);
    fixture.componentRef.setInput('moduleCode', 'departments');
    fixture.componentRef.setInput('allowImport', allowImport);
    fixture.detectChanges();
    return { fixture, calls: () => calls };
  }

  it('does not request the catalog when the role can neither export nor import', async () => {
    const { fixture, calls } = setup(['excel:View'], () => of([info('departments', true, true)]));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(calls()).toBe(0);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('button, a')).toHaveLength(0);
  });

  it('does not request the catalog without excel:View (the API requires it)', async () => {
    const { fixture, calls } = setup(['excel:Export', 'excel:Import'], () =>
      of([info('departments', true, true)]),
    );
    await fixture.whenStable();
    fixture.detectChanges();
    expect(calls()).toBe(0);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('button, a')).toHaveLength(0);
  });

  it('does not request it for an import-only role on a screen that only exports', async () => {
    const { fixture, calls } = setup(['excel:View', 'excel:Import'], () => of([]), false);
    await fixture.whenStable();
    expect(calls()).toBe(0);
  });

  it('shows no buttons and does not throw when the catalog answers 403', async () => {
    const forbidden: AppError = {
      code: 'Forbidden',
      message: 'You do not have permission to do that.',
    };
    const { fixture, calls } = setup(['excel:View', 'excel:Export', 'excel:Import'], () =>
      throwError(() => forbidden),
    );
    await fixture.whenStable();
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(calls()).toBe(1);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('button, a')).toHaveLength(0);
  });
});
