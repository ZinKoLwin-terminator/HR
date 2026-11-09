import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { UiNotificationService } from '../../core/services/ui-notification.service';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/**
 * Auth endpoints where a 401 is an expected answer the screen handles itself (wrong
 * password, not signed in yet), so the interceptor must not redirect.
 */
const SELF_HANDLED = [
  '/auth/login',
  '/auth/logout',
  '/auth/forgot-password',
  '/auth/reset-password',
  '/auth/antiforgery',
];

const AUTH_PAGES = /^\/(login|forgot-password|reset-password)(\?|$)/;

/**
 * API build only. Applies to calls under the API base URL:
 *  - 401: the session is gone. Clears the in-memory session and goes to
 *    `/login?returnUrl=<current url>`. Skipped when nobody was signed in (the startup `me` check:
 *    the guard redirects) and when already on an auth page.
 *  - 403: shows a forbidden message in a toast; the error still reaches the caller.
 * The XSRF header is added by Angular's XSRF support, not here. Nothing is logged.
 */
export const apiAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(UiNotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url.startsWith(baseUrl)) {
        const selfHandled = SELF_HANDLED.some((p) => req.url.startsWith(`${baseUrl}${p}`));
        if (error.status === 401 && !selfHandled && auth.isAuthenticated()) {
          auth.clearSession();
          const url = router.url;
          if (!AUTH_PAGES.test(url)) {
            const queryParams = url && url !== '/' ? { returnUrl: url } : undefined;
            void router.navigate(['/login'], { queryParams });
          }
        } else if (error.status === 403) {
          toast.error(mapHttpError(error).message, 'You do not have permission to do that.');
        }
      }
      return throwError(() => error);
    }),
  );
};
