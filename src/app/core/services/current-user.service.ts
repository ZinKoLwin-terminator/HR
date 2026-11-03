import { Injectable, computed, signal } from '@angular/core';

export interface CurrentUser {
  id: number;
  employeeId: number | null;
  displayName: string;
  email: string;
  jobTitle: string;
  roles: string[];
}

/**
 * Demo identity for the frontend-only phase. There is NO authentication:
 * this is a UI simulation and provides no security.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly user = signal<CurrentUser>({
    id: 1,
    employeeId: 2,
    displayName: 'Su Myat Noe',
    email: 'su.myatnoe@apexhorizon.example',
    jobTitle: 'Director of Human Resources',
    roles: ['HR Administrator'],
  });

  readonly current = this.user.asReadonly();
  readonly initials = computed(() =>
    this.user()
      .displayName.split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join(''),
  );
}
