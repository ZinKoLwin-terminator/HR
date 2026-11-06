import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { MODULE_GUIDE } from '../../core/constants/module-guide';
import { SCREEN_DETAILS } from '../../core/constants/screen-details';
import { HelpLanguageService, HelpTabsComponent } from './help-tabs.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

const LABELS = {
  en: {
    description: 'What each module does, its screens and the rules the system enforces.',
    screens: 'Screens',
    rules: 'Good to know',
    open: 'Open',
    note: 'Limitation in this version',
    flows: 'See how the modules connect and the step-by-step workflows',
  },
  my: {
    description: 'Module တစ်ခုချင်းစီ၏ လုပ်ဆောင်ချက်၊ Screen များနှင့် စနစ်၏ စည်းမျဉ်းများ။',
    screens: 'Screen များ',
    rules: 'သိထားသင့်သည်များ',
    open: 'ဖွင့်ရန်',
    note: 'ဤ Version ၏ ကန့်သတ်ချက်',
    flows: 'Module များ ဆက်စပ်ပုံနှင့် အဆင့်ဆင့် Workflow များကို ကြည့်ရန်',
  },
} as const;

/** In-app guide that explains every module to the people using the system. */
@Component({
  selector: 'app-module-guide',
  imports: [RouterLink, MatIconModule, PageHeaderComponent, HelpTabsComponent],
  template: `
    <app-page-header title="Help" [description]="t().description">
      <app-help-tabs pageActions />
    </app-page-header>

    <section class="module" [attr.aria-labelledby]="'module-title'">
      <header class="module__head">
        <span class="module__icon" aria-hidden="true"
          ><mat-icon>{{ current().icon }}</mat-icon></span
        >
        <div>
          <h2 id="module-title">{{ current().name }}</h2>
          <p>{{ current().summary[lang()] }}</p>
        </div>
      </header>

      <h3>{{ t().screens }}</h3>
      <ul class="screens">
        @for (s of current().screens; track s.route) {
          <li>
            <div class="screen__head">
              <a [routerLink]="s.route" [attr.aria-label]="t().open + ' ' + s.label">
                {{ s.label }}<mat-icon aria-hidden="true">arrow_forward</mat-icon>
              </a>
              <span class="screen__purpose">{{ s.text[lang()] }}</span>
            </div>
            @if (details[s.route]; as items) {
              <ul class="screen__details">
                @for (d of items; track d.en) {
                  <li>{{ d[lang()] }}</li>
                }
              </ul>
            }
          </li>
        }
      </ul>

      <h3>{{ t().rules }}</h3>
      <ul class="rules">
        @for (r of current().rules; track r.en) {
          <li>{{ r[lang()] }}</li>
        }
      </ul>

      @if (current().note; as note) {
        <p class="note" role="note">
          <mat-icon aria-hidden="true">info</mat-icon>
          <span
            ><strong>{{ t().note }}:</strong> {{ note[lang()] }}</span
          >
        </p>
      }
    </section>

    <a class="flows-link" routerLink="/help/workflows">
      <mat-icon aria-hidden="true">account_tree</mat-icon>{{ t().flows }}
      <mat-icon aria-hidden="true">arrow_forward</mat-icon>
    </a>
  `,
  styles: `
    :host {
      display: block;
    }
    .flows-link {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      margin-top: var(--app-spacing-lg, 20px);
      color: var(--mat-sys-primary);
      font-weight: 500;
      text-decoration: none;
    }
    .flows-link:hover,
    .flows-link:focus-visible {
      text-decoration: underline;
    }
    .module {
      padding: var(--app-spacing-lg, 20px);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 16px;
      background: var(--mat-sys-surface-container-lowest, var(--mat-sys-surface));
    }
    .module__head {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .module__icon {
      display: grid;
      place-items: center;
      flex: none;
      width: 48px;
      height: 48px;
      border-radius: 14px;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .module__head h2 {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    .module__head p {
      margin: 2px 0 0;
      line-height: 1.7;
      color: var(--mat-sys-on-surface-variant);
    }
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }
    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .screens > li {
      padding: 12px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .screen__head {
      display: grid;
      grid-template-columns: 220px 1fr;
      gap: 12px;
      align-items: baseline;
    }
    .screen__head a {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .screen__head a mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .screen__purpose {
      font-weight: 500;
      line-height: 1.7;
    }
    .screen__details {
      margin: 6px 0 0 232px;
      padding: 0;
      list-style: none;
      color: var(--mat-sys-on-surface-variant);
    }
    .screen__details li {
      position: relative;
      padding: 3px 0 3px 16px;
      line-height: 1.75;
    }
    .screen__details li::before {
      content: '–';
      position: absolute;
      left: 2px;
    }
    .rules li {
      position: relative;
      padding: 4px 0 4px 18px;
    }
    .rules li::before {
      content: '•';
      position: absolute;
      left: 4px;
    }
    .note {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      margin: 16px 0 0;
      padding: 12px;
      border-radius: 12px;
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .note mat-icon {
      flex: none;
    }
    @media (max-width: 700px) {
      .screen__head {
        grid-template-columns: 1fr;
        gap: 2px;
      }
      .screen__details {
        margin-left: 0;
      }
    }
  `,
})
export class ModuleGuideComponent {
  protected readonly modules = MODULE_GUIDE;
  protected readonly lang = inject(HelpLanguageService).lang;
  protected readonly t = computed(() => LABELS[this.lang()]);
  protected readonly details = SCREEN_DETAILS;
  /** Module shown on the page: /help/modules?module=payroll (defaults to the first). */
  private readonly selectedId = toSignal(
    inject(ActivatedRoute).queryParamMap.pipe(map((p) => p.get('module'))),
    { initialValue: null },
  );
  protected readonly current = computed(
    () => MODULE_GUIDE.find((m) => m.id === this.selectedId()) ?? MODULE_GUIDE[0],
  );
}
