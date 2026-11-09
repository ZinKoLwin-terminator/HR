import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, switchMap, throwError } from 'rxjs';
import { CurrentUser } from '../../core/services/current-user.service';
import { AuthDataService } from '../../core/services/data/auth-data.service';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/** The body of `GET /api/auth/me` and `POST /api/auth/login`. */
export interface ApiSessionUser {
  id: number;
  employeeId: number | null;
  displayName: string;
  email: string;
  roleName: string;
  permissions: string[];
}

function toCurrentUser(user: ApiSessionUser): CurrentUser {
  return {
    id: user.id,
    employeeId: user.employeeId,
    displayName: user.displayName,
    email: user.email,
    jobTitle: '',
    roles: user.roleName ? [user.roleName] : [],
    permissions: user.permissions ?? [],
  };
}

/**
 * HTTP implementation of the auth contract (`{API_BASE_URL}/auth/*`). The session is a cookie the
 * browser manages; nothing here stores a token or the user. Passwords are only ever placed in the
 * request body and are never logged.
 *
 * Anti-forgery: the API issues the `XSRF-TOKEN` cookie from `GET auth/antiforgery`, and Angular
 * echoes it in `X-XSRF-TOKEN` on unsafe requests. The token is tied to the identity, so it is
 * fetched again after login and logout.
 */
@Injectable({ providedIn: 'root' })
export class AuthHttpService implements AuthDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private get url(): string {
    return `${this.baseUrl}/auth`;
  }

  /** Fetches the anti-forgery cookie. Must complete before the first unsafe request. */
  antiforgery(): Observable<void> {
    return this.http.get<void>(`${this.url}/antiforgery`).pipe(map(() => undefined));
  }

  me(): Observable<CurrentUser> {
    return this.http.get<ApiSessionUser>(`${this.url}/me`).pipe(
      map(toCurrentUser),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  signIn(email: string, password: string): Observable<CurrentUser> {
    return this.login(email, password, false);
  }

  login(email: string, password: string, rememberMe: boolean): Observable<CurrentUser> {
    return this.antiforgery().pipe(
      switchMap(() =>
        this.http.post<ApiSessionUser>(`${this.url}/login`, { email, password, rememberMe }),
      ),
      // A new identity needs a new anti-forgery token for the following requests.
      switchMap((user) => this.antiforgery().pipe(map(() => toCurrentUser(user)))),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.url}/logout`, {}).pipe(
      switchMap(() => this.antiforgery()),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.antiforgery().pipe(
      switchMap(() => this.http.post<void>(`${this.url}/forgot-password`, { email })),
      map(() => undefined),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.antiforgery().pipe(
      switchMap(() =>
        this.http.post<void>(`${this.url}/reset-password`, { email, token, newPassword }),
      ),
      map(() => undefined),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }
}
