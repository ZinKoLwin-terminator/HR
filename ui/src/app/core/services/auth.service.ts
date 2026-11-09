import { Injectable, InjectionToken, Injector, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, firstValueFrom, of, tap } from 'rxjs';
import { CurrentUser } from './current-user.service';
import { AuthDataService } from './data/auth-data.service';

export const SESSION_STORAGE_KEY = 'hrms.session';

/** Minimum length of a new password (the API enforces the same). */
export const MIN_PASSWORD_LENGTH = 12;

/**
 * True in the API build: the session is a server cookie and the user is loaded from
 * `/api/auth/me`; nothing about the user is kept in localStorage or sessionStorage.
 * False (default) in the mock build, which keeps today's browser-storage simulation.
 */
export const SERVER_SESSION = new InjectionToken<boolean>('SERVER_SESSION', {
  providedIn: 'root',
  factory: () => false,
});

/**
 * Holds the signed-in session. Mock build: "Remember me" keeps it in localStorage, otherwise
 * sessionStorage (plain user info, not a secret). API build: memory only, restored on every
 * page load by `initialize()`.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  // Resolved on use only, so merely reading the session never needs the data layer.
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  /** True when the server owns the session (API build). */
  readonly serverSession = inject(SERVER_SESSION);
  private readonly state = signal<CurrentUser | null>(this.restore());

  readonly session = this.state.asReadonly();
  readonly isAuthenticated = computed(() => this.state() !== null);

  /**
   * App initializer (API build): asks the server who is signed in before routing starts, so the
   * synchronous auth guard has an answer. Any failure, 401 included, means signed out.
   */
  initialize(): Promise<void> {
    if (!this.serverSession) return Promise.resolve();
    this.clearStorage(); // drop anything an older build left behind
    return firstValueFrom(
      this.data()
        .me()
        .pipe(
          tap((user) => this.state.set(user)),
          catchError(() => {
            this.state.set(null);
            return of(null);
          }),
        ),
    ).then(() => undefined);
  }

  signIn(email: string, password: string, remember = false): Observable<CurrentUser> {
    return this.data()
      .login(email, password, remember)
      .pipe(
        tap((user) => {
          this.persist(user, remember);
          this.state.set(user);
        }),
      );
  }

  signOut(): void {
    // The server call is best effort: the local session ends either way.
    this.data()
      .logout()
      .pipe(catchError(() => of(undefined)))
      .subscribe();
    this.clearSession();
    void this.router.navigate(['/login']);
  }

  /** Ends the in-memory session without calling the server (used when the server says 401). */
  clearSession(): void {
    this.state.set(null);
    this.clearStorage();
  }

  /** Forgot-password and reset-password pass through the contract. */
  forgotPassword(email: string): Observable<void> {
    return this.data().forgotPassword(email);
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.data().resetPassword(email, token, newPassword);
  }

  private data(): AuthDataService {
    return this.injector.get(AuthDataService);
  }

  private persist(user: CurrentUser, remember: boolean): void {
    if (this.serverSession) return;
    this.clearStorage();
    try {
      (remember ? localStorage : sessionStorage).setItem(SESSION_STORAGE_KEY, JSON.stringify(user));
    } catch {
      // Storage unavailable (private mode): the session lasts until reload.
    }
  }

  private clearStorage(): void {
    try {
      localStorage.removeItem(SESSION_STORAGE_KEY);
      sessionStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  private restore(): CurrentUser | null {
    if (this.serverSession) return null;
    try {
      const raw =
        sessionStorage.getItem(SESSION_STORAGE_KEY) ?? localStorage.getItem(SESSION_STORAGE_KEY);
      const parsed = raw ? (JSON.parse(raw) as Partial<CurrentUser>) : null;
      return parsed && typeof parsed.id === 'number' && typeof parsed.displayName === 'string'
        ? (parsed as CurrentUser)
        : null;
    } catch {
      return null;
    }
  }
}
