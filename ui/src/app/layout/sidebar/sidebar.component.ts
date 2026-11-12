import { Component, computed, inject, input, output, signal } from '@angular/core';
import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { KeyValuePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { NAVIGATION, NavItem, visibleNavigation } from '../../core/constants/navigation';
import { PermissionService } from '../../core/services/permission.service';

@Component({
  selector: 'app-sidebar',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    OverlayModule,
    KeyValuePipe,
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

  private readonly permissions = inject(PermissionService);
  /** Only the entries the signed-in user's role allows (UX; the server enforces). */
  protected readonly items = computed(() =>
    visibleNavigation(NAVIGATION, (key) => this.permissions.can(key)),
  );

  /** Label of the group whose flyout is open in the collapsed icon rail. */
  protected readonly flyout = signal<string | null>(null);
  private closeTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly flyoutPositions: ConnectedPosition[] = [
    { originX: 'end', originY: 'top', overlayX: 'start', overlayY: 'top', offsetX: 8, offsetY: -4 },
    {
      originX: 'end',
      originY: 'bottom',
      overlayX: 'start',
      overlayY: 'bottom',
      offsetX: 8,
      offsetY: 4,
    },
  ];
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());
  private readonly currentUrl = signal(this.router.url);

  protected get groupedItems(): Map<string, NavItem[]> {
    const sections = new Map<string, NavItem[]>();
    const sectionOrder = ['MAIN', 'PEOPLE', 'OPERATIONS', 'SYSTEM'];

    for (const section of sectionOrder) {
      const itemsInSection = this.items().filter((item) => item.section === section);
      if (itemsInSection.length > 0) {
        sections.set(section, itemsInSection);
      }
    }
    return sections;
  }

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

  /** Pointer over a group: show its flyout (skipped when it is already open inline). */
  protected hoverGroup(group: NavItem): void {
    if (this.collapsed() || !this.isExpanded(group)) {
      this.openFlyout(group.label);
    }
  }

  protected focusGroup(group: NavItem): void {
    if (this.collapsed()) {
      this.openFlyout(group.label);
    }
  }

  protected clickGroup(group: NavItem): void {
    if (this.collapsed()) {
      this.openFlyout(group.label);
    } else {
      this.closeFlyout();
      this.toggle(group);
    }
  }

  protected openFlyout(label: string): void {
    this.cancelClose();
    this.flyout.set(label);
  }

  /** Short delay so the pointer can cross the gap between the rail and the panel. */
  protected scheduleClose(): void {
    this.cancelClose();
    this.closeTimer = setTimeout(() => this.flyout.set(null), 180);
  }

  protected cancelClose(): void {
    clearTimeout(this.closeTimer);
  }

  /** Only reset when the detached overlay is still the active one (not when switching groups). */
  protected onFlyoutDetach(label: string): void {
    if (this.flyout() === label) {
      this.closeFlyout();
    }
  }

  protected closeFlyout(): void {
    this.cancelClose();
    this.flyout.set(null);
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
    const active = this.items().find((g) =>
      g.children?.some((c) => c.route && url.startsWith(c.route)),
    );
    if (active && !this.expanded().has(active.label)) {
      this.expanded.update((current) => new Set(current).add(active.label));
    }
  }
}
