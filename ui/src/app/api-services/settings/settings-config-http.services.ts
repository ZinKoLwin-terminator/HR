import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, throwError } from 'rxjs';
import {
  GeneralSettings,
  PermissionAction,
  PermissionGrants,
  RolePermissions,
} from '../../core/models/settings.models';
import {
  GeneralSettingsDataService,
  RolePermissionDataService,
} from '../../core/services/data/settings-data.services';
import { API_BASE_URL } from '../api-base-url';
import { validation } from '../recruitment/recruitment-http.base';
import { ADMIN_ONLY_GENERAL_MESSAGE, settingsError } from './settings-http.errors';

interface ApiRolePermissions {
  id?: number;
  roleId?: number;
  grants?: Record<string, string[] | null> | null;
  rowVersion?: string;
}

interface ApiMatrix {
  roles?: ApiRolePermissions[];
}

/**
 * HTTP implementation of the permission matrix (admin-only on the server).
 *
 *  - The matrix `rowVersion` of a role is the ROLE's token and a save moves it, so the adapter
 *    keeps the latest one per role from every response and reads the role first when it has none.
 *  - Grants are carried as received. The API knows actions the screen has no column for
 *    (ViewSensitive, EditSensitive, ViewBank, EditBank, Import, Export); they stay in the grant
 *    set and are sent back unchanged on save, so a save never drops a grant the user did not touch.
 *  - Roles with no grants yet come back with an empty grant set.
 */
@Injectable({ providedIn: 'root' })
export class RolePermissionHttpService implements RolePermissionDataService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/role-permissions`;
  private readonly versions = new Map<number, string>();

  getAll(): Observable<RolePermissions[]> {
    return this.http.get<ApiMatrix>(this.url).pipe(
      map((matrix) =>
        (matrix.roles ?? []).map((item) => this.keep(item, item.roleId ?? item.id ?? 0)),
      ),
      catchError((e) => throwError(() => settingsError(e))),
    );
  }

  getForRole(roleId: number): Observable<RolePermissions> {
    return this.http.get<ApiRolePermissions>(`${this.url}/${roleId}`).pipe(
      map((item) => this.keep(item, roleId)),
      catchError((e) => throwError(() => settingsError(e))),
    );
  }

  save(roleId: number, grants: PermissionGrants): Observable<RolePermissions> {
    const body: Record<string, string[]> = {};
    for (const [module, actions] of Object.entries(grants)) {
      if (!Array.isArray(actions)) {
        return throwError(() =>
          validation('The permissions are not valid.', module, 'Choose a list of actions.'),
        );
      }
      body[module] = [...new Set(actions)];
    }
    const version = this.versions.get(roleId);
    const known$ = version
      ? of(version)
      : this.getForRole(roleId).pipe(map(() => this.version(roleId)));
    return known$.pipe(
      switchMap((rowVersion) =>
        this.http.put<ApiRolePermissions>(`${this.url}/${roleId}`, { grants: body, rowVersion }),
      ),
      map((item) => this.keep(item, roleId)),
      catchError((e) => throwError(() => settingsError(e))),
    );
  }

  private version(roleId: number): string {
    return this.versions.get(roleId) ?? '';
  }

  private keep(item: ApiRolePermissions, roleId: number): RolePermissions {
    if (item.rowVersion) {
      this.versions.set(roleId, item.rowVersion);
    }
    const grants: PermissionGrants = {};
    for (const [module, actions] of Object.entries(item.grants ?? {})) {
      // Extra API actions are kept as plain strings even though the screen type has no name for them.
      grants[module] = [...(actions ?? [])] as PermissionAction[];
    }
    return { id: roleId, roleId, grants };
  }
}

/**
 * HTTP implementation of the general settings. Any signed-in user may read them; saving needs
 * the HR Administrator role (403 shows an "administrators only" message). The row's `rowVersion`
 * is kept from the last response and sent with the PUT.
 */
@Injectable({ providedIn: 'root' })
export class GeneralSettingsHttpService implements GeneralSettingsDataService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/general-settings`;
  private rowVersion: string | undefined;

  get(): Observable<GeneralSettings> {
    return this.http.get<GeneralSettings & { rowVersion?: string }>(this.url).pipe(
      map((item) => this.keep(item)),
      catchError((e) => throwError(() => settingsError(e, ADMIN_ONLY_GENERAL_MESSAGE))),
    );
  }

  save(values: Omit<GeneralSettings, 'id'>): Observable<GeneralSettings> {
    const known$ = this.rowVersion
      ? of(this.rowVersion)
      : this.get().pipe(map(() => this.rowVersion ?? ''));
    return known$.pipe(
      switchMap((rowVersion) =>
        this.http.put<GeneralSettings & { rowVersion?: string }>(this.url, {
          organizationName: values.organizationName,
          contactEmail: values.contactEmail,
          timezone: values.timezone,
          dateFormat: values.dateFormat,
          currencyCode: values.currencyCode,
          fiscalYearStartMonth: Number(values.fiscalYearStartMonth),
          emailNotifications: values.emailNotifications,
          weeklyDigest: values.weeklyDigest,
          rowVersion,
        }),
      ),
      map((item) => this.keep(item)),
      catchError((e) => throwError(() => settingsError(e, ADMIN_ONLY_GENERAL_MESSAGE))),
    );
  }

  private keep(item: GeneralSettings & { rowVersion?: string }): GeneralSettings {
    this.rowVersion = item.rowVersion ?? this.rowVersion;
    const { rowVersion: _token, ...settings } = item;
    void _token;
    return settings;
  }
}
