import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { catchError, of } from 'rxjs';
import { APP_CONFIG } from '../configuration/app-config';
import { GeneralSettings } from '../models/settings.models';
import { setDisplayTimeZone } from '../../shared/utilities/date.util';
import { formatDisplayDate } from '../../shared/utilities/format.util';
import { AuthService } from './auth.service';
import { GeneralSettingsDataService } from './data/settings-data.services';
import { PermissionService } from './permission.service';

/** The settings the UI applies. Matches the seed data. */
export type DisplaySettings = Pick<
  GeneralSettings,
  'organizationName' | 'timezone' | 'dateFormat' | 'currencyCode' | 'fiscalYearStartMonth'
>;

export const DEFAULT_DISPLAY_SETTINGS: DisplaySettings = {
  organizationName: 'APEX Group',
  timezone: 'Asia/Yangon',
  dateFormat: 'yyyy-MM-dd',
  currencyCode: 'MMK',
  fiscalYearStartMonth: 1,
};

/**
 * The saved general settings as the rest of the UI sees them: a signal with safe defaults, loaded
 * once after sign-in and updated when the General settings screen saves.
 *
 * API build: `GET /api/general-settings` needs `settings:View`, which most roles do not hold. Such
 * users keep the defaults and no request is made, so there is no 403 noise. A public "display
 * settings" endpoint would be needed for them to see the saved values (see docs/FUTURE-TODO.md).
 * A failed read is not retried either; the defaults stay.
 *
 * Display only: stored dates stay ISO and amounts stay numbers.
 */
@Injectable({ providedIn: 'root' })
export class AppSettingsService {
  private readonly api = inject(GeneralSettingsDataService, { optional: true });
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly config = inject(APP_CONFIG);
  private requested = false;

  private readonly state = signal<DisplaySettings>({
    ...DEFAULT_DISPLAY_SETTINGS,
    organizationName: this.config.organizationName,
    currencyCode: this.config.currency,
  });

  readonly settings = this.state.asReadonly();
  readonly organizationName = computed(() => this.state().organizationName);
  readonly dateFormat = computed(() => this.state().dateFormat);
  readonly timezone = computed(() => this.state().timezone);
  readonly currencyCode = computed(() => this.state().currencyCode);
  readonly fiscalYearStartMonth = computed(() => this.state().fiscalYearStartMonth);

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        untracked(() => this.reset());
        return;
      }
      const allowed = this.permissions.can('settings:View');
      untracked(() => {
        if (allowed) this.load();
      });
    });
  }

  /** Formats a date (ISO string, timestamp or `Date`) with the configured format and time zone. */
  formatDate(value: string | number | Date | null | undefined): string {
    return formatDisplayDate(value, this.state().dateFormat, this.state().timezone);
  }

  /** Uses saved settings, e.g. the result of a successful save on the General settings screen. */
  apply(values: Partial<DisplaySettings>): void {
    const next: DisplaySettings = { ...this.state() };
    const name = values.organizationName?.trim();
    if (name) next.organizationName = name;
    if (values.timezone) next.timezone = values.timezone;
    if (values.dateFormat) next.dateFormat = values.dateFormat;
    if (values.currencyCode) next.currencyCode = values.currencyCode;
    if (values.fiscalYearStartMonth) next.fiscalYearStartMonth = values.fiscalYearStartMonth;
    this.state.set(next);
    setDisplayTimeZone(next.timezone);
  }

  /** Reads the settings once per session; a failure keeps the defaults. */
  private load(): void {
    if (this.requested || !this.api) return;
    this.requested = true;
    this.api
      .get()
      .pipe(catchError(() => of(null)))
      .subscribe((general) => {
        if (general) this.apply(general);
      });
  }

  private reset(): void {
    this.requested = false;
    this.state.set({
      ...DEFAULT_DISPLAY_SETTINGS,
      organizationName: this.config.organizationName,
      currencyCode: this.config.currency,
    });
    setDisplayTimeZone(null);
  }
}
