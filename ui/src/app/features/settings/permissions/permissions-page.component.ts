import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { filter } from 'rxjs';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import {
  ADMIN_ROLE_NAME,
  PERMISSION_ACTIONS,
  PERMISSION_MODULES,
  PermissionAction,
  PermissionGrants,
} from '../../../core/models/settings.models';
import { SERVER_SESSION } from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import {
  RoleDataService,
  RolePermissionDataService,
} from '../../../core/services/data/settings-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

/** Normalised, comparable form of a grant set. */
const normalise = (grants: PermissionGrants): string =>
  JSON.stringify(PERMISSION_MODULES.map((m) => [m.key, [...(grants[m.key] ?? [])].sort()]));

/**
 * Permission matrix per role. A UI simulation: the choices are stored in memory and are not
 * enforced anywhere in the application.
 */
@Component({
  selector: 'app-permissions-page',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    EmptyStateComponent,
  ],
  template: `
    <app-page-header
      title="Permissions"
      description="Choose what each role can do in every module."
    />

    @if (!apiMode) {
      <p class="notice" role="note">
        <mat-icon aria-hidden="true">info</mat-icon>
        Simulation only — these permissions are stored in memory and are not enforced anywhere in
        this demo.
      </p>
    }

    @if (roles.error() || permissions.error()) {
      <app-empty-state icon="error" title="Permissions could not be loaded">
        <button mat-flat-button type="button" (click)="roles.reload(); permissions.reload()">
          Try again
        </button>
      </app-empty-state>
    } @else if (roles.value(); as roleList) {
      <div class="toolbar">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Role</mat-label>
          <mat-select [value]="roleId()" (selectionChange)="changeRole($event.value)">
            @for (r of roleList; track r.id) {
              <mat-option [value]="r.id">{{ r.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <span class="spacer"></span>
        <button mat-stroked-button type="button" [disabled]="!dirty()" (click)="reset()">
          Reset
        </button>
        <button
          mat-flat-button
          type="button"
          [disabled]="!dirty() || locked() || !access.can('settings:Edit')"
          (click)="save()"
        >
          Save permissions
        </button>
      </div>

      @if (locked()) {
        <p class="notice" role="note">
          <mat-icon aria-hidden="true">lock</mat-icon>
          {{ adminRole }} always has full access, so its permissions cannot be changed.
        </p>
      }

      @if (permissions.value()) {
        <div class="table-wrap">
          <table class="matrix">
            <caption class="sr-only">
              Permissions of
              {{
                roleName()
              }}
            </caption>
            <thead>
              <tr>
                <th scope="col">Module</th>
                @for (action of actions; track action) {
                  <th scope="col">{{ action }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (module of modules; track module.key) {
                <tr>
                  <th scope="row">{{ module.label }}</th>
                  @for (action of actions; track action) {
                    <td>
                      @if (module.actions.includes(action)) {
                        <mat-checkbox
                          [checked]="has(module.key, action)"
                          [disabled]="locked()"
                          [aria-label]="module.label + ' ' + action"
                          (change)="toggle(module.key, action, $event.checked)"
                        />
                      } @else {
                        <span aria-hidden="true">—</span>
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <mat-progress-bar mode="indeterminate" aria-label="Loading permissions" />
      }
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading roles" />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .notice {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      padding: 10px 14px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
      font: var(--mat-sys-body-medium);
    }
    .toolbar {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
    .spacer {
      flex: 1;
    }
    .table-wrap {
      overflow-x: auto;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--app-radius-md);
    }
    .matrix {
      width: 100%;
      border-collapse: collapse;
      min-width: 520px;
    }
    .matrix th,
    .matrix td {
      padding: 4px 12px;
      text-align: center;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .matrix th[scope='row'],
    .matrix thead th:first-child {
      text-align: start;
    }
    .matrix thead th {
      font: var(--mat-sys-label-large);
      background: var(--mat-sys-surface-container-low);
    }
    .matrix tbody tr:last-child > * {
      border-bottom: 0;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }
  `,
})
export class PermissionsPageComponent implements HasUnsavedChanges {
  /** `?role=` query parameter (set by "Edit permissions" on the Roles screen). */
  readonly role = input<string>();

  private readonly rolesApi = inject(RoleDataService);
  private readonly permissionsApi = inject(RolePermissionDataService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  protected readonly access = inject(PermissionService);
  /** True in the API build, where saved permissions live on the server. */
  protected readonly apiMode = inject(SERVER_SESSION);

  protected readonly modules = PERMISSION_MODULES;
  protected readonly actions = PERMISSION_ACTIONS;
  protected readonly adminRole = ADMIN_ROLE_NAME;

  protected readonly roles = rxResource({ stream: () => this.rolesApi.getAll() });
  protected readonly roleId = linkedSignal<number | null>(() => {
    const list = this.roles.value();
    if (!list?.length) {
      return null;
    }
    const wanted = Number(this.role());
    return list.some((r) => r.id === wanted) ? wanted : list[0].id;
  });
  protected readonly permissions = rxResource({
    params: () => this.roleId() ?? undefined,
    stream: ({ params }) => this.permissionsApi.getForRole(params),
  });
  protected readonly grants = linkedSignal<PermissionGrants>(() =>
    structuredClone(this.permissions.value()?.grants ?? {}),
  );

  protected readonly roleName = computed(
    () => this.roles.value()?.find((r) => r.id === this.roleId())?.name ?? '',
  );
  protected readonly locked = computed(() => this.roleName() === ADMIN_ROLE_NAME);
  protected readonly dirty = computed(
    () => normalise(this.grants()) !== normalise(this.permissions.value()?.grants ?? {}),
  );

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  protected has(moduleKey: string, action: PermissionAction): boolean {
    return (this.grants()[moduleKey] ?? []).includes(action);
  }

  /** Granting anything needs View; removing View removes everything else in the module. */
  protected toggle(moduleKey: string, action: PermissionAction, checked: boolean): void {
    const current = new Set(this.grants()[moduleKey] ?? []);
    if (checked) {
      current.add(action);
      current.add('View');
    } else if (action === 'View') {
      current.clear();
    } else {
      current.delete(action);
    }
    this.grants.update((g) => ({ ...g, [moduleKey]: [...current] }));
  }

  protected changeRole(id: number): void {
    if (id === this.roleId()) return;
    if (!this.dirty()) {
      this.roleId.set(id);
      return;
    }
    this.confirm
      .confirm({
        title: 'Discard unsaved changes?',
        message: 'The permission changes for this role have not been saved.',
        confirmText: 'Discard changes',
        cancelText: 'Keep editing',
        destructive: true,
      })
      .pipe(filter(Boolean))
      .subscribe(() => this.roleId.set(id));
  }

  protected reset(): void {
    this.grants.set(structuredClone(this.permissions.value()?.grants ?? {}));
  }

  protected save(): void {
    const id = this.roleId();
    if (id === null) return;
    this.permissionsApi.save(id, this.grants()).subscribe({
      next: () => {
        this.toast.success(
          this.apiMode
            ? `Permissions of ${this.roleName()} saved.`
            : `Permissions of ${this.roleName()} saved (simulation).`,
        );
        this.permissions.reload();
      },
      error: (error: unknown) => this.toast.error(error),
    });
  }
}
