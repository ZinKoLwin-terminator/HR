import { Injectable, Injector, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { CurrentUser } from './current-user.service';
import { AuthDataService } from './data/auth-data.service';

export const SESSION_STORAGE_KEY = 'hrms.session';

/**
 * Holds the signed-in session. "Remember me" keeps it in localStorage; otherwise it
 * lives in sessionStorage and ends with the browser tab. Frontend-only: the stored
 * object is plain user info, not a secret token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  // Resolved on sign-in only, so merely reading the session never needs the data layer.
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  private readonly state = signal<CurrentUser | null>(this.restore());

  readonly session = this.state.asReadonly();
  readonly isAuthenticated = computed(() => this.state() !== null);

  signIn(email: string, password: string, remember = false): Observable<CurrentUser> {
    return this.injector
      .get(AuthDataService)
      .signIn(email, password)
      .pipe(
        tap((user) => {
          this.persist(user, remember);
          this.state.set(user);
        }),
      );
  }

  signOut(): void {
    this.state.set(null);
    this.clearStorage();
    void this.router.navigate(['/login']);
  }

  private persist(user: CurrentUser, remember: boolean): void {
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
