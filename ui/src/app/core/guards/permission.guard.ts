import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { NAVIGATION, firstVisibleRoute } from '../constants/navigation';
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
  // Someone who may not see the dashboard lands on the first page they may see, by the same rule
  // as the sidebar (a child's own key overrides its group's).
  if (state.url === '/dashboard' || state.url === '/') {
    const first = firstVisibleRoute(NAVIGATION, (key) => permissions.can(key));
    if (first) return router.createUrlTree([first]);
  }
  return router.createUrlTree(['/forbidden']);
};
