import { Routes } from '@angular/router';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

export const SETTINGS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'general' },
  {
    path: 'general',
    title: 'General Settings',
    data: { breadcrumb: 'General Settings' },
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () =>
      import('./general/general-settings.component').then((m) => m.GeneralSettingsComponent),
  },
  {
    path: 'users',
    title: 'Users',
    data: { breadcrumb: 'Users' },
    loadComponent: () => import('./users/users-page.component').then((m) => m.UsersPageComponent),
  },
  {
    path: 'roles',
    title: 'Roles',
    data: { breadcrumb: 'Roles' },
    loadComponent: () => import('./roles/roles-page.component').then((m) => m.RolesPageComponent),
  },
  {
    path: 'permissions',
    title: 'Permissions',
    data: { breadcrumb: 'Permissions' },
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () =>
      import('./permissions/permissions-page.component').then((m) => m.PermissionsPageComponent),
  },
];
