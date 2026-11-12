import { TestBed } from '@angular/core/testing';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../configuration/app-config';
import { AppSettingsService } from './app-settings.service';
import { PrintService } from './print.service';

function configure() {
  TestBed.configureTestingModule({
    providers: [
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, appName: 'Fallback HRMS' } },
    ],
  });
  return TestBed.inject(PrintService);
}

describe('PrintService', () => {
  afterEach(() => vi.restoreAllMocks());

  it('opens the browser print dialog', () => {
    const spy = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    configure().print();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('announces when the print dialog closes', () => {
    const service = configure();
    let count = 0;
    service.afterPrint$.subscribe(() => count++);
    window.dispatchEvent(new Event('afterprint'));
    expect(count).toBe(1);
  });

  it('uses the organization name from the app settings', () => {
    const service = configure();
    expect(service.organizationName()).toBe('APEX Group');
    TestBed.inject(AppSettingsService).apply({ organizationName: ' Example Org ' });
    expect(service.organizationName()).toBe('Example Org');
  });
});
