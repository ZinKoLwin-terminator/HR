import { Routes } from '@angular/router';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard',
        data: { breadcrumb: 'Dashboard' },
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'organization',
        data: { breadcrumb: 'Organization' },
        loadChildren: () =>
          import('./features/organization/organization.routes').then((m) => m.ORGANIZATION_ROUTES),
      },
      {
        path: 'employees',
        data: { breadcrumb: 'Employees' },
        loadChildren: () =>
          import('./features/employees/employees.routes').then((m) => m.EMPLOYEE_ROUTES),
      },
      {
        path: 'attendance',
        data: { breadcrumb: 'Attendance' },
        loadChildren: () =>
          import('./features/attendance/attendance.routes').then((m) => m.ATTENDANCE_ROUTES),
      },
      {
        path: 'leave',
        data: { breadcrumb: 'Leave Management' },
        loadChildren: () =>
          import('./features/leave-management/leave.routes').then((m) => m.LEAVE_ROUTES),
      },
      {
        path: 'payroll',
        data: { breadcrumb: 'Payroll' },
        loadChildren: () =>
          import('./features/payroll/payroll.routes').then((m) => m.PAYROLL_ROUTES),
      },
      {
        path: 'recruitment',
        data: { breadcrumb: 'Recruitment' },
        loadChildren: () =>
          import('./features/recruitment/recruitment.routes').then((m) => m.RECRUITMENT_ROUTES),
      },
      {
        path: 'data-exchange',
        data: { breadcrumb: 'Data Exchange' },
        loadChildren: () =>
          import('./features/data-exchange/data-exchange.routes').then(
            (m) => m.DATA_EXCHANGE_ROUTES,
          ),
      },
      {
        path: 'settings',
        data: { breadcrumb: 'Settings' },
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: '**',
        title: 'Page not found',
        data: { breadcrumb: 'Not found' },
        loadComponent: () =>
          import('./shared/components/not-found/not-found.component').then(
            (m) => m.NotFoundComponent,
          ),
      },
    ],
  },
];
