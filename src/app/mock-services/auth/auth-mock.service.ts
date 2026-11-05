import { Injectable, Injector, inject } from '@angular/core';
import { Observable } from 'rxjs';
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
}
