import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, forkJoin, map, of } from 'rxjs';
import { AppUser, Role, USER_STATUSES } from '../../../core/models/settings.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { MIN_PASSWORD_LENGTH, SERVER_SESSION } from '../../../core/services/auth.service';
import {
  RoleDataService,
  UserDataService,
} from '../../../core/services/data/settings-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import {
  OrganizationLookupService,
  employeeOptions,
  nameMap,
  options,
} from '../../organization/organization-lookups';
import { safeValue } from '../../../shared/utilities/resource-value';

const STATUS_OPTIONS = USER_STATUSES.map((s) => ({ value: s, label: s }));

/** Application users linked to employees. A simulation: there is no sign-in and nothing is enforced. */
@Component({
  selector: 'app-users-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading users" />
    }
  `,
})
export class UsersPageComponent {
  private readonly users = inject(UserDataService);
  private readonly rolesApi = inject(RoleDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly me = inject(CurrentUserService);
  /** True in the API build: real accounts, so a create needs an initial password. */
  private readonly apiMode = inject(SERVER_SESSION);
  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        // Each falls back to an empty list, so the list below always renders (with its own error).
        users: this.users.getAll().pipe(catchError(() => of([] as AppUser[]))),
        roles: this.rolesApi.getAll().pipe(catchError(() => of([] as Role[]))),
        // Full records with employees:View; otherwise only what the role may read (never fails).
        employees: this.lookupService
          .people()
          .pipe(map((p) => (p.employeeRecords.length ? p.employeeRecords : p.employees))),
      }),
  });

  protected readonly config = computed<CrudPageConfig<AppUser> | null>(() => {
    const l = safeValue(this.lookups);
    if (!l) {
      return null;
    }
    const roleName = nameMap(l.roles, (r) => r.name);
    const taken = new Set(l.users.map((u) => u.employeeId));
    const myId = this.me.current().id;
    return {
      title: 'Users',
      description: this.apiMode
        ? 'Application users and their roles. Only HR Administrators can manage users.'
        : 'Application users and their roles. Demo only: there is no sign-in, and roles are not enforced.',
      entityLabel: 'User',
      service: this.users,
      hasStatus: false,
      searchLabel: 'Search by name or email',
      defaultSort: { active: 'displayName', direction: 'asc' },
      filters: [
        { key: 'roleId', label: 'Role', options: options(l.roles, (r) => r.name) },
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
      ],
      nameOf: (u) => u.displayName,
      canDelete: (u) => u.id !== myId,
      extraActions: [
        {
          id: 'unlock',
          label: 'Unlock account',
          icon: 'lock_open',
          visible: (u) => u.status === 'Locked',
          run: (u) => this.users.update(u.id, { status: 'Active' }),
          successMessage: (u) => `${u.displayName} unlocked.`,
        },
        {
          id: 'password-reset',
          label: 'Send password reset',
          icon: 'mail_lock',
          // Not on your own row: change your own password from your profile.
          visible: (u) => u.id !== myId,
          confirm: {
            title: 'Send password reset?',
            message: (u) =>
              `Send a password reset email to ${u.displayName}? They will choose a new password from the link.`,
            confirmText: 'Send email',
          },
          run: (u) => this.users.sendPasswordReset(u.id),
          successMessage: () => 'Password reset email sent.',
        },
      ],
      columns: [
        { key: 'displayName', header: 'User', sortable: true },
        { key: 'email', header: 'Email', hideBelow: 'md' },
        {
          key: 'roleId',
          header: 'Role',
          sortable: true,
          hideBelow: 'sm',
          value: (u) => roleName(u.roleId),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
        { key: 'lastLoginOn', header: 'Last sign-in', type: 'date', hideBelow: 'lg', nowrap: true },
      ],
      newItem: () => ({ status: 'Active' }),
      fields: [
        {
          key: 'employeeId',
          label: 'Employee',
          type: 'select',
          required: true,
          fullWidth: true,
          lockedOnEdit: true,
          hint: 'Name and email come from the employee record',
          options: (_values, ctx) =>
            employeeOptions(
              l.employees.filter(
                (e) => (!('exitDate' in e && e.exitDate) && !taken.has(e.id)) || ctx.id !== null,
              ),
            ),
        },
        {
          key: 'roleId',
          label: 'Role',
          type: 'select',
          required: true,
          options: () =>
            options(
              l.roles.filter((r) => r.status === 'Active'),
              (r) => r.name,
            ),
        },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        ...(this.apiMode
          ? [
              {
                key: 'initialPassword',
                label: 'Initial password',
                type: 'password' as const,
                required: true,
                createOnly: true,
                fullWidth: true,
                minLength: MIN_PASSWORD_LENGTH,
                hint: `At least ${MIN_PASSWORD_LENGTH} characters. Share it with the user in person; it is never shown again.`,
              },
            ]
          : []),
        { key: 'email', label: 'Email', type: 'text', detailsOnly: true },
        { key: 'lastLoginOn', label: 'Last sign-in', type: 'date', detailsOnly: true },
      ],
    };
  });
}
