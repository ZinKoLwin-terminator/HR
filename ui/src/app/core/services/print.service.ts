import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { AppSettingsService } from './app-settings.service';

/**
 * Browser printing. The user prints or saves as PDF from the browser's own print dialog; nothing is
 * generated on a server. Screens render their printable content (see the `.print-only` and
 * `.no-print` utilities in `styles.scss`) and call {@link print}.
 */
@Injectable({ providedIn: 'root' })
export class PrintService {
  private readonly settings = inject(AppSettingsService);

  /** Name for the print header: the organization name from the general settings. */
  readonly organizationName = this.settings.organizationName;

  /** Emits when the print dialog closes (printed or cancelled), so screens can drop print-only content. */
  readonly afterPrint$ = new Subject<void>();

  constructor() {
    window.addEventListener('afterprint', () => this.afterPrint$.next());
  }

  /** Opens the browser print dialog. A thin wrapper so tests can observe it. */
  print(): void {
    window.print();
  }
}
