import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

export const EMPLOYEE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Employee List',
    data: { breadcrumb: 'Employee List' },
    loadComponent: () =>
      import('./employee-list/employee-list.component').then((m) => m.EmployeeListComponent),
  },
  {
    path: 'new',
    title: 'Add Employee',
    data: { breadcrumb: 'Add Employee', permission: 'employees:Create' },
    canActivate: [permissionGuard],
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () =>
      import('./employee-form/employee-form-page.component').then(
        (m) => m.EmployeeFormPageComponent,
      ),
  },
  {
    path: 'documents',
    title: 'Employee Documents',
    data: { breadcrumb: 'Employee Documents', permission: 'employees:ViewSensitive' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./employee-documents/employee-documents-page.component').then(
        (m) => m.EmployeeDocumentsPageComponent,
      ),
  },
  {
    path: ':id',
    data: { breadcrumb: 'Employee Profile' },
    children: [
      {
        path: '',
        title: 'Employee Profile',
        loadComponent: () =>
          import('./employee-profile/employee-profile.component').then(
            (m) => m.EmployeeProfileComponent,
          ),
      },
      {
        path: 'letters',
        title: 'Letters',
        data: { breadcrumb: 'Letters', permission: 'employees:View' },
        canActivate: [permissionGuard],
        loadComponent: () =>
          import('./employee-letters/employee-letters.component').then(
            (m) => m.EmployeeLettersComponent,
          ),
      },
      {
        path: 'edit',
        title: 'Edit Employee',
        data: { breadcrumb: 'Edit', permission: 'employees:Edit' },
        canActivate: [permissionGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () =>
          import('./employee-form/employee-form-page.component').then(
            (m) => m.EmployeeFormPageComponent,
          ),
      },
    ],
  },
];
