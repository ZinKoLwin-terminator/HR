import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { Entity } from '../../../core/models/common.models';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { CrudPageComponent } from './crud-page.component';
import { CrudPageConfig } from './crud.models';

interface Thing extends Entity {
  name: string;
  status: 'Pending' | 'Approved';
}

const pending: Thing = { id: 1, name: 'Fictional pending', status: 'Pending' };
const approved: Thing = { id: 2, name: 'Fictional approved', status: 'Approved' };

const service = {
  query: () => of({ items: [pending, approved], total: 2, pageIndex: 0, pageSize: 10 }),
} as unknown as CrudDataService<Thing>;

function config(extra: Partial<CrudPageConfig<Thing>> = {}): CrudPageConfig<Thing> {
  return {
    title: 'Things',
    description: 'Sample things',
    entityLabel: 'Thing',
    service,
    columns: [{ key: 'name', header: 'Name' }],
    fields: [{ key: 'name', label: 'Name', type: 'text' }],
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

/** Opens the details dialog for a row and returns the labels of its action buttons. */
async function detailsButtons(row: Thing, extra: Partial<CrudPageConfig<Thing>> = {}) {
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
  (fixture.componentInstance as unknown as { openDetails(row: Thing): void }).openDetails(row);
  await settle(fixture);
  const container = document.querySelector('mat-dialog-actions');
  const labels = [...(container?.querySelectorAll('button') ?? [])].map((b) =>
    (b.textContent ?? '').replace('edit', '').trim(),
  );
  TestBed.inject(MatDialog).closeAll();
  await settle(fixture);
  return labels;
}

describe('CrudPageComponent details dialog', () => {
  const pendingOnly = { canEdit: (row: Thing) => row.status === 'Pending' };

  it('offers Edit for a row the config lets the user edit', async () => {
    expect(await detailsButtons(pending, pendingOnly)).toEqual(['Close', 'Edit']);
  });

  it('does not offer Edit for a row whose canEdit is false, like the row menu', async () => {
    expect(await detailsButtons(approved, pendingOnly)).toEqual(['Close']);
  });

  it('offers Edit for every row when the config has no canEdit', async () => {
    expect(await detailsButtons(approved)).toEqual(['Close', 'Edit']);
  });

  it('does not offer Edit on a list whose rows are never edited', async () => {
    expect(await detailsButtons(pending, { editable: false })).toEqual(['Close']);
  });
});
