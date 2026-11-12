import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { GeneralSettings } from '../models/settings.models';
import { setDisplayTimeZone, todayIso } from '../../shared/utilities/date.util';
import { AppSettingsService, DEFAULT_DISPLAY_SETTINGS } from './app-settings.service';
import { AuthService } from './auth.service';
import { CurrentUser } from './current-user.service';
import { GeneralSettingsDataService } from './data/settings-data.services';

const saved: GeneralSettings = {
  id: 1,
  organizationName: ' Example Org ',
  contactEmail: 'hr@example.test',
  timezone: 'Europe/London',
  dateFormat: 'dd/MM/yyyy',
  currencyCode: 'USD',
  fiscalYearStartMonth: 4,
  emailNotifications: true,
  weeklyDigest: false,
};

function setup(permissions: string[] | undefined, get: () => Observable<GeneralSettings>) {
  const session = signal<CurrentUser | null>({
    id: 1,
    employeeId: null,
    displayName: 'Fictional User',
    email: 'user@example.test',
    jobTitle: 'Tester',
    roles: [],
    permissions,
  });
  const getSpy = vi.fn(get);
  TestBed.configureTestingModule({
    providers: [
      {
        provide: AuthService,
        useValue: { session, isAuthenticated: computed(() => session() !== null) },
      },
      { provide: GeneralSettingsDataService, useValue: { get: getSpy } },
    ],
  });
  const service = TestBed.inject(AppSettingsService);
  TestBed.tick();
  return { service, getSpy, session };
}

describe('AppSettingsService', () => {
  afterEach(() => setDisplayTimeZone(null));

  it('starts with the seed defaults', () => {
    const { service } = setup(['employees:View'], () => of(saved));
    expect(service.settings()).toEqual(DEFAULT_DISPLAY_SETTINGS);
  });

  it('loads the saved settings once for a user who may view them', () => {
    const { service, getSpy, session } = setup(['settings:View'], () => of(saved));
    expect(getSpy).toHaveBeenCalledTimes(1);
    expect(service.organizationName()).toBe('Example Org');
    expect(service.formatDate('2025-03-07')).toBe('07/03/2025');
    expect(service.currencyCode()).toBe('USD');
    expect(service.fiscalYearStartMonth()).toBe(4);
    session.update((u) => ({ ...u!, jobTitle: 'Changed' }));
    TestBed.tick();
    expect(getSpy).toHaveBeenCalledTimes(1);
  });

  it('loads in the mock build, where the session has no permission list', () => {
    const { getSpy } = setup(undefined, () => of(saved));
    expect(getSpy).toHaveBeenCalledTimes(1);
  });

  it('makes no request, and keeps the defaults, without settings:View', () => {
    const { service, getSpy, session } = setup(['employees:View'], () => of(saved));
    session.update((u) => ({ ...u!, jobTitle: 'Changed' }));
    TestBed.tick();
    expect(getSpy).not.toHaveBeenCalled();
    expect(service.dateFormat()).toBe('yyyy-MM-dd');
  });

  it('keeps the defaults and does not retry when the read fails', () => {
    const { service, getSpy, session } = setup(['settings:View'], () =>
      throwError(() => new Error('403')),
    );
    session.update((u) => ({ ...u!, jobTitle: 'Changed' }));
    TestBed.tick();
    expect(getSpy).toHaveBeenCalledTimes(1);
    expect(service.settings()).toEqual(DEFAULT_DISPLAY_SETTINGS);
  });

  it('applies saved values and the time zone for today', () => {
    const { service } = setup(['employees:View'], () => of(saved));
    service.apply({ ...saved, timezone: 'Asia/Yangon', dateFormat: 'dd MMM yyyy' });
    expect(service.formatDate('2025-03-07')).toBe('07 Mar 2025');
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    service.apply({ dateFormat: 'MM/dd/yyyy' });
    expect(service.formatDate('2025-03-07')).toBe('03/07/2025');
  });

  it('returns to the defaults on sign-out', () => {
    const { service, session } = setup(['settings:View'], () => of(saved));
    expect(service.currencyCode()).toBe('USD');
    session.set(null);
    TestBed.tick();
    expect(service.settings()).toEqual(DEFAULT_DISPLAY_SETTINGS);
  });
});
