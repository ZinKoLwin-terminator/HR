import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { AppUser, PermissionGrants } from '../../core/models/settings.models';
import {
  GeneralSettingsDataService,
  RoleDataService,
  RolePermissionDataService,
  UserDataService,
} from '../../core/services/data/settings-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import {
  GeneralSettingsHttpService,
  RolePermissionHttpService,
} from './settings-config-http.services';
import { RoleHttpService, UserHttpService } from './user-role-http.services';

const user = {
  id: 4,
  employeeId: 9,
  displayName: 'Ada Sample',
  email: 'ada@example.test',
  roleId: 2,
  status: 'Active',
  lastLoginOn: null,
  rowVersion: 'USRV1',
};
const admin = { ...user, id: 1, employeeId: null, displayName: 'Built-in Admin', roleId: 1 };
const role = {
  id: 2,
  name: 'Manager',
  description: null,
  isSystem: false,
  status: 'Active',
  rowVersion: 'ROLV1',
};
const settings = {
  id: 1,
  organizationName: 'Example Org',
  contactEmail: 'hr@example.test',
  timezone: 'UTC',
  dateFormat: 'yyyy-MM-dd',
  currencyCode: 'MMK',
  fiscalYearStartMonth: 1,
  emailNotifications: true,
  weeklyDigest: false,
  rowVersion: 'GENV1',
};
const PASSWORD = 'correct-horse-battery';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  http
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return {
    http,
    users: TestBed.inject(UserDataService),
    roles: TestBed.inject(RoleDataService),
    permissions: TestBed.inject(RolePermissionDataService),
    general: TestBed.inject(GeneralSettingsDataService),
  };
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

const tick = () => new Promise<void>((resolve) => setTimeout(resolve));

/** Reads the user through GET so the adapter knows its rowVersion. */
async function seedUser(http: HttpTestingController, users: UserDataService, row = user) {
  const promise = firstValueFrom(users.getById(row.id));
  http.expectOne((r) => r.method === 'GET' && r.url.endsWith(`/users/${row.id}`)).flush(row);
  return promise;
}

async function seedRole(http: HttpTestingController, roles: RoleDataService) {
  const promise = firstValueFrom(roles.getById(role.id));
  http.expectOne((r) => r.method === 'GET' && r.url.endsWith(`/roles/${role.id}`)).flush(role);
  return promise;
}

describe('Settings HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mock settings services only when API mode is on', () => {
    const { users, roles, permissions, general } = setup();
    expect(users).toBeInstanceOf(UserHttpService);
    expect(roles).toBeInstanceOf(RoleHttpService);
    expect(permissions).toBeInstanceOf(RolePermissionHttpService);
    expect(general).toBeInstanceOf(GeneralSettingsHttpService);
  });

  describe('users', () => {
    it('builds the query with whitelisted sort and only known filters', async () => {
      const { http, users } = setup();
      const promise = firstValueFrom(
        users.query({
          pageIndex: 1,
          pageSize: 20,
          sort: { active: 'displayName', direction: 'desc' },
          search: '  ada ',
          filters: { roleId: 2, status: 'All', bogus: 'x' } as never,
        }),
      );
      const req = http.expectOne((r) => r.url.endsWith('/users/query'));
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('20');
      expect(req.request.params.get('sort')).toBe('displayName:desc');
      expect(req.request.params.get('search')).toBe('ada');
      expect(req.request.params.get('roleId')).toBe('2');
      expect(req.request.params.has('status')).toBe(false);
      expect(req.request.params.has('bogus')).toBe(false);
      req.flush({ items: [admin], total: 1, pageIndex: 1, pageSize: 20 });
      const page = await promise;
      // The built-in administrator has no employee: the screen gets 0 like the mock.
      expect(page.items[0].employeeId).toBe(0);
    });

    it('drops a sort key the API does not support', async () => {
      const { http, users } = setup();
      const promise = firstValueFrom(
        users.query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'employeeId', direction: 'asc' },
        }),
      );
      const req = http.expectOne((r) => r.url.endsWith('/users/query'));
      expect(req.request.params.has('sort')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await promise;
    });

    it('getAll pages /users/query until every user is read (there is no unpaged route)', async () => {
      const { http, users } = setup();
      const promise = firstValueFrom(users.getAll());
      const first = http.expectOne((r) => r.url.endsWith('/users/query'));
      expect(first.request.params.get('pageIndex')).toBe('0');
      expect(first.request.params.get('pageSize')).toBe('200');
      first.flush({ items: [user], total: 201, pageIndex: 0, pageSize: 200 });
      await tick();
      const second = http.expectOne((r) => r.url.endsWith('/users/query'));
      expect(second.request.params.get('pageIndex')).toBe('1');
      second.flush({ items: [admin], total: 201, pageIndex: 1, pageSize: 200 });
      const all = await promise;
      expect(all.map((u) => u.id)).toEqual([4, 1]);
      http.expectNone((r) => r.url.endsWith('/users'));
    });

    it('create sends initialPassword and the linked employee, role and status', async () => {
      const { http, users } = setup();
      const promise = firstValueFrom(
        users.create({
          employeeId: 9,
          roleId: 2,
          status: 'Active',
          initialPassword: PASSWORD,
        } as never),
      );
      const req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/users'));
      expect(req.request.body).toEqual({
        employeeId: 9,
        roleId: 2,
        status: 'Active',
        initialPassword: PASSWORD,
      });
      req.flush(user, { status: 201, statusText: 'Created' });
      const created = await promise;
      expect(JSON.stringify(created)).not.toContain(PASSWORD);
    });

    it('create refuses a short or missing password before any request', async () => {
      const { http, users } = setup();
      const short = await rejection(
        firstValueFrom(
          users.create({
            employeeId: 9,
            roleId: 2,
            status: 'Active',
            initialPassword: 'short',
          } as never),
        ),
      );
      expect(short.code).toBe('Validation');
      expect(short.fieldErrors?.['initialPassword']).toBeTruthy();
      expect(JSON.stringify(short)).not.toContain('short"');
      const missing = await rejection(
        firstValueFrom(users.create({ employeeId: 9, roleId: 2, status: 'Active' } as never)),
      );
      expect(missing.fieldErrors?.['initialPassword']).toBeTruthy();
      http.expectNone(() => true);
    });

    it('shows the server field errors of a create', async () => {
      const { http, users } = setup();
      const failure = rejection(
        firstValueFrom(
          users.create({
            employeeId: 9,
            roleId: 2,
            status: 'Active',
            initialPassword: PASSWORD,
          } as never),
        ),
      );
      http
        .expectOne((r) => r.method === 'POST')
        .flush(
          {
            detail: 'Validation failed.',
            code: 'Validation',
            errors: {
              EmployeeId: ['A user already exists for this employee.'],
              RoleId: ['Choose an active role.'],
              InitialPassword: ['The password is too common.'],
            },
          },
          { status: 400, statusText: 'Bad Request' },
        );
      const error = await failure;
      expect(error.fieldErrors).toEqual({
        employeeId: 'A user already exists for this employee.',
        roleId: 'Choose an active role.',
        initialPassword: 'The password is too common.',
      });
      expect(JSON.stringify(error)).not.toContain(PASSWORD);
    });

    it('PUT carries only the role and the rowVersion, never the status', async () => {
      const { http, users } = setup();
      await seedUser(http, users);
      const promise = firstValueFrom(
        users.update(4, { roleId: 3, status: 'Active', employeeId: 9 } as never),
      );
      await tick();
      const req = http.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/users/4'));
      expect(req.request.body).toEqual({ roleId: 3, rowVersion: 'USRV1' });
      req.flush({ ...user, roleId: 3, rowVersion: 'USRV2' });
      await promise;
      http.expectNone((r) => r.method === 'POST');
    });

    it('status changes use the commands with the latest rowVersion', async () => {
      const { http, users } = setup();
      await seedUser(http, users);
      let promise: Promise<unknown> = firstValueFrom(
        users.update(4, { status: 'Inactive' } as never),
      );
      await tick();
      let req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/users/4/deactivate'));
      expect(req.request.body).toEqual({ rowVersion: 'USRV1' });
      req.flush({ ...user, status: 'Inactive', rowVersion: 'USRV2' });
      await promise;

      promise = firstValueFrom(users.update(4, { status: 'Active' } as never));
      await tick();
      req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/users/4/activate'));
      expect(req.request.body).toEqual({ rowVersion: 'USRV2' });
      req.flush({ ...user, status: 'Active', rowVersion: 'USRV3' });
      await promise;
      http.expectNone((r) => r.method === 'PUT');
    });

    it('unlocks a locked user with the unlock command and refuses to lock one', async () => {
      const { http, users } = setup();
      await seedUser(http, users, { ...user, status: 'Locked' });
      const promise = firstValueFrom(users.update(4, { status: 'Active' } as never));
      await tick();
      const req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/users/4/unlock'));
      expect(req.request.body).toEqual({ rowVersion: 'USRV1' });
      req.flush({ ...user, rowVersion: 'USRV2' });
      await promise;
      const refused = await rejection(
        firstValueFrom(users.update(4, { status: 'Locked' } as never)),
      );
      expect(refused.code).toBe('Conflict');
      http.expectNone((r) => r.method !== 'GET');
    });

    it('delete sends the rowVersion in the body', async () => {
      const { http, users } = setup();
      await seedUser(http, users);
      const promise = firstValueFrom(users.delete(4));
      await tick();
      const req = http.expectOne((r) => r.method === 'DELETE' && r.url.endsWith('/users/4'));
      expect(req.request.body).toEqual({ rowVersion: 'USRV1' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      await promise;
    });

    it('password reset posts without a body and maps 503 to "Email is not configured."', async () => {
      const { http, users } = setup();
      const service = users as UserHttpService;
      const ok = firstValueFrom(service.resetPassword(4));
      const req = http.expectOne(
        (r) => r.method === 'POST' && r.url.endsWith('/users/4/password-reset'),
      );
      expect(req.request.body).toBeNull();
      req.flush({ message: 'Sent.' });
      expect(await ok).toBe('Sent.');

      const failure = rejection(firstValueFrom(service.resetPassword(4)));
      http
        .expectOne((r) => r.url.endsWith('/users/4/password-reset'))
        .flush(
          { code: 'Unknown', detail: 'No mail host.' },
          { status: 503, statusText: 'Unavailable' },
        );
      expect((await failure).message).toBe('Email is not configured.');
    });

    it('maps 403 to the administrators-only message and 409 to the reload message', async () => {
      const { http, users } = setup();
      const forbidden = rejection(firstValueFrom(users.query({ pageIndex: 0, pageSize: 10 })));
      http
        .expectOne((r) => r.url.endsWith('/users/query'))
        .flush({ title: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
      const denied = await forbidden;
      expect(denied.code).toBe('Forbidden');
      expect(denied.message).toBe('Only HR Administrators can manage users and roles.');

      await seedUser(http, users);
      const stale = rejection(firstValueFrom(users.update(4, { roleId: 3 } as never)));
      await tick();
      http
        .expectOne((r) => r.method === 'PUT')
        .flush(null, { status: 409, statusText: 'Conflict' });
      const conflict = await stale;
      expect(conflict.code).toBe('Conflict');
      expect(conflict.message).toBe('The record changed. Reload and try again.');
    });
  });

  describe('roles', () => {
    it('create sends the name, description and status', async () => {
      const { http, roles } = setup();
      const promise = firstValueFrom(
        roles.create({ name: 'Auditor', description: ' ', status: 'Inactive' } as never),
      );
      const req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/roles'));
      expect(req.request.body).toEqual({ name: 'Auditor', description: null, status: 'Inactive' });
      req.flush(
        { ...role, name: 'Auditor', status: 'Inactive' },
        { status: 201, statusText: 'Created' },
      );
      await promise;
    });

    it('PUT carries name and description only, and the status goes through a command', async () => {
      const { http, roles } = setup();
      await seedRole(http, roles);
      const promise = firstValueFrom(
        roles.update(2, { name: 'Lead', description: 'Leads', status: 'Inactive' } as never),
      );
      await tick();
      const put = http.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/roles/2'));
      expect(put.request.body).toEqual({ name: 'Lead', description: 'Leads', rowVersion: 'ROLV1' });
      put.flush({ ...role, name: 'Lead', description: 'Leads', rowVersion: 'ROLV2' });
      await tick();
      const command = http.expectOne(
        (r) => r.method === 'POST' && r.url.endsWith('/roles/2/deactivate'),
      );
      expect(command.request.body).toEqual({ rowVersion: 'ROLV2' });
      command.flush({ ...role, name: 'Lead', status: 'Inactive', rowVersion: 'ROLV3' });
      await promise;
    });

    it('a status-only change sends no PUT', async () => {
      const { http, roles } = setup();
      await seedRole(http, roles);
      const promise = firstValueFrom(roles.update(2, { status: 'Inactive' } as never));
      await tick();
      http
        .expectOne((r) => r.method === 'POST' && r.url.endsWith('/roles/2/deactivate'))
        .flush({
          ...role,
          status: 'Inactive',
        });
      await promise;
      http.expectNone((r) => r.method === 'PUT');
    });

    it('getAll uses the unpaged route and maps a null description to empty text', async () => {
      const { http, roles } = setup();
      const promise = firstValueFrom(roles.getAll());
      http.expectOne((r) => r.method === 'GET' && r.url.endsWith('/roles')).flush([role]);
      expect((await promise)[0].description).toBe('');
    });
  });

  describe('permission matrix', () => {
    const matrix = {
      modules: [{ key: 'employees', label: 'Employees', actions: ['View', 'ViewBank'] }],
      roles: [
        {
          id: 2,
          roleId: 2,
          grants: { employees: ['View', 'ViewBank', 'Export'], leave: ['View'] },
          rowVersion: 'ROLV1',
        },
      ],
    };

    it('reads the matrix and keeps the extra actions', async () => {
      const { http, permissions } = setup();
      const promise = firstValueFrom(permissions.getAll());
      http
        .expectOne((r) => r.method === 'GET' && r.url.endsWith('/role-permissions'))
        .flush(matrix);
      const [first] = await promise;
      expect(first.roleId).toBe(2);
      expect(first.grants['employees']).toEqual(['View', 'ViewBank', 'Export']);
    });

    it('saves with the role rowVersion and keeps untouched grants including extra actions', async () => {
      const { http, permissions } = setup();
      const read = firstValueFrom(permissions.getAll());
      http.expectOne((r) => r.url.endsWith('/role-permissions')).flush(matrix);
      const [loaded] = await read;

      const grants = structuredClone(loaded.grants) as PermissionGrants;
      grants['leave'] = ['View', 'Create'];
      const promise = firstValueFrom(permissions.save(2, grants));
      await tick();
      const req = http.expectOne(
        (r) => r.method === 'PUT' && r.url.endsWith('/role-permissions/2'),
      );
      expect(req.request.body).toEqual({
        grants: { employees: ['View', 'ViewBank', 'Export'], leave: ['View', 'Create'] },
        rowVersion: 'ROLV1',
      });
      req.flush({ id: 2, roleId: 2, grants: req.request.body.grants, rowVersion: 'ROLV2' });
      expect((await promise).grants['employees']).toContain('ViewBank');

      // The save moved the token: the next save uses it.
      const again = firstValueFrom(permissions.save(2, grants));
      await tick();
      const next = http.expectOne((r) => r.method === 'PUT');
      expect(next.request.body.rowVersion).toBe('ROLV2');
      next.flush({ id: 2, roleId: 2, grants, rowVersion: 'ROLV3' });
      await again;
    });

    it('reads the role first when it has no rowVersion yet', async () => {
      const { http, permissions } = setup();
      const promise = firstValueFrom(permissions.save(2, { leave: ['View'] }));
      http
        .expectOne((r) => r.method === 'GET' && r.url.endsWith('/role-permissions/2'))
        .flush(matrix.roles[0]);
      await tick();
      const req = http.expectOne((r) => r.method === 'PUT');
      expect(req.request.body.rowVersion).toBe('ROLV1');
      req.flush({ ...matrix.roles[0], rowVersion: 'ROLV2' });
      await promise;
    });

    it('returns an empty grant set for a role without grants and maps 403 and 409', async () => {
      const { http, permissions } = setup();
      const empty = firstValueFrom(permissions.getForRole(5));
      http
        .expectOne((r) => r.url.endsWith('/role-permissions/5'))
        .flush({ id: 5, roleId: 5, grants: {}, rowVersion: 'R5' });
      expect((await empty).grants).toEqual({});

      const forbidden = rejection(firstValueFrom(permissions.getForRole(6)));
      http
        .expectOne((r) => r.url.endsWith('/role-permissions/6'))
        .flush(null, { status: 403, statusText: 'Forbidden' });
      expect((await forbidden).message).toBe('Only HR Administrators can manage users and roles.');

      const stale = rejection(firstValueFrom(permissions.save(5, { leave: ['View'] })));
      await tick();
      http
        .expectOne((r) => r.method === 'PUT')
        .flush(null, { status: 409, statusText: 'Conflict' });
      expect((await stale).code).toBe('Conflict');
    });
  });

  describe('general settings', () => {
    it('reads the settings and strips the rowVersion from the screen model', async () => {
      const { http, general } = setup();
      const promise = firstValueFrom(general.get());
      http
        .expectOne((r) => r.method === 'GET' && r.url.endsWith('/general-settings'))
        .flush(settings);
      const value = await promise;
      expect(value.organizationName).toBe('Example Org');
      expect('rowVersion' in value).toBe(false);
    });

    it('PUT sends every field with the rowVersion of the last read, and the next save uses the new one', async () => {
      const { http, general } = setup();
      const read = firstValueFrom(general.get());
      http.expectOne((r) => r.url.endsWith('/general-settings')).flush(settings);
      await read;

      const { id: _id, rowVersion: _token, ...values } = settings;
      void _id;
      void _token;
      const promise = firstValueFrom(general.save({ ...values, fiscalYearStartMonth: 4 }));
      await tick();
      const req = http.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/general-settings'));
      expect(req.request.body).toEqual({ ...values, fiscalYearStartMonth: 4, rowVersion: 'GENV1' });
      req.flush({ ...settings, fiscalYearStartMonth: 4, rowVersion: 'GENV2' });
      expect((await promise).fiscalYearStartMonth).toBe(4);

      const again = firstValueFrom(general.save(values));
      await tick();
      const next = http.expectOne((r) => r.method === 'PUT');
      expect(next.request.body.rowVersion).toBe('GENV2');
      next.flush({ ...settings, rowVersion: 'GENV3' });
      await again;
    });

    it('maps a 403 on save to an administrators-only message and keeps field errors', async () => {
      const { http, general } = setup();
      const { id: _id, rowVersion: _token, ...values } = settings;
      void _id;
      void _token;
      const read = firstValueFrom(general.get());
      http.expectOne((r) => r.url.endsWith('/general-settings')).flush(settings);
      await read;

      const forbidden = rejection(firstValueFrom(general.save(values)));
      await tick();
      http
        .expectOne((r) => r.method === 'PUT')
        .flush(null, { status: 403, statusText: 'Forbidden' });
      expect((await forbidden).message).toBe('Only HR Administrators can change general settings.');

      const invalid = rejection(firstValueFrom(general.save(values)));
      await tick();
      http
        .expectOne((r) => r.method === 'PUT')
        .flush(
          { code: 'Validation', errors: { ContactEmail: ['Enter a valid email address.'] } },
          { status: 400, statusText: 'Bad Request' },
        );
      expect((await invalid).fieldErrors).toEqual({ contactEmail: 'Enter a valid email address.' });
    });
  });

  describe('password handling', () => {
    it('never appears in browser storage, console output or the returned values', async () => {
      const logs: unknown[][] = [];
      const spies = (['log', 'info', 'warn', 'error', 'debug'] as const).map((level) =>
        vi.spyOn(console, level).mockImplementation((...args: unknown[]) => void logs.push(args)),
      );
      try {
        const { http, users } = setup();
        const ok = firstValueFrom(
          users.create({
            employeeId: 9,
            roleId: 2,
            status: 'Active',
            initialPassword: PASSWORD,
          } as never),
        );
        http
          .expectOne((r) => r.method === 'POST')
          .flush(user, { status: 201, statusText: 'Created' });
        const created = await ok;

        const bad = rejection(
          firstValueFrom(
            users.create({
              employeeId: 9,
              roleId: 2,
              status: 'Active',
              initialPassword: PASSWORD,
            } as never),
          ),
        );
        http
          .expectOne((r) => r.method === 'POST')
          .flush(
            { detail: 'Validation failed.', errors: { InitialPassword: ['Too common.'] } },
            { status: 400, statusText: 'Bad Request' },
          );
        const error = await bad;

        const stored = [
          ...Object.keys(localStorage).map((k) => `${k}=${localStorage.getItem(k)}`),
          ...Object.keys(sessionStorage).map((k) => `${k}=${sessionStorage.getItem(k)}`),
        ].join('\n');
        expect(stored).not.toContain(PASSWORD);
        expect(JSON.stringify(logs)).not.toContain(PASSWORD);
        expect(JSON.stringify(created satisfies AppUser)).not.toContain(PASSWORD);
        expect(JSON.stringify(error)).not.toContain(PASSWORD);
      } finally {
        spies.forEach((spy) => spy.mockRestore());
      }
    });
  });
});
