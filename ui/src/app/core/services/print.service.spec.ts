import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../configuration/app-config';
import { GeneralSettings } from '../models/settings.models';
import { GeneralSettingsDataService } from './data/settings-data.services';
import { PrintService } from './print.service';

function configure(settings: Partial<GeneralSettingsDataService> | null) {
  TestBed.configureTestingModule({
    providers: [
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, appName: 'Fallback HRMS' } },
      ...(settings ? [{ provide: GeneralSettingsDataService, useValue: settings }] : []),
    ],
  });
  return TestBed.inject(PrintService);
}

describe('PrintService', () => {
  afterEach(() => vi.restoreAllMocks());

  it('opens the browser print dialog', () => {
    const spy = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    configure(null).print();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('announces when the print dialog closes', () => {
    const service = configure(null);
    let count = 0;
    service.afterPrint$.subscribe(() => count++);
    window.dispatchEvent(new Event('afterprint'));
    expect(count).toBe(1);
  });

  it('uses the organization name from the general settings, and the app name until then', () => {
    const get = vi.fn(() => of({ organizationName: ' Example Org ' } as GeneralSettings));
    const service = configure({ get });
    expect(service.organizationName()).toBe('Fallback HRMS');
    service.loadOrganizationName();
    service.loadOrganizationName();
    expect(service.organizationName()).toBe('Example Org');
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('keeps the app name when the settings cannot be read', () => {
    const service = configure({ get: () => throwError(() => new Error('403')) });
    service.loadOrganizationName();
    expect(service.organizationName()).toBe('Fallback HRMS');
  });
});
