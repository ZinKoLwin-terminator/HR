import { Observable } from 'rxjs';
import { CurrentUser } from '../current-user.service';

/**
 * Authentication contract. The mock implementation checks demo credentials; the API
 * implementation talks to `/api/auth/*` (cookie session, no token in the browser).
 * Failures are `AppError`s: `Validation` from the mock when sign-in is refused, `Unauthorized`
 * from the API (one generic message for every sign-in failure).
 */
export abstract class AuthDataService {
  /** Mock-era sign-in without "remember me". The API adapter maps it to `login`. */
  abstract signIn(email: string, password: string): Observable<CurrentUser>;

  /** The current session's user. Fails with `Unauthorized` when signed out. */
  abstract me(): Observable<CurrentUser>;

  abstract login(email: string, password: string, rememberMe: boolean): Observable<CurrentUser>;

  abstract logout(): Observable<void>;

  /** Always succeeds, whether or not the account exists (no account enumeration). */
  abstract forgotPassword(email: string): Observable<void>;

  /** Fails with `Validation` and `fieldErrors` for an invalid token or a short password. */
  abstract resetPassword(email: string, token: string, newPassword: string): Observable<void>;
}
