import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

interface QuickLink {
  label: string;
  icon: string;
  route: string;
  tone: 'primary' | 'success' | 'warning' | 'danger' | 'neutral';
}

const LINKS: readonly QuickLink[] = [
  { label: 'Add employee', icon: 'person_add', route: '/employees/new', tone: 'primary' },
  { label: 'Attendance', icon: 'monitoring', route: '/attendance/overview', tone: 'success' },
  { label: 'Apply leave', icon: 'note_add', route: '/leave/applications', tone: 'warning' },
  { label: 'Run payroll', icon: 'calculate', route: '/payroll/processing', tone: 'danger' },
  {
    label: 'Job openings',
    icon: 'work_outline',
    route: '/recruitment/job-openings',
    tone: 'primary',
  },
  { label: 'Import data', icon: 'upload_file', route: '/data-exchange/import', tone: 'neutral' },
];

/** Shortcut tiles to the most common HR tasks. */
@Component({
  selector: 'app-quick-access-card',
  imports: [MatIconModule, RouterLink],
  template: `
    <section class="card" aria-labelledby="quick-heading">
      <div class="card__header">
        <div>
          <h2 id="quick-heading">Quick access</h2>
          <p>Jump straight to common tasks</p>
        </div>
      </div>
      <div class="tiles">
        @for (l of links; track l.route) {
          <a class="tile" [routerLink]="l.route" [attr.data-tone]="l.tone">
            <span class="tile__icon" aria-hidden="true"
              ><mat-icon>{{ l.icon }}</mat-icon></span
            >
            <span class="tile__label">{{ l.label }}</span>
          </a>
        }
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .card {
      height: 100%;
    }
    .tiles {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
    }
    .tile {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 14px 6px 12px;
      border-radius: 12px;
      border: 1px solid var(--mat-sys-outline-variant);
      color: var(--mat-sys-on-surface);
      text-decoration: none;
      text-align: center;
      transition:
        background-color 120ms ease,
        border-color 120ms ease,
        transform 120ms ease;
    }
    .tile:hover,
    .tile:focus-visible {
      outline: 0;
      border-color: color-mix(in srgb, var(--icon-fg) 50%, transparent);
      background: color-mix(in srgb, var(--icon-bg) 55%, transparent);
      transform: translateY(-1px);
    }
    .tile__icon {
      display: grid;
      place-items: center;
      width: 36px;
      height: 36px;
      border-radius: 10px;
      background: var(--icon-bg);
      color: var(--icon-fg);
    }
    .tile__icon mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .tile__label {
      font: var(--mat-sys-label-medium);
    }
    [data-tone='primary'] {
      --icon-bg: var(--mat-sys-primary-container);
      --icon-fg: var(--mat-sys-on-primary-container);
    }
    [data-tone='success'] {
      --icon-bg: var(--app-success-container);
      --icon-fg: var(--app-on-success-container);
    }
    [data-tone='warning'] {
      --icon-bg: var(--app-warning-container);
      --icon-fg: var(--app-on-warning-container);
    }
    [data-tone='danger'] {
      --icon-bg: var(--mat-sys-error-container);
      --icon-fg: var(--mat-sys-on-error-container);
    }
    [data-tone='neutral'] {
      --icon-bg: var(--mat-sys-surface-container-highest);
      --icon-fg: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class QuickAccessCardComponent {
  protected readonly links = LINKS;
}
