import { Component, Injectable, inject, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';

export type HelpLang = 'en' | 'my';

/** Shared so the chosen language stays when moving between the Help pages. */
@Injectable({ providedIn: 'root' })
export class HelpLanguageService {
  readonly lang = signal<HelpLang>('en');
}

/** Language switch shown in the header of every Help page. */
@Component({
  selector: 'app-help-tabs',
  imports: [MatButtonToggleModule],
  template: `
    <mat-button-toggle-group
      aria-label="Language"
      [value]="lang()"
      (change)="language.lang.set($event.value)"
      hideSingleSelectionIndicator
    >
      <mat-button-toggle value="en">English</mat-button-toggle>
      <mat-button-toggle value="my">မြန်မာ</mat-button-toggle>
    </mat-button-toggle-group>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class HelpTabsComponent {
  protected readonly language = inject(HelpLanguageService);
  protected readonly lang = this.language.lang;
}
