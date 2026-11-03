import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { GeneralSettings, PERMISSION_MODULES } from '../../core/models/settings.models';
import {
  GeneralSettingsDataService,
  RoleDataService,
  RolePermissionDataService,
  UserDataService,
} from '../../core/services/data/settings-data.services';
import {
  MOCK_ROLE_PERMISSIONS,
  MOCK_ROLES,
  MOCK_USERS,
} from '../../mock-data/settings/settings.mock';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('settings seed data', () => {
  it('is consistent', () => {
    const roleIds = new Set(MOCK_ROLES.map((r) => r.id));
    MOCK_USERS.forEach((u) => expect(roleIds.has(u.roleId)).toBe(true));
    expect(new Set(MOCK_USERS.map((u) => u.employeeId)).size).toBe(MOCK_USERS.length);
    expect(MOCK_ROLE_PERMISSIONS).toHaveLength(MOCK_ROLES.length);
    // every grant uses a known module and an action that module offers; non-View needs View
    for (const set of MOCK_ROLE_PERMISSIONS) {
      for (const [key, actions] of Object.entries(set.grants)) {
        const module = PERMISSION_MODULES.find((m) => m.key === key)!;
        expect(module).toBeDefined();
        actions.forEach((a) => expect(module.actions).toContain(a));
        if (actions.length) expect(actions).toContain('View');
      }
    }
    // the built-in administrator has full access
    const admin = MOCK_ROLE_PERMISSIONS[0].grants;
    PERMISSION_MODULES.forEach((m) => expect(admin[m.key]).toEqual([...m.actions]));
  });
});

describe('settings data services', () => {
  let users: UserDataService;
  let roles: RoleDataService;
  let permissions: RolePermissionDataService;
  let general: GeneralSettingsDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
    users = TestBed.inject(UserDataService);
    roles = TestBed.inject(RoleDataService);
    permissions = TestBed.inject(RolePermissionDataService);
    general = TestBed.inject(GeneralSettingsDataService);
  });

  const newUser = (overrides = {}) =>
    ({
      employeeId: 20,
      roleId: 6,
      status: 'Active',
      displayName: '',
      email: '',
      lastLoginOn: null,
      ...overrides,
    }) as Parameters<UserDataService['create']>[0];

  describe('users', () => {
    it('copies the name and email from the employee and rejects duplicates', async () => {
      const created = await firstValueFrom(users.create(newUser()));
      expect(created.displayName).not.toBe('');
      expect(created.email).toMatch(/@/);
      expect(created.lastLoginOn).toBeNull();
      await expectError(firstValueFrom(users.create(newUser())), 'Validation'); // same employee
      await expectError(firstValueFrom(users.create(newUser({ employeeId: 999 }))), 'Validation');
      await expectError(
        firstValueFrom(users.create(newUser({ employeeId: 21, roleId: 7 }))),
        'Validation',
      ); // inactive role
      await expectError(
        firstValueFrom(users.create(newUser({ employeeId: 21, roleId: 99 }))),
        'Validation',
      );
    });

    it('does not let employees be swapped and protects your own account', async () => {
      await expectError(firstValueFrom(users.update(3, { employeeId: 30 })), 'Validation');
      await expectError(firstValueFrom(users.update(1, { status: 'Inactive' })), 'Conflict'); // current user
      await expectError(firstValueFrom(users.update(1, { roleId: 2 })), 'Conflict');
      await expectError(firstValueFrom(users.delete(1)), 'Conflict');
    });

    it('lets an extra administrator be demoted and deleted (the only one is the current user)', async () => {
      const second = await firstValueFrom(users.create(newUser({ roleId: 1 })));
      // with two administrators one may be demoted…
      await firstValueFrom(users.update(second.id, { roleId: 2 }));
      const admin = await firstValueFrom(users.update(second.id, { roleId: 1 }));
      expect(admin.roleId).toBe(1);
      await firstValueFrom(users.delete(second.id));
    });

    it('unlocks an account and filters by role and status', async () => {
      expect((await firstValueFrom(users.update(9, { status: 'Active' }))).status).toBe('Active');
      const page = await firstValueFrom(
        users.query({ pageIndex: 0, pageSize: 50, filters: { roleId: 3, status: 'Active' } }),
      );
      expect(page.items.length).toBeGreaterThan(0);
      expect(page.items.every((u) => u.roleId === 3 && u.status === 'Active')).toBe(true);
    });
  });

  describe('roles', () => {
    it('requires unique names and keeps built-in roles intact', async () => {
      await expectError(
        firstValueFrom(
          roles.create({ name: 'recruiter', description: '', isSystem: false, status: 'Active' }),
        ),
        'Validation',
      );
      await expectError(firstValueFrom(roles.update(1, { name: 'Boss' })), 'Validation');
      await expectError(firstValueFrom(roles.update(6, { status: 'Inactive' })), 'Conflict');
      await expectError(firstValueFrom(roles.delete(1)), 'Conflict'); // built-in
      expect((await firstValueFrom(roles.update(1, { description: 'Updated' }))).description).toBe(
        'Updated',
      );
    });

    it('creates custom roles that cannot claim to be built-in, and protects roles in use', async () => {
      const role = await firstValueFrom(
        roles.create({ name: 'Trainer', description: '', isSystem: true, status: 'Active' }),
      );
      expect(role.isSystem).toBe(false);
      const empty = await firstValueFrom(permissions.getForRole(role.id));
      expect(empty.grants).toEqual({});

      const error = await expectError(firstValueFrom(roles.delete(3)), 'Conflict'); // has users
      expect(error.message).toContain('user(s)');
      await expectError(firstValueFrom(roles.update(3, { status: 'Inactive' })), 'Conflict'); // active users
      await firstValueFrom(roles.delete(role.id));
      await expectError(firstValueFrom(permissions.getForRole(role.id)), 'NotFound');
    });
  });

  describe('permissions', () => {
    it('saves a valid matrix and returns it', async () => {
      const saved = await firstValueFrom(
        permissions.save(5, { dashboard: ['View'], recruitment: ['View', 'Edit', 'View'] }),
      );
      expect(saved.grants['recruitment']).toEqual(['View', 'Edit']);
      expect((await firstValueFrom(permissions.getForRole(5))).grants['recruitment']).toEqual([
        'View',
        'Edit',
      ]);
      const all = await firstValueFrom(permissions.getAll());
      expect(all.find((p) => p.roleId === 5)?.grants['dashboard']).toEqual(['View']);
    });

    it('rejects unknown modules, unsupported actions and grants without View', async () => {
      await expectError(firstValueFrom(permissions.save(5, { nope: ['View'] })), 'Validation');
      await expectError(
        firstValueFrom(permissions.save(5, { dashboard: ['View', 'Delete'] })),
        'Validation',
      );
      const error = await expectError(
        firstValueFrom(permissions.save(5, { payroll: ['Edit'] })),
        'Validation',
      );
      expect(error.fieldErrors?.['payroll']).toBeTruthy();
      await expectError(firstValueFrom(permissions.save(999, {})), 'NotFound');
    });

    it('never changes the administrator role', async () => {
      await expectError(firstValueFrom(permissions.save(1, { dashboard: ['View'] })), 'Conflict');
    });
  });

  describe('general settings', () => {
    const valid = (overrides: Partial<GeneralSettings> = {}): Omit<GeneralSettings, 'id'> => ({
      organizationName: ' New Name ',
      contactEmail: 'hr@example.com',
      timezone: 'UTC',
      dateFormat: 'dd/MM/yyyy',
      currencyCode: 'EUR',
      fiscalYearStartMonth: 4,
      emailNotifications: false,
      weeklyDigest: true,
      ...overrides,
    });

    it('saves valid values and trims the name', async () => {
      const saved = await firstValueFrom(general.save(valid()));
      expect(saved.organizationName).toBe('New Name');
      expect((await firstValueFrom(general.get())).currencyCode).toBe('EUR');
    });

    it('reports every invalid field', async () => {
      const error = await expectError(
        firstValueFrom(
          general.save(
            valid({
              organizationName: ' ',
              contactEmail: 'x',
              timezone: 'Mars/Base',
              currencyCode: 'usd',
              fiscalYearStartMonth: 13,
            }),
          ),
        ),
        'Validation',
      );
      expect(Object.keys(error.fieldErrors ?? {}).sort()).toEqual([
        'contactEmail',
        'currencyCode',
        'fiscalYearStartMonth',
        'organizationName',
        'timezone',
      ]);
      expect((await firstValueFrom(general.get())).organizationName).toBe('Apex Horizon Group'); // unchanged
    });
  });
});
