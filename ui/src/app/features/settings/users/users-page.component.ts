import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { AppUser, USER_STATUSES } from '../../../core/models/settings.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  RoleDataService,
  UserDataService,
} from '../../../core/services/data/settings-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { employeeOptions, nameMap, options } from '../../organization/organization-lookups';

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
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly me = inject(CurrentUserService);
  protected readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        users: this.users.getAll(),
        roles: this.rolesApi.getAll(),
        employees: this.employeesApi.getAll(),
      }),
  });

  protected readonly config = computed<CrudPageConfig<AppUser> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const roleName = nameMap(l.roles, (r) => r.name);
    const taken = new Set(l.users.map((u) => u.employeeId));
    const myId = this.me.current().id;
    return {
      title: 'Users',
      description:
        'Application users and their roles. Demo only: there is no sign-in, and roles are not enforced.',
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
              l.employees.filter((e) => (!e.exitDate && !taken.has(e.id)) || ctx.id !== null),
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
        { key: 'email', label: 'Email', type: 'text', detailsOnly: true },
        { key: 'lastLoginOn', label: 'Last sign-in', type: 'date', detailsOnly: true },
      ],
    };
  });
}
