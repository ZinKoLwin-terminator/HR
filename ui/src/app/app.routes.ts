import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'forgot-password',
    title: 'Forgot password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
  },
  {
    path: 'reset-password',
    title: 'Reset password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent,
      ),
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard',
        data: { breadcrumb: 'Dashboard', permission: 'dashboard:View' },
        canActivate: [permissionGuard],
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'organization',
        data: { breadcrumb: 'Organization', permission: 'organization:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/organization/organization.routes').then((m) => m.ORGANIZATION_ROUTES),
      },
      {
        path: 'employees',
        data: { breadcrumb: 'Employees', permission: 'employees:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/employees/employees.routes').then((m) => m.EMPLOYEE_ROUTES),
      },
      {
        path: 'attendance',
        data: { breadcrumb: 'Attendance', permission: 'attendance:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/attendance/attendance.routes').then((m) => m.ATTENDANCE_ROUTES),
      },
      {
        path: 'leave',
        data: { breadcrumb: 'Leave Management', permission: 'leave:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/leave-management/leave.routes').then((m) => m.LEAVE_ROUTES),
      },
      {
        path: 'payroll',
        data: { breadcrumb: 'Payroll', permission: 'payroll:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/payroll/payroll.routes').then((m) => m.PAYROLL_ROUTES),
      },
      {
        path: 'recruitment',
        data: { breadcrumb: 'Recruitment', permission: 'recruitment:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/recruitment/recruitment.routes').then((m) => m.RECRUITMENT_ROUTES),
      },
      {
        path: 'my',
        data: { breadcrumb: 'My' },
        loadChildren: () =>
          import('./features/self-service/self-service.routes').then((m) => m.SELF_SERVICE_ROUTES),
      },
      {
        path: 'data-exchange',
        data: { breadcrumb: 'Data Exchange', permission: 'excel:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/data-exchange/data-exchange.routes').then(
            (m) => m.DATA_EXCHANGE_ROUTES,
          ),
      },
      {
        path: 'settings',
        data: { breadcrumb: 'Settings', permission: 'settings:View' },
        canActivate: [permissionGuard],
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: 'help',
        data: { breadcrumb: 'Help' },
        loadChildren: () => import('./features/help/help.routes').then((m) => m.HELP_ROUTES),
      },
      {
        path: 'forbidden',
        title: 'No access',
        data: { breadcrumb: 'No access' },
        loadComponent: () =>
          import('./shared/components/forbidden/forbidden.component').then(
            (m) => m.ForbiddenComponent,
          ),
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
