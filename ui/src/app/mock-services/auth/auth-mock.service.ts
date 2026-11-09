import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService, MIN_PASSWORD_LENGTH } from '../../core/services/auth.service';
import { CurrentUser } from '../../core/services/current-user.service';
import { AuthDataService } from '../../core/services/data/auth-data.service';
import { MOCK_EMPLOYEES } from '../../mock-data/employees/employees.mock';
import { MOCK_POSITIONS } from '../../mock-data/organization/positions.mock';
import { MOCK_ROLES } from '../../mock-data/settings/settings.mock';
import { mockError, mockResponse } from '../mock-response';
import { UserMockService } from '../settings/settings-mock.services';

/**
 * DEMO password shared by every sample account. This is a UI simulation of
 * sign-in against the Users list in Settings — it provides NO real security.
 */
export const DEMO_PASSWORD = 'Welcome@123';

const GENERIC_FAILURE = 'Incorrect email or password.';

@Injectable({ providedIn: 'root' })
export class AuthMockService implements AuthDataService {
  // Resolved lazily: UserMockService -> ActivityMockService -> CurrentUserService -> AuthService
  // would otherwise form an injection cycle through this class.
  private readonly injector = inject(Injector);

  signIn(email: string, password: string): Observable<CurrentUser> {
    return mockResponse(() => {
      const user = this.injector
        .get(UserMockService)
        .records()
        .find((u) => u.email.toLowerCase() === email.trim().toLowerCase());
      // Same message for unknown email and wrong password (no account enumeration).
      if (!user || password !== DEMO_PASSWORD) {
        throw mockError('Validation', GENERIC_FAILURE);
      }
      if (user.status === 'Inactive') {
        throw mockError('Validation', 'This account is deactivated. Contact an administrator.');
      }
      if (user.status === 'Locked') {
        throw mockError('Validation', 'This account is locked. Ask an administrator to unlock it.');
      }
      const employee = MOCK_EMPLOYEES.find((e) => e.id === user.employeeId);
      const position = MOCK_POSITIONS.find((p) => p.id === employee?.positionId);
      const role = MOCK_ROLES.find((r) => r.id === user.roleId);
      return {
        id: user.id,
        employeeId: user.employeeId,
        displayName: user.displayName,
        email: user.email,
        jobTitle: position?.title ?? '',
        roles: role ? [role.name] : [],
      } satisfies CurrentUser;
    }, 600);
  }

  me(): Observable<CurrentUser> {
    return mockResponse(() => {
      const session = this.injector.get(AuthService).session();
      if (!session) throw mockError('Unauthorized', 'You need to sign in to continue.');
      return session;
    }, 0);
  }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.signIn(email, password);
  }

  logout(): Observable<void> {
    return mockResponse(() => undefined, 0);
  }

  /** Stand-in: the demo has no mailbox, so the request always "succeeds". */
  forgotPassword(): Observable<void> {
    return mockResponse(() => undefined, 300);
  }

  /** Stand-in: enforces the same minimum length as the API; any token is accepted. */
  resetPassword(_email: string, _token: string, newPassword: string): Observable<void> {
    return mockResponse(() => {
      if (newPassword.length < MIN_PASSWORD_LENGTH) {
        throw mockError('Validation', 'Please correct the highlighted fields.', {
          newPassword: `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`,
        });
      }
    }, 300);
  }
}
