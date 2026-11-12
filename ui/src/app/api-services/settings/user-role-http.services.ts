import { Injectable } from '@angular/core';
import {
  EMPTY,
  Observable,
  catchError,
  expand,
  map,
  of,
  reduce,
  switchMap,
  throwError,
} from 'rxjs';
import { CreateModel, UpdateModel } from '../../core/models/common.models';
import {
  AppUser,
  BUILT_IN_ADMIN_EMPLOYEE_ID,
  Role,
  UserStatus,
} from '../../core/models/settings.models';
import { MIN_PASSWORD_LENGTH } from '../../core/services/auth.service';
import { RoleDataService, UserDataService } from '../../core/services/data/settings-data.services';
import {
  ApiShape,
  RecruitmentHttpBase,
  Versioned,
  conflict,
  textOrNull,
  validation,
} from '../recruitment/recruitment-http.base';
import { passwordResetError, settingsError } from './settings-http.errors';

/** Largest page the API serves (`Paging.MaxPageSize`). */
const PAGE_SIZE = 200;

/** Shared error mapping of the settings resources (admin-only 403 message). */
abstract class SettingsHttpBase<T extends { id: number }> extends RecruitmentHttpBase<T> {
  protected override fail(error: unknown): Observable<never> {
    return throwError(() => settingsError(error));
  }
}

/**
 * HTTP implementation of the user contract (admin-only on the server).
 *
 *  - There is no unpaged read: `getAll` pages `/users/query` until every row is read.
 *  - Create sends `initialPassword` (at least 12 characters, refused locally before any request).
 *    The password only lives in that request body: it is not kept, logged or put into an error.
 *  - PUT carries only the role. Status moves use the commands: Active from Inactive is
 *    `activate`, Active from Locked is `unlock`, Inactive is `deactivate`. `Locked` cannot be
 *    requested, it is the result of failed sign-ins.
 *  - The built-in administrator has no employee (API null); the screens get `0` as the mock did.
 */
@Injectable({ providedIn: 'root' })
export class UserHttpService extends SettingsHttpBase<AppUser> implements UserDataService {
  protected readonly resource = 'users';
  protected readonly filterKeys = ['roleId', 'status'];
  protected readonly sortKeys = ['id', 'displayName', 'email', 'roleId', 'status', 'lastLoginOn'];

  protected normalise(raw: ApiShape<AppUser>): Versioned<AppUser> {
    return {
      ...raw,
      employeeId: raw.employeeId ?? BUILT_IN_ADMIN_EMPLOYEE_ID,
      displayName: raw.displayName ?? '',
      email: raw.email ?? '',
    } as Versioned<AppUser>;
  }

  override getAll(): Observable<AppUser[]> {
    let next = 0;
    const read = () => this.query({ pageIndex: next++, pageSize: PAGE_SIZE });
    return read().pipe(
      expand((page) => (page.items.length > 0 && next * PAGE_SIZE < page.total ? read() : EMPTY)),
      reduce((all, page) => all.concat(page.items), [] as AppUser[]),
    );
  }

  create(model: CreateModel<AppUser>): Observable<AppUser> {
    const from = model as unknown as Record<string, unknown>;
    const password = from['initialPassword'];
    if (typeof password !== 'string' || password.length < MIN_PASSWORD_LENGTH) {
      return throwError(() =>
        validation(
          `The initial password needs at least ${MIN_PASSWORD_LENGTH} characters.`,
          'initialPassword',
          `Use at least ${MIN_PASSWORD_LENGTH} characters.`,
        ),
      );
    }
    const status = from['status'] === 'Inactive' ? 'Inactive' : 'Active';
    return this.post('', {
      employeeId: Number(from['employeeId']),
      roleId: Number(from['roleId']),
      status,
      initialPassword: password,
    }).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<AppUser>): Observable<AppUser> {
    const { rowVersion, roleId, status } = changes as {
      rowVersion?: string;
      roleId?: number;
      status?: UserStatus;
    };
    return this.current(id).pipe(
      switchMap((current) =>
        roleId === undefined || Number(roleId) === current.roleId
          ? of(current)
          : this.put(id, { roleId: Number(roleId) }, rowVersion ?? current.rowVersion ?? ''),
      ),
      switchMap((saved) =>
        status === undefined || status === saved.status
          ? of(saved)
          : this.moveStatus(id, saved.status, status),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  changeStatus(id: number, status: UserStatus): Observable<AppUser> {
    return this.update(id, { status } as UpdateModel<AppUser>);
  }

  /** Emails a reset link to the user (202, no body). The server answers 503 when email is not configured. */
  sendPasswordReset(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/password-reset`, null).pipe(
      map(() => undefined),
      catchError((e) => throwError(() => passwordResetError(e))),
    );
  }

  private moveStatus(id: number, from: UserStatus, to: UserStatus): Observable<Versioned<AppUser>> {
    if (to === 'Locked') {
      return throwError(() =>
        conflict('An account is locked by failed sign-ins and cannot be locked by hand.'),
      );
    }
    const command = to === 'Inactive' ? 'deactivate' : from === 'Locked' ? 'unlock' : 'activate';
    return this.command(id, command);
  }
}

/**
 * HTTP implementation of the role contract (admin-only on the server). Create and PUT send the
 * name and description; status moves use `activate` and `deactivate`, never the PUT.
 */
@Injectable({ providedIn: 'root' })
export class RoleHttpService extends SettingsHttpBase<Role> implements RoleDataService {
  protected readonly resource = 'roles';
  protected readonly filterKeys = ['status'];
  protected readonly sortKeys = ['id', 'name', 'description', 'isSystem', 'status'];

  protected normalise(raw: ApiShape<Role>): Versioned<Role> {
    return { ...raw, description: raw.description ?? '' } as Versioned<Role>;
  }

  create(model: CreateModel<Role>): Observable<Role> {
    const from = model as unknown as Record<string, unknown>;
    const status = from['status'] === 'Inactive' ? 'Inactive' : 'Active';
    return this.post('', {
      name: from['name'],
      description: textOrNull(from['description']),
      status,
    }).pipe(catchError((e) => this.fail(e)));
  }

  update(id: number, changes: UpdateModel<Role>): Observable<Role> {
    const { rowVersion, name, description, status } = changes as {
      rowVersion?: string;
      name?: string;
      description?: string;
      status?: Role['status'];
    };
    return this.current(id).pipe(
      switchMap((current) => {
        const nextName = name ?? current.name;
        const nextDescription = description ?? current.description;
        return nextName === current.name && nextDescription === current.description
          ? of(current)
          : this.put(
              id,
              { name: nextName, description: textOrNull(nextDescription) },
              rowVersion ?? current.rowVersion ?? '',
            );
      }),
      switchMap((saved) =>
        status === undefined || status === saved.status
          ? of(saved)
          : this.command(id, status === 'Active' ? 'activate' : 'deactivate'),
      ),
      catchError((e) => this.fail(e)),
    );
  }

  changeStatus(id: number, status: Role['status']): Observable<Role> {
    return this.update(id, { status } as UpdateModel<Role>);
  }
}
