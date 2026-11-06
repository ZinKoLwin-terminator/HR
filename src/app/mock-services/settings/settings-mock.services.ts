import { Injectable, Injector, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import {
  ADMIN_ROLE_NAME,
  AppUser,
  DATE_FORMATS,
  GeneralSettings,
  PERMISSION_MODULES,
  PermissionAction,
  PermissionGrants,
  Role,
  RolePermissions,
  TIMEZONES,
} from '../../core/models/settings.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import {
  GeneralSettingsDataService,
  RoleDataService,
  RolePermissionDataService,
  UserDataService,
} from '../../core/services/data/settings-data.services';
import {
  MOCK_GENERAL_SETTINGS,
  MOCK_ROLES,
  MOCK_ROLE_PERMISSIONS,
  MOCK_USERS,
} from '../../mock-data/settings/settings.mock';
import { ActivityMockService } from '../activity/activity-mock.service';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { mockError, mockResponse } from '../mock-response';

@Injectable({ providedIn: 'root' })
export class RoleMockService extends InMemoryCrudService<Role> implements RoleDataService {
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Role';
  protected readonly activityCategory = 'system';
  protected override readonly searchFields = ['name', 'description'] as const;

  constructor() {
    super(MOCK_ROLES);
  }

  protected override describe(entity: Role): string {
    return entity.name;
  }

  protected override prepare(candidate: Role, isNew: boolean): void {
    candidate.name = candidate.name.trim();
    if (isNew) {
      candidate.isSystem = false;
    } else {
      candidate.isSystem = this.findById(candidate.id)?.isSystem ?? candidate.isSystem;
    }
  }

  protected override validate(candidate: Role, isNew: boolean): void {
    this.ensureUnique(candidate, 'name', 'Role name');
    if (!isNew) {
      const stored = this.findById(candidate.id);
      if (stored?.isSystem && stored.name !== candidate.name) {
        this.validationError('A built-in role cannot be renamed.', {
          name: 'Built-in roles keep their name.',
        });
      }
      if (candidate.status === 'Inactive') {
        if (stored?.isSystem) {
          this.conflictError('A built-in role cannot be deactivated.');
        }
        const active = this.injector
          .get(UserMockService)
          .records()
          .filter((u) => u.roleId === candidate.id && u.status === 'Active').length;
        if (active > 0) {
          this.conflictError(
            `${active} active user(s) still have this role. Move them to another role first.`,
          );
        }
      }
    }
  }

  protected override beforeDelete(entity: Role): void {
    if (entity.isSystem) {
      this.conflictError('A built-in role cannot be deleted.');
    }
    const users = this.injector
      .get(UserMockService)
      .records()
      .filter((u) => u.roleId === entity.id).length;
    this.ensureNotReferenced(entity.name, [['user(s)', users]]);
    this.injector.get(RolePermissionMockService).removeForRole(entity.id);
  }
}

@Injectable({ providedIn: 'root' })
export class UserMockService extends InMemoryCrudService<AppUser> implements UserDataService {
  private readonly roles = inject(RoleMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly currentUser = inject(CurrentUserService);
  protected readonly entityLabel = 'User';
  protected readonly activityCategory = 'system';
  protected override readonly searchFields = ['displayName', 'email'] as const;

  constructor() {
    super(MOCK_USERS);
  }

  protected override describe(entity: AppUser): string {
    return entity.displayName;
  }

  protected override prepare(candidate: AppUser, isNew: boolean): void {
    const employee = this.employees.findById(candidate.employeeId);
    if (employee) {
      candidate.displayName = `${employee.firstName} ${employee.lastName}`;
      candidate.email = employee.workEmail;
    }
    if (isNew) {
      candidate.status ||= 'Active';
      candidate.lastLoginOn = null;
    }
  }

  private isAdminRole(roleId: number): boolean {
    return this.roles.findById(roleId)?.name === ADMIN_ROLE_NAME;
  }

  /** True when no OTHER active user holds the administrator role. */
  private isLastAdmin(user: AppUser): boolean {
    return (
      user.status === 'Active' &&
      this.isAdminRole(user.roleId) &&
      !this.store().some(
        (u) => u.id !== user.id && u.status === 'Active' && this.isAdminRole(u.roleId),
      )
    );
  }

  protected override validate(candidate: AppUser, isNew: boolean): void {
    const employee = this.employees.findById(candidate.employeeId);
    if (!employee || employee.exitDate) {
      this.validationError('Choose a current employee.', {
        employeeId: 'Choose a current employee.',
      });
    }
    if (this.store().some((u) => u.id !== candidate.id && u.employeeId === candidate.employeeId)) {
      this.validationError('This employee already has a user account.', {
        employeeId: 'A user already exists for this employee.',
      });
    }
    const role = this.roles.findById(candidate.roleId);
    if (!role || role.status !== 'Active') {
      this.validationError('Choose an active role.', { roleId: 'Choose an active role.' });
    }
    if (!isNew) {
      const stored = this.findById(candidate.id);
      if (stored && stored.employeeId !== candidate.employeeId) {
        this.validationError('The linked employee cannot be changed.', {
          employeeId: 'Create a new user instead.',
        });
      }
      if (stored?.id === this.currentUser.current().id) {
        if (candidate.status !== 'Active' || candidate.roleId !== stored.roleId) {
          this.conflictError('You cannot change your own role or deactivate your own account.');
        }
      }
      if (
        stored &&
        this.isLastAdmin(stored) &&
        (candidate.status !== 'Active' || !this.isAdminRole(candidate.roleId))
      ) {
        this.conflictError(`There must always be one active ${ADMIN_ROLE_NAME}.`);
      }
    }
  }

  protected override beforeDelete(entity: AppUser): void {
    if (entity.id === this.currentUser.current().id) {
      this.conflictError('You cannot delete your own account.');
    }
    if (this.isLastAdmin(entity)) {
      this.conflictError(`There must always be one active ${ADMIN_ROLE_NAME}.`);
    }
  }
}

@Injectable({ providedIn: 'root' })
export class RolePermissionMockService implements RolePermissionDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly roles = inject(RoleMockService);
  private readonly activity = inject(ActivityMockService);
  private readonly store = signal<RolePermissions[]>(structuredClone(MOCK_ROLE_PERMISSIONS));

  getAll(): Observable<RolePermissions[]> {
    return mockResponse(() => this.store(), this.latency);
  }

  getForRole(roleId: number): Observable<RolePermissions> {
    return mockResponse(() => {
      this.requireRole(roleId);
      return this.store().find((p) => p.roleId === roleId) ?? { id: roleId, roleId, grants: {} };
    }, this.latency);
  }

  save(roleId: number, grants: PermissionGrants): Observable<RolePermissions> {
    return mockResponse(() => {
      const role = this.requireRole(roleId);
      if (role.name === ADMIN_ROLE_NAME) {
        throw mockError(
          'Conflict',
          `${ADMIN_ROLE_NAME} always has full access and cannot be changed.`,
        );
      }
      const clean: PermissionGrants = {};
      for (const [key, actions] of Object.entries(grants)) {
        const module = PERMISSION_MODULES.find((m) => m.key === key);
        if (!module) {
          throw mockError('Validation', `Unknown module "${key}".`);
        }
        const unique = [...new Set(actions)] as PermissionAction[];
        const invalid = unique.find((a) => !module.actions.includes(a));
        if (invalid) {
          throw mockError('Validation', `${module.label} has no "${invalid}" permission.`);
        }
        if (unique.length > 0 && !unique.includes('View')) {
          throw mockError(
            'Validation',
            `${module.label}: View is required to grant other permissions.`,
            {
              [key]: 'View is required.',
            },
          );
        }
        clean[key] = unique;
      }
      const saved: RolePermissions = { id: roleId, roleId, grants: clean };
      this.store.update((items) => [...items.filter((p) => p.roleId !== roleId), saved]);
      this.activity.record('system', `Permissions of role "${role.name}" updated (simulation)`);
      return saved;
    }, this.latency);
  }

  removeForRole(roleId: number): void {
    this.store.update((items) => items.filter((p) => p.roleId !== roleId));
  }

  private requireRole(roleId: number): Role {
    const role = this.roles.findById(roleId);
    if (!role) {
      throw mockError('NotFound', `Role #${roleId} was not found.`);
    }
    return role;
  }
}

@Injectable({ providedIn: 'root' })
export class GeneralSettingsMockService implements GeneralSettingsDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly activity = inject(ActivityMockService);
  private readonly store = signal<GeneralSettings>(structuredClone(MOCK_GENERAL_SETTINGS));

  get(): Observable<GeneralSettings> {
    return mockResponse(() => this.store(), this.latency);
  }

  save(values: Omit<GeneralSettings, 'id'>): Observable<GeneralSettings> {
    return mockResponse(() => {
      const errors: Record<string, string> = {};
      if (!values.organizationName?.trim())
        errors['organizationName'] = 'Organization name is required.';
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.contactEmail ?? '')) {
        errors['contactEmail'] = 'Enter a valid email address.';
      }
      if (!TIMEZONES.includes(values.timezone)) errors['timezone'] = 'Choose a listed time zone.';
      if (!DATE_FORMATS.includes(values.dateFormat))
        errors['dateFormat'] = 'Choose a listed format.';
      if (!/^[A-Z]{3}$/.test(values.currencyCode ?? '')) {
        errors['currencyCode'] = 'Use a 3-letter code such as MMK.';
      }
      const month = Number(values.fiscalYearStartMonth);
      if (!Number.isInteger(month) || month < 1 || month > 12) {
        errors['fiscalYearStartMonth'] = 'Choose a month.';
      }
      if (Object.keys(errors).length > 0) {
        throw mockError('Validation', 'Please fix the highlighted settings.', errors);
      }
      const saved: GeneralSettings = {
        ...values,
        organizationName: values.organizationName.trim(),
        fiscalYearStartMonth: month,
        id: 1,
      };
      this.store.set(saved);
      this.activity.record('system', 'General settings updated (simulation)');
      return saved;
    }, this.latency);
  }
}
