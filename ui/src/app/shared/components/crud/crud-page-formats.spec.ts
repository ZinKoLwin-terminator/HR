import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { Entity } from '../../../core/models/common.models';
import { AppSettingsService } from '../../../core/services/app-settings.service';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { setDisplayTimeZone } from '../../utilities/date.util';
import { CrudPageComponent } from './crud-page.component';
import { CrudPageConfig } from './crud.models';

interface Thing extends Entity {
  name: string;
  startDate: string;
  salary: number;
}

const rows: Thing[] = [{ id: 1, name: 'Fictional One', startDate: '2025-03-07', salary: 1200 }];

const service = {
  query: () => of({ items: rows, total: rows.length, pageIndex: 0, pageSize: 10 }),
} as unknown as CrudDataService<Thing>;

const config: CrudPageConfig<Thing> = {
  title: 'Things',
  description: 'Sample things',
  entityLabel: 'Thing',
  service,
  columns: [
    { key: 'name', header: 'Name' },
    { key: 'startDate', header: 'Start', type: 'date' },
    { key: 'salary', header: 'Salary', type: 'currency', align: 'end' },
  ],
  fields: [],
  newItem: () => ({}),
  nameOf: (row) => row.name,
  canCreate: false,
  hasStatus: false,
};

async function settle(fixture: ComponentFixture<unknown>) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
}

describe('CrudPageComponent display settings', () => {
  afterEach(() => setDisplayTimeZone(null));

  it('shows dates and amounts in the configured format and currency', async () => {
    TestBed.configureTestingModule({
      imports: [MatDialogModule],
      providers: [
        provideRouter([]),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    const fixture = TestBed.createComponent(CrudPageComponent<Thing>);
    fixture.componentRef.setInput('config', config);
    await settle(fixture);
    const text = () =>
      ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s/g, ' ');
    expect(text()).toContain('2025-03-07');
    expect(text()).toContain('MMK 1,200');

    TestBed.inject(AppSettingsService).apply({ dateFormat: 'dd MMM yyyy', currencyCode: 'USD' });
    fixture.detectChanges();
    expect(text()).toContain('07 Mar 2025');
    expect(text()).toContain('$1,200');
    expect(text()).not.toContain('MMK');
  });
});
