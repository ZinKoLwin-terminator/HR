import { Observable } from 'rxjs';
import { CurrentUser } from '../current-user.service';

/**
 * Authentication contract. The mock implementation checks demo credentials;
 * the future API implementation will exchange them for a token / session.
 * Fails with an `AppError` (code `Validation`) when sign-in is refused.
 */
export abstract class AuthDataService {
  abstract signIn(email: string, password: string): Observable<CurrentUser>;
}
