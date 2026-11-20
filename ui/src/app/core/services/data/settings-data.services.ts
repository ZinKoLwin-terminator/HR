import { Observable } from 'rxjs';
import {
  AppUser,
  GeneralSettings,
  PermissionGrants,
  Role,
  RolePermissions,
} from '../../models/settings.models';
import { CrudDataService } from './crud-data.service';

export abstract class UserDataService extends CrudDataService<AppUser> {
  /**
   * Emails the user a link to choose a new password (administrators only). Refused with
   * `Conflict` for an inactive account or a leaver.
   */
  abstract sendPasswordReset(id: number): Observable<void>;
  /**
   * `POST /users/{id}/link-employee` (body `{ rowVersion }`): links an UNLINKED account to the
   * current employee whose work email is the account's sign-in email (trimmed, case-insensitive).
   * The server picks the employee; there is no employee id to send. Answers with the updated user
   * (employee and display name set) and ends that account's sessions, so its owner signs in again.
   * Refused with `Conflict` (server text shown as is) when the account is already linked, no current
   * employee has that work email, or that employee already has an account; a stale `rowVersion` is
   * the usual `Conflict`. `rowVersion` is optional: when left out, the last one seen is used.
   */
  abstract linkEmployee(id: number, rowVersion?: string): Observable<AppUser>;
}
export abstract class RoleDataService extends CrudDataService<Role> {}

/** Role permission matrix (UI simulation only — nothing is enforced). */
export abstract class RolePermissionDataService {
  abstract getAll(): Observable<RolePermissions[]>;
  /** An empty grant set is returned for a role that has none yet. */
  abstract getForRole(roleId: number): Observable<RolePermissions>;
  abstract save(roleId: number, grants: PermissionGrants): Observable<RolePermissions>;
}

export abstract class GeneralSettingsDataService {
  abstract get(): Observable<GeneralSettings>;
  abstract save(values: Omit<GeneralSettings, 'id'>): Observable<GeneralSettings>;
}
