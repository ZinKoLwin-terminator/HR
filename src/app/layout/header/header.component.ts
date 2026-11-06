import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { MODULE_GUIDE } from '../../core/constants/module-guide';
import { AppNotification } from '../../core/models/activity.models';
import { AuthService } from '../../core/services/auth.service';
import { CurrentUserService } from '../../core/services/current-user.service';
import { NotificationDataService } from '../../core/services/data/activity-data.service';
import { LayoutService } from '../../core/services/layout.service';
import { ThemeService } from '../../core/services/theme.service';
import { UiNotificationService } from '../../core/services/ui-notification.service';
import { ThemePickerComponent } from '../../shared/components/theme-picker/theme-picker.component';
import { RelativeTimePipe } from '../../shared/pipes/relative-time.pipe';

export interface Breadcrumb {
  label: string;
  url: string;
}

function buildBreadcrumbs(root: ActivatedRouteSnapshot): Breadcrumb[] {
  const crumbs: Breadcrumb[] = [];
  let url = '';
  let route: ActivatedRouteSnapshot | null = root;
  while (route) {
    const segment = route.url.map((s) => s.path).join('/');
    if (segment) {
      url += `/${segment}`;
    }
    const label = route.data['breadcrumb'] as string | undefined;
    if (label && crumbs.at(-1)?.label !== label) {
      crumbs.push({ label, url: url || '/' });
    }
    route = route.firstChild;
  }
  return crumbs;
}

@Component({
  selector: 'app-header',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatBadgeModule,
    MatTooltipModule,
    MatDividerModule,
    RouterLink,
    RelativeTimePipe,
    ThemePickerComponent,
  ],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  protected readonly guideModules = MODULE_GUIDE;
  protected readonly layout = inject(LayoutService);
  protected readonly theme = inject(ThemeService);
  protected readonly user = inject(CurrentUserService);
  private readonly notificationsApi = inject(NotificationDataService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(UiNotificationService);
  private readonly router = inject(Router);

  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => buildBreadcrumbs(this.router.routerState.snapshot.root)),
    ),
    { initialValue: [] as Breadcrumb[] },
  );

  protected readonly notifications = rxResource({
    stream: () => this.notificationsApi.getNotifications(),
    defaultValue: [] as AppNotification[],
  });
  protected readonly unreadCount = computed(
    () => this.notifications.value().filter((n) => !n.read).length,
  );

  protected open(notification: AppNotification): void {
    if (!notification.read) {
      this.notificationsApi
        .markAsRead(notification.id)
        .subscribe(() => this.notifications.reload());
    }
    if (notification.link) {
      this.router.navigateByUrl(notification.link);
    }
  }

  protected markAllRead(): void {
    this.notificationsApi.markAllAsRead().subscribe(() => this.notifications.reload());
  }

  protected signOut(): void {
    this.auth.signOut();
    this.toast.info('You have been signed out.');
  }
}
