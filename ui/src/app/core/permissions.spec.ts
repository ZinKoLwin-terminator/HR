import { HttpClient } from '@angular/common/http';
import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import {
  ActivatedRoute,
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { of } from 'rxjs';
import { ApiSessionUser, AuthHttpService } from '../api-services/auth/auth-http.service';
import { ProfileOverviewTabComponent } from '../features/employees/employee-profile/tabs/profile-overview-tab.component';
import { CompaniesPageComponent } from '../features/organization/companies/companies-page.component';
import { MOCK_EMPLOYEES } from '../mock-data/employees/employees.mock';
import { provideMockDataServices } from '../mock-services/provide-mock-data-services';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from './configuration/app-config';
import { NAVIGATION, visibleNavigation } from './constants/navigation';
import { permissionGuard } from './guards/permission.guard';
import { CurrentUser, CurrentUserService } from './services/current-user.service';
import { PermissionService } from './services/permission.service';

function user(permissions?: string[]): CurrentUser {
  return {
    id: 1,
    employeeId: null,
    displayName: 'Test User',
    email: 'test@example.test',
    jobTitle: '',
    roles: ['Tester'],
    permissions,
  };
}

function withUser(current: Signal<CurrentUser>, extra: object[] = []) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([{ path: 'forbidden', children: [] }]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current } },
      ...extra,
    ],
  });
}

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
}

describe('PermissionService', () => {
  it('allows everything when the session has no permission list (mock build)', () => {
    withUser(signal(user(undefined)));
    const p = TestBed.inject(PermissionService);
    expect(p.restricted()).toBe(false);
    expect(p.can('anything:Delete')).toBe(true);
  });

  it('honours the list from /api/auth/me, including an empty one', () => {
    const current = signal(user(['employees:View', 'leave:Approve']));
    withUser(current);
    const p = TestBed.inject(PermissionService);
    expect(p.restricted()).toBe(true);
    expect(p.can('employees:View')).toBe(true);
    expect(p.canIn('leave', 'Approve')).toBe(true);
    expect(p.can('employees:ViewBank')).toBe(false);
    expect(p.canAny(['x:View', 'leave:Approve'])).toBe(true);
    current.set(user([]));
    expect(p.can('employees:View')).toBe(false);
  });

  it('is fed by the permissions member of the /me payload', () => {
    const payload: ApiSessionUser = {
      id: 3,
      employeeId: 7,
      displayName: 'Fictional Person',
      email: 'fictional@example.test',
      roleName: 'Employee',
      permissions: ['attendance:View', 'attendance:Create'],
    };
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { get: () => of(payload) } }],
    });
    let result: CurrentUser | undefined;
    TestBed.inject(AuthHttpService)
      .me()
      .subscribe((u) => (result = u));
    expect(result?.permissions).toEqual(['attendance:View', 'attendance:Create']);

    TestBed.resetTestingModule();
    withUser(signal(result as CurrentUser));
    const p = TestBed.inject(PermissionService);
    expect(p.can('attendance:Create')).toBe(true);
    expect(p.can('attendance:Edit')).toBe(false);
  });
});

describe('permissionGuard', () => {
  const run = (permission: string | undefined, url: string) =>
    TestBed.runInInjectionContext(() =>
      permissionGuard(
        { data: permission ? { permission } : {} } as unknown as ActivatedRouteSnapshot,
        { url } as RouterStateSnapshot,
      ),
    );

  it('allows a route without a permission and one the user holds', () => {
    withUser(signal(user(['leave:View'])));
    expect(run(undefined, '/help')).toBe(true);
    expect(run('leave:View', '/leave/types')).toBe(true);
  });

  it('redirects to the no-access page without the key', () => {
    withUser(signal(user(['leave:View'])));
    const result = run('employees:Create', '/employees/new') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/forbidden');
  });

  it('sends a user without dashboard access to the first page they may see', () => {
    withUser(signal(user(['leave:View'])));
    const result = run('dashboard:View', '/dashboard') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/leave/types');
  });
});

describe('navigation', () => {
  it('hides entries and empty groups the role cannot use', () => {
    const held = new Set(['dashboard:View', 'leave:View', 'employees:View']);
    const labels = visibleNavigation(NAVIGATION, (k) => held.has(k)).flatMap((i) => [
      i.label,
      ...(i.children ?? []).map((c) => c.label),
    ]);
    expect(labels).toContain('Dashboard');
    expect(labels).toContain('Leave Types');
    expect(labels).toContain('Employee List');
    expect(labels).not.toContain('Add Employee');
    expect(labels).not.toContain('Leave Approvals');
    expect(labels).not.toContain('Settings');
    expect(labels).not.toContain('Payroll');
  });
});

describe('screens', () => {
  const route = (permission: string) => ({
    provide: ActivatedRoute,
    useValue: { snapshot: { data: { permission }, parent: null } },
  });

  async function addButton(permissions: string[] | undefined): Promise<boolean> {
    withUser(signal(user(permissions)), [route('organization:View')]);
    const fixture = TestBed.createComponent(CompaniesPageComponent);
    await settle(fixture);
    return (fixture.nativeElement as HTMLElement).textContent?.includes('Add company') ?? false;
  }

  it('hides Add without Create and shows it with Create', async () => {
    expect(await addButton(['organization:View'])).toBe(false);
    TestBed.resetTestingModule();
    expect(await addButton(['organization:View', 'organization:Create'])).toBe(true);
  });

  it('shows personal and bank sections only with their keys', async () => {
    const render = async (permissions: string[]) => {
      withUser(signal(user(permissions)));
      const fixture = TestBed.createComponent(ProfileOverviewTabComponent);
      fixture.componentRef.setInput('employee', MOCK_EMPLOYEES[0]);
      await settle(fixture);
      return (fixture.nativeElement as HTMLElement).textContent ?? '';
    };
    const none = await render(['employees:View']);
    expect(none).not.toContain('National ID');
    expect(none).not.toContain('Bank information');
    TestBed.resetTestingModule();
    const all = await render(['employees:ViewSensitive', 'employees:ViewBank']);
    expect(all).toContain('National ID');
    expect(all).toContain('Bank information');
  });
});
