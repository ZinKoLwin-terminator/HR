import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { AppError, Entity, PagedResult } from '../../../core/models/common.models';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { ExcelModuleCatalogDataService } from '../../../core/services/data/excel-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { CrudPageComponent } from './crud-page.component';
import { CrudPageConfig } from './crud.models';

interface Thing extends Entity {
  name: string;
}

const FORBIDDEN: AppError = {
  code: 'Forbidden',
  message: 'You do not have permission to do that.',
};

function config(service: CrudDataService<Thing>): CrudPageConfig<Thing> {
  return {
    title: 'Things',
    description: 'Sample things',
    entityLabel: 'Thing',
    service,
    excel: { moduleCode: 'things' },
    columns: [{ key: 'name', header: 'Name' }],
    fields: [{ key: 'name', label: 'Name', type: 'text' }],
    newItem: () => ({}),
    nameOf: (row) => row.name,
    hasStatus: false,
  };
}

async function settle(fixture: ComponentFixture<unknown>) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
}

function setup(query: () => ReturnType<CrudDataService<Thing>['query']>) {
  let catalogCalls = 0;
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      // A role without any excel key (for example a Department Manager).
      {
        provide: PermissionService,
        useValue: { can: (key: string) => !key.startsWith('excel:') },
      },
      {
        provide: ExcelModuleCatalogDataService,
        useValue: { getModules: () => (catalogCalls++, throwError(() => FORBIDDEN)) },
      },
    ],
  });
  const fixture = TestBed.createComponent(CrudPageComponent<Thing>);
  fixture.componentRef.setInput('config', config({ query } as unknown as CrudDataService<Thing>));
  return { fixture, catalogCalls: () => catalogCalls };
}

describe('CrudPageComponent error state', () => {
  it('shows the server message and Try again when the list answers 403', async () => {
    const { fixture, catalogCalls } = setup(() => throwError(() => FORBIDDEN));
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const empty = el.querySelector('app-empty-state');
    expect(empty?.textContent).toContain('Things could not be loaded');
    expect(empty?.textContent).toContain('You do not have permission to do that.');
    expect(empty?.querySelector('button')?.textContent).toContain('Try again');
    expect(el.querySelector('app-data-table')).toBeNull();
    // No excel key: the catalog is never asked for.
    expect(catalogCalls()).toBe(0);
  });

  it('Try again reloads and shows the rows once the list can be read', async () => {
    let fail = true;
    const page: PagedResult<Thing> = {
      items: [{ id: 1, name: 'Fictional thing' }],
      total: 1,
      pageIndex: 0,
      pageSize: 10,
    };
    const { fixture } = setup(() => (fail ? throwError(() => FORBIDDEN) : of(page)));
    await settle(fixture);
    fail = false;
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('app-empty-state button')!
      .click();
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-empty-state')?.textContent ?? '').not.toContain(
      'could not be loaded',
    );
    expect(el.textContent).toContain('Fictional thing');
  });

  it('keeps the loaded rows and shows an error banner when a later reload fails', async () => {
    const answers = new Subject<PagedResult<Thing>>();
    let calls = 0;
    const rows = (name: string) =>
      of({ items: [{ id: 1, name }], total: 1, pageIndex: 0, pageSize: 10 });
    const { fixture } = setup(() => {
      calls++;
      return calls === 1 ? rows('Fictional kept') : calls === 2 ? answers : rows('Fictional again');
    });
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).toBeNull();

    (fixture.componentInstance as unknown as { refresh(): void }).refresh();
    answers.error(FORBIDDEN);
    await settle(fixture);
    expect(el.textContent).toContain('Fictional kept');
    const banner = el.querySelector('app-error-banner [role="alert"]');
    expect(banner?.textContent).toContain(FORBIDDEN.message);
    const retry = banner?.querySelector<HTMLButtonElement>('button');
    expect(retry?.textContent).toContain('Try again');

    retry!.click();
    await settle(fixture);
    expect(calls).toBe(3);
    expect(el.querySelector('app-error-banner')).toBeNull();
    expect(el.textContent).toContain('Fictional again');
  });
});
