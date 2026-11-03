import { Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { NAVIGATION, NavItem } from '../../core/constants/navigation';

@Component({
  selector: 'app-sidebar',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatMenuModule,
  ],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
  host: { '[class.collapsed]': 'collapsed()' },
})
export class SidebarComponent {
  private readonly router = inject(Router);
  protected readonly config = inject(APP_CONFIG);

  readonly collapsed = input(false);
  /** Emitted after a link is chosen (used to close the mobile drawer). */
  readonly navigated = output<void>();

  protected readonly items = NAVIGATION;
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());
  private readonly currentUrl = signal(this.router.url);

  constructor() {
    this.expandActiveGroup(this.router.url);
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((e) => {
        this.currentUrl.set(e.urlAfterRedirects);
        this.expandActiveGroup(e.urlAfterRedirects);
      });
  }

  protected isExpanded(group: NavItem): boolean {
    return this.expanded().has(group.label);
  }

  protected isGroupActive(group: NavItem): boolean {
    const url = this.currentUrl();
    return !!group.children?.some((child) => child.route && url.startsWith(child.route));
  }

  protected toggle(group: NavItem): void {
    this.expanded.update((current) => {
      const next = new Set(current);
      if (next.has(group.label)) {
        next.delete(group.label);
      } else {
        next.add(group.label);
      }
      return next;
    });
  }

  protected groupId(group: NavItem): string {
    return `nav-group-${group.label.toLowerCase().replace(/\W+/g, '-')}`;
  }

  private expandActiveGroup(url: string): void {
    const active = this.items.find((g) =>
      g.children?.some((c) => c.route && url.startsWith(c.route)),
    );
    if (active && !this.expanded().has(active.label)) {
      this.expanded.update((current) => new Set(current).add(active.label));
    }
  }
}
