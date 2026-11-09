import { Routes } from '@angular/router';

export const PAYROLL_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'processing' },
  {
    path: 'salary-structures',
    title: 'Salary Structures',
    data: { breadcrumb: 'Salary Structures' },
    loadComponent: () =>
      import('./salary-structures/salary-structures-page.component').then(
        (m) => m.SalaryStructuresPageComponent,
      ),
  },
  {
    path: 'salary-components',
    title: 'Salary Components',
    data: { breadcrumb: 'Salary Components' },
    loadComponent: () =>
      import('./salary-components/salary-components-page.component').then(
        (m) => m.SalaryComponentsPageComponent,
      ),
  },
  {
    path: 'employee-salaries',
    title: 'Employee Salaries',
    data: { breadcrumb: 'Employee Salaries' },
    loadComponent: () =>
      import('./employee-salaries/employee-salaries-page.component').then(
        (m) => m.EmployeeSalariesPageComponent,
      ),
  },
  {
    path: 'processing',
    data: { breadcrumb: 'Payroll Processing' },
    children: [
      {
        path: '',
        title: 'Payroll Processing',
        loadComponent: () =>
          import('./payroll-processing/payroll-processing-page.component').then(
            (m) => m.PayrollProcessingPageComponent,
          ),
      },
      {
        path: ':id',
        title: 'Payroll Review',
        data: { breadcrumb: 'Review' },
        loadComponent: () =>
          import('./payroll-review/payroll-review.component').then((m) => m.PayrollReviewComponent),
      },
    ],
  },
  {
    path: 'payslips',
    data: { breadcrumb: 'Payslips' },
    children: [
      {
        path: '',
        title: 'Payslips',
        loadComponent: () =>
          import('./payslips/payslips-list.component').then((m) => m.PayslipsListComponent),
      },
      {
        path: ':id',
        title: 'Payslip',
        data: { breadcrumb: 'Payslip' },
        loadComponent: () =>
          import('./payslips/payslip-view.component').then((m) => m.PayslipViewComponent),
      },
    ],
  },
  {
    path: 'history',
    title: 'Payroll History',
    data: { breadcrumb: 'Payroll History' },
    loadComponent: () =>
      import('./payroll-history/payroll-history.component').then((m) => m.PayrollHistoryComponent),
  },
];
