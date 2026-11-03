import { Routes } from '@angular/router';
import { placeholderRoutes } from '../../shared/utilities/placeholder-routes';

export const PAYROLL_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'processing' },
  ...placeholderRoutes(8, [
    {
      path: 'salary-structures',
      label: 'Salary Structures',
      description: 'Salary templates by grade.',
    },
    {
      path: 'salary-components',
      label: 'Salary Components',
      description: 'Allowances and deductions.',
    },
    {
      path: 'employee-salaries',
      label: 'Employee Salaries',
      description: 'Salary assignments per employee.',
    },
    {
      path: 'processing',
      label: 'Payroll Processing',
      description: 'Run and review payroll periods.',
    },
    { path: 'payslips', label: 'Payslips', description: 'Preview sample payslips.' },
    { path: 'history', label: 'Payroll History', description: 'Past payroll periods.' },
  ]),
];
