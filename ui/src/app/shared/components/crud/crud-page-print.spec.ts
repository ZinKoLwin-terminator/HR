import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { Entity } from '../../../core/models/common.models';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { CrudPageComponent } from './crud-page.component';
import { CrudPageConfig } from './crud.models';

interface Thing extends Entity {
  name: string;
  salary: number;
}

const rows: Thing[] = [
  { id: 1, name: 'Fictional One', salary: 1200 },
  { id: 2, name: 'Fictional Two', salary: 900 },
];

const service = {
  query: () => of({ items: rows, total: rows.length, pageIndex: 0, pageSize: 10 }),
} as unknown as CrudDataService<Thing>;

function config(extra: Partial<CrudPageConfig<Thing>> = {}): CrudPageConfig<Thing> {
  return {
    title: 'Things',
    description: 'Sample things',
    entityLabel: 'Thing',
    service,
    columns: [
      { key: 'name', header: 'Name' },
      { key: 'salary', header: 'Salary', type: 'number', align: 'end' },
    ],
    fields: [],
    newItem: () => ({}),
    nameOf: (row) => row.name,
    canCreate: false,
    hasStatus: false,
    ...extra,
  };
}

async function settle(fixture: ComponentFixture<unknown>) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
}

async function render(extra: Partial<CrudPageConfig<Thing>> = {}) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
    ],
  });
  const fixture = TestBed.createComponent(CrudPageComponent<Thing>);
  fixture.componentRef.setInput('config', config(extra));
  await settle(fixture);
  return fixture;
}

const printButton = (fixture: ComponentFixture<unknown>) =>
  (fixture.nativeElement as HTMLElement).querySelector('button[aria-label="Print Things"]');

describe('CrudPageComponent print action', () => {
  afterEach(() => vi.restoreAllMocks());

  it('offers Print by default', async () => {
    const fixture = await render();
    expect(printButton(fixture)?.textContent).toContain('Print');
  });

  it('hides Print when the screen is not printable', async () => {
    const fixture = await render({ printable: false });
    expect(printButton(fixture)).toBeNull();
  });

  it('does not offer Print inside an embedded list unless asked', async () => {
    expect(printButton(await render({ embedded: true }))).toBeNull();
    TestBed.resetTestingModule();
    expect(printButton(await render({ embedded: true, printable: true }))).not.toBeNull();
  });

  it('lays out a plain table of the rows and opens the print dialog', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const fixture = await render();
    (fixture.componentInstance as unknown as { printCurrent(): void }).printCurrent();
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(print).toHaveBeenCalledTimes(1);
    const table = el.querySelector('.print-only table.print-table');
    expect(table?.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(table?.textContent).toContain('Fictional One');
    expect(table?.querySelector('th:last-child')?.textContent).toContain('Salary');
    expect(table?.textContent).not.toContain('Actions');
    // The on-screen table is not printed while the print layout is on the page.
    expect(el.querySelector('app-data-table')?.classList).toContain('no-print');
  });

  it('removes the print layout after the print dialog closes', async () => {
    vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const fixture = await render();
    (fixture.componentInstance as unknown as { printCurrent(): void }).printCurrent();
    await settle(fixture);
    window.dispatchEvent(new Event('afterprint'));
    await settle(fixture);
    expect((fixture.nativeElement as HTMLElement).querySelector('.print-only')).toBeNull();
  });
});
