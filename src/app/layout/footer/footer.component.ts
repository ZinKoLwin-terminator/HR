import { Component, inject } from '@angular/core';
import { APP_CONFIG } from '../../core/configuration/app-config';

@Component({
  selector: 'app-footer',
  template: `
    <footer class="footer">
      <span>© {{ year }} {{ config.organizationName }} · {{ config.appName }}</span>
      <span>Frontend demo · all figures are sample data</span>
    </footer>
  `,
  styles: `
    .footer {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 8px;
      padding: 14px 28px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
  `,
})
export class FooterComponent {
  protected readonly config = inject(APP_CONFIG);
  protected readonly year = new Date().getFullYear();
}
