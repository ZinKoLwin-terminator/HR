import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from './auth.service';

export interface CurrentUser {
  id: number;
  employeeId: number | null;
  displayName: string;
  email: string;
  jobTitle: string;
  roles: string[];
  /** Permission codes. Set only by the API build (from `/api/auth/me`). */
  permissions?: string[];
}

const ANONYMOUS: CurrentUser = {
  id: 0,
  employeeId: null,
  displayName: 'Guest',
  email: '',
  jobTitle: '',
  roles: [],
};

/**
 * The signed-in user (from AuthService). Falls back to an anonymous placeholder
 * when signed out; routes behind authGuard never see it.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly auth = inject(AuthService);

  readonly current = computed<CurrentUser>(() => this.auth.session() ?? ANONYMOUS);
  readonly initials = computed(() =>
    this.current()
      .displayName.split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join(''),
  );
}
