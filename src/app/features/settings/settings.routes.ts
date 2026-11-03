import { Routes } from '@angular/router';
import { placeholderRoutes } from '../../shared/utilities/placeholder-routes';

export const SETTINGS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'general' },
  ...placeholderRoutes(10, [
    { path: 'general', label: 'General Settings', description: 'Organization-wide preferences.' },
    { path: 'users', label: 'Users', description: 'Application users and status.' },
    { path: 'roles', label: 'Roles', description: 'Roles and role assignment.' },
    {
      path: 'permissions',
      label: 'Permissions',
      description: 'Permission matrix (UI simulation only).',
    },
  ]),
];
