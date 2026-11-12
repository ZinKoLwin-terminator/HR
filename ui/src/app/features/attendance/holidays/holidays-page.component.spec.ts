import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { Holiday } from '../../../core/models/attendance.models';
import { CurrentUser, CurrentUserService } from '../../../core/services/current-user.service';
import { HolidayDataService } from '../../../core/services/data/attendance-data.services';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { HolidaysPageComponent } from './holidays-page.component';

function user(permissions?: string[]): CurrentUser {
  return {
    id: 1,
    employeeId: null,
    displayName: 'Test User',
    email: 'test@example.test',
    jobTitle: '',
    roles: ['Tester'],
    permissions,
  };
}

function setup(permissions?: string[]) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user(permissions)) } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { data: { permission: 'attendance:View' }, parent: null } },
      },
    ],
  });
  return TestBed.createComponent(HolidaysPageComponent);
}

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
}

describe('HolidaysPageComponent', () => {
  it('describes the calendar, its leave and working day effect and its permission keys', async () => {
    const fixture = setup();
    await settle(fixture);
    const crud = fixture.debugElement.query(By.directive(CrudPageComponent))
      .componentInstance as CrudPageComponent<Holiday>;
    const config = crud.config() as CrudPageConfig<Holiday>;
    expect(config.permissionKeys).toEqual({ Create: 'attendance:Edit' });
    expect(config.description).toContain('leave day counts and working days');
    expect(config.fields.map((f) => f.key)).toEqual(['date', 'name', 'type']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Independence Day');
  });

  it('offers Add and Edit with attendance:Edit, and Delete only with attendance:Delete', async () => {
    const result = async (permissions: string[]) => {
      const fixture = setup(permissions);
      await settle(fixture);
      const crud = fixture.debugElement.query(By.directive(CrudPageComponent))
        .componentInstance as { actions(): { id: string }[] };
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      return { ids: crud.actions().map((a) => a.id), add: text.includes('Add holiday') };
    };
    expect(await result(['attendance:View', 'attendance:Create'])).toEqual({
      ids: ['view'],
      add: false,
    });
    TestBed.resetTestingModule();
    expect(await result(['attendance:View', 'attendance:Edit'])).toEqual({
      ids: ['view', 'edit'],
      add: true,
    });
    TestBed.resetTestingModule();
    const all = await result(['attendance:View', 'attendance:Edit', 'attendance:Delete']);
    expect(all.ids).toEqual(['view', 'edit', 'delete']);
  });

  it('creates, refuses a second holiday on the same date, and deletes through the mock', async () => {
    setup();
    const service = TestBed.inject(HolidayDataService);
    const created = await firstValueFrom(
      service.create({ date: '2027-03-02', name: 'Fictional Day', type: 'Company' }),
    );
    expect(created.id).toBeGreaterThan(0);
    expect((await firstValueFrom(service.getAll())).some((h) => h.name === 'Fictional Day')).toBe(
      true,
    );
    await expect(
      firstValueFrom(service.create({ date: '2027-03-02', name: 'Another', type: 'Public' })),
    ).rejects.toMatchObject({ code: 'Validation' });
    await firstValueFrom(service.delete(created.id));
    expect((await firstValueFrom(service.getAll())).some((h) => h.id === created.id)).toBe(false);
  });
});
