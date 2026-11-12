import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';

export const DATA_EXCHANGE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'import' },
  {
    path: 'import',
    title: 'Import Data',
    data: { breadcrumb: 'Import Data', permission: 'excel:Import' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./import/import-page.component').then((m) => m.ImportPageComponent),
  },
  {
    path: 'history',
    title: 'Import History',
    data: { breadcrumb: 'Import History' },
    loadComponent: () =>
      import('./import-history/import-history.component').then((m) => m.ImportHistoryComponent),
  },
];
