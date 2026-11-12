import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { NAVIGATION, NavItem } from '../constants/navigation';
import { PermissionService } from '../services/permission.service';

/** Route `data` key that holds the permission the route needs (`module:Action`). */
export const PERMISSION_DATA_KEY = 'permission';

/**
 * UX only: sends users without the route's permission to the "no access" page instead of a screen
 * whose every call would answer 403. The server enforces the same keys regardless of this guard.
 * Routes without `data.permission` are open to any signed-in user.
 */
export const permissionGuard: CanActivateFn = (route, state) => {
  const permission = route.data[PERMISSION_DATA_KEY] as string | undefined;
  const permissions = inject(PermissionService);
  if (!permission || permissions.can(permission)) {
    return true;
  }
  const router = inject(Router);
  // Someone who may not see the dashboard lands on the first page they may see.
  if (state.url === '/dashboard' || state.url === '/') {
    const first = firstAllowedRoute(NAVIGATION, permissions);
    if (first) return router.createUrlTree([first]);
  }
  return router.createUrlTree(['/forbidden']);
};

function firstAllowedRoute(items: NavItem[], permissions: PermissionService): string | null {
  for (const item of items) {
    if (item.permission && !permissions.can(item.permission)) continue;
    if (item.route) return item.route;
    const nested = item.children ? firstAllowedRoute(item.children, permissions) : null;
    if (nested) return nested;
  }
  return null;
}
