import { Routes } from '@angular/router';

/**
 * Employee self-service ("My ..."). No permission key: the signed-in policy is all the API asks
 * for, and every call returns only the caller's own records.
 */
export const SELF_SERVICE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'payslips' },
  {
    path: 'payslips',
    data: { breadcrumb: 'My Payslips' },
    children: [
      {
        path: '',
        title: 'My Payslips',
        loadComponent: () =>
          import('./my-payslips/my-payslips-list.component').then((m) => m.MyPayslipsListComponent),
      },
      {
        path: ':id',
        title: 'My Payslip',
        data: { breadcrumb: 'Payslip' },
        loadComponent: () =>
          import('./my-payslips/my-payslip-view.component').then((m) => m.MyPayslipViewComponent),
      },
    ],
  },
];
