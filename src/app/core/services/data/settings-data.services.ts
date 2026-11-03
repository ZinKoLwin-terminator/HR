import { Observable } from 'rxjs';
import {
  AppUser,
  GeneralSettings,
  PermissionGrants,
  Role,
  RolePermissions,
} from '../../models/settings.models';
import { CrudDataService } from './crud-data.service';

export abstract class UserDataService extends CrudDataService<AppUser> {}
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
