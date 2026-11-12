import { Injectable, inject, signal } from '@angular/core';
import { Subject, catchError, of } from 'rxjs';
import { APP_CONFIG } from '../configuration/app-config';
import { GeneralSettingsDataService } from './data/settings-data.services';

/**
 * Browser printing. The user prints or saves as PDF from the browser's own print dialog; nothing is
 * generated on a server. Screens render their printable content (see the `.print-only` and
 * `.no-print` utilities in `styles.scss`) and call {@link print}.
 */
@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly appConfig = inject(APP_CONFIG);
  private readonly settings = inject(GeneralSettingsDataService, { optional: true });
  private requested = false;

  /** Name for the print header: the organization name from the general settings, else the app name. */
  readonly organizationName = signal(this.appConfig.appName);

  /** Emits when the print dialog closes (printed or cancelled), so screens can drop print-only content. */
  readonly afterPrint$ = new Subject<void>();

  constructor() {
    window.addEventListener('afterprint', () => this.afterPrint$.next());
  }

  /** Opens the browser print dialog. A thin wrapper so tests can observe it. */
  print(): void {
    window.print();
  }

  /** Reads the organization name once (any signed-in user may read it); keeps the fallback on error. */
  loadOrganizationName(): void {
    if (this.requested) {
      return;
    }
    this.requested = true;
    this.settings
      ?.get()
      .pipe(catchError(() => of(null)))
      .subscribe((general) => {
        const name = general?.organizationName?.trim();
        if (name) {
          this.organizationName.set(name);
        }
      });
  }
}
