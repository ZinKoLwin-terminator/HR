import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../core/configuration/app-config';
import { DemoDataDataService } from '../core/services/data/demo-data-data.service';
import { CompanyDataService } from '../core/services/data/organization-data.services';
import { UserDataService } from '../core/services/data/settings-data.services';
import { provideMockDataServices } from './provide-mock-data-services';

const NEW_COMPANY = {
  code: 'DEMO',
  name: 'Demo Trading Co.',
  legalName: 'Demo Trading Co., Ltd.',
  registrationNumber: 'REG-0001',
  taxId: 'TAX-0001',
  industry: 'Trading',
  email: 'info@demo.example',
  phone: '+95 1 555 0100',
  website: 'https://demo.example',
  address: '1 Sample Road',
  city: 'Yangon',
  country: 'Myanmar',
  foundedDate: '2020-01-01',
  status: 'Active' as const,
};

/** Starts a fresh application instance, as a page reload would. */
function startApp(options: Parameters<typeof provideMockDataServices>[0]): void {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(options),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
    ],
  });
}

describe('data persistence and reset', () => {
  beforeEach(() => localStorage.clear());

  afterEach(() => {
    localStorage.clear();
    provideMockDataServices(); // restore the defaults for anything that runs later
  });

  it('keeps created and updated records across a reload and removes deleted ones', async () => {
    startApp({ emptyData: true, persist: true });
    const created = await firstValueFrom(TestBed.inject(CompanyDataService).create(NEW_COMPANY));
    await firstValueFrom(
      TestBed.inject(CompanyDataService).update(created.id, { name: 'Renamed Co.' }),
    );
    TestBed.tick(); // flush the save effects

    startApp({ emptyData: true, persist: true });
    const afterReload = await firstValueFrom(TestBed.inject(CompanyDataService).getAll());
    expect(afterReload.map((c) => c.name)).toEqual(['Renamed Co.']);

    await firstValueFrom(TestBed.inject(CompanyDataService).delete(created.id));
    TestBed.tick();
    startApp({ emptyData: true, persist: true });
    expect(await firstValueFrom(TestBed.inject(CompanyDataService).getAll())).toEqual([]);
  });

  it('starts from the seed again when persistence is off', async () => {
    startApp({ emptyData: true, persist: true });
    await firstValueFrom(TestBed.inject(CompanyDataService).create(NEW_COMPANY));
    TestBed.tick();

    startApp({ emptyData: true, persist: false });
    expect(await firstValueFrom(TestBed.inject(CompanyDataService).getAll())).toEqual([]);
    expect(TestBed.inject(DemoDataDataService).canReset).toBe(false);
  });

  it('reset discards everything, including stores not loaded yet', async () => {
    startApp({ emptyData: true, persist: true });
    await firstValueFrom(TestBed.inject(CompanyDataService).create(NEW_COMPANY));
    TestBed.tick();
    expect(Object.keys(localStorage).some((k) => k.includes('companies'))).toBe(true);

    // Fresh page: only the users store is loaded when the user presses Reset.
    startApp({ emptyData: true, persist: true });
    const demo = TestBed.inject(DemoDataDataService);
    expect(demo.canReset).toBe(true);
    await firstValueFrom(TestBed.inject(UserDataService).getAll());
    await firstValueFrom(demo.reset());
    TestBed.tick();

    // The companies store was never loaded above, yet its saved copy must be gone.
    startApp({ emptyData: true, persist: true });
    expect(await firstValueFrom(TestBed.inject(CompanyDataService).getAll())).toEqual([]);
  });

  it('puts live stores back to the seed on reset', async () => {
    startApp({ emptyData: true, persist: true });
    const companies = TestBed.inject(CompanyDataService);
    await firstValueFrom(companies.create(NEW_COMPANY));
    await firstValueFrom(TestBed.inject(DemoDataDataService).reset());
    expect(await firstValueFrom(companies.getAll())).toEqual([]);
  });
});
