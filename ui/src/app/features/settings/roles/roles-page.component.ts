import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { Role } from '../../../core/models/settings.models';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import {
  RoleDataService,
  UserDataService,
} from '../../../core/services/data/settings-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

/** Roles and how many users hold each. Permissions are edited on the Permissions screen. */
@Component({
  selector: 'app-roles-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" (changed)="lookups.reload()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading roles" />
    }
  `,
})
export class RolesPageComponent {
  private readonly roles = inject(RoleDataService);
  private readonly usersApi = inject(UserDataService);
  private readonly router = inject(Router);
  private readonly apiMode = inject(SERVER_SESSION);
  protected readonly lookups = rxResource({
    stream: () => forkJoin({ roles: this.roles.getAll(), users: this.usersApi.getAll() }),
  });

  protected readonly config = computed<CrudPageConfig<Role> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const members = (r: Role) => l.users.filter((u) => u.roleId === r.id).length;
    return {
      title: 'Roles',
      description: this.apiMode
        ? 'Roles group users. Built-in roles cannot be renamed or deleted. Only HR Administrators can manage roles.'
        : 'Roles group users. Built-in roles cannot be renamed or deleted. Demo only: roles are not enforced.',
      entityLabel: 'Role',
      service: this.roles,
      hasStatus: false,
      searchLabel: 'Search roles',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (r) => r.name,
      canDelete: (r) => !r.isSystem && members(r) === 0,
      extraActions: [
        {
          id: 'permissions',
          label: 'Edit permissions',
          icon: 'tune',
          silent: true,
          run: (r) => {
            this.router.navigate(['/settings/permissions'], { queryParams: { role: r.id } });
            return of(null);
          },
          successMessage: () => '',
        },
      ],
      columns: [
        { key: 'name', header: 'Role', sortable: true },
        { key: 'description', header: 'Description', hideBelow: 'md' },
        {
          key: 'type',
          header: 'Type',
          hideBelow: 'sm',
          value: (r) => (r.isSystem ? 'Built-in' : 'Custom'),
        },
        {
          key: 'users',
          header: 'Users',
          type: 'number',
          align: 'end',
          value: members,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ status: 'Active' }),
      fields: [
        { key: 'name', label: 'Role name', type: 'text', required: true, maxLength: 40 },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        {
          key: 'description',
          label: 'Description',
          type: 'textarea',
          maxLength: 200,
          fullWidth: true,
        },
      ],
    };
  });
}
