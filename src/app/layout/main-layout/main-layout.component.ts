import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSidenavModule } from '@angular/material/sidenav';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { LayoutService } from '../../core/services/layout.service';
import { FooterComponent } from '../footer/footer.component';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

@Component({
  selector: 'app-main-layout',
  imports: [MatSidenavModule, RouterOutlet, SidebarComponent, HeaderComponent, FooterComponent],
  template: `
    <a class="skip-link" href="#main-content" (click)="focusMain($event)">Skip to main content</a>
    <mat-sidenav-container class="shell" [autosize]="true">
      <mat-sidenav
        class="shell__sidenav"
        [mode]="layout.isCompact() ? 'over' : 'side'"
        [opened]="layout.isCompact() ? layout.mobileOpen() : true"
        [disableClose]="!layout.isCompact()"
        (closedStart)="layout.closeMobile()"
      >
        <app-sidebar [collapsed]="layout.collapsed()" (navigated)="layout.closeMobile()" />
      </mat-sidenav>
      <mat-sidenav-content class="shell__content">
        <app-header />
        <main id="main-content" class="shell__main" tabindex="-1">
          <router-outlet />
        </main>
        <app-footer />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: block;
      height: 100dvh;
    }
    .shell {
      height: 100%;
      background: var(--app-page-bg);
    }
    .shell__sidenav {
      border-right: 0;
      border-radius: 0;
      background: var(--app-sidebar-bg);
      width: auto;
    }
    .shell__content {
      display: flex;
      flex-direction: column;
    }
    .shell__main {
      flex: 1;
      width: 100%;
      max-width: 1600px;
      margin: 0 auto;
      padding: 0 28px 32px;
      box-sizing: border-box;
      outline: none;
    }
    @media (max-width: 599.98px) {
      .shell__main {
        padding: 0 14px 24px;
      }
    }
    .skip-link {
      position: absolute;
      left: 12px;
      top: -48px;
      z-index: 1000;
      padding: 8px 16px;
      border-radius: 8px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      text-decoration: none;
      transition: top 120ms ease;
    }
    .skip-link:focus {
      top: 12px;
    }
  `,
})
export class MainLayoutComponent {
  protected readonly layout = inject(LayoutService);

  constructor() {
    // Close the overlay drawer after any navigation on small screens.
    inject(Router)
      .events.pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.layout.closeMobile());
  }

  protected focusMain(event: Event): void {
    event.preventDefault();
    document.getElementById('main-content')?.focus();
  }
}
