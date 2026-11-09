import { Routes } from '@angular/router';

export const ORGANIZATION_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'companies' },
  {
    path: 'companies',
    title: 'Companies',
    data: { breadcrumb: 'Companies' },
    loadComponent: () =>
      import('./companies/companies-page.component').then((m) => m.CompaniesPageComponent),
  },
  {
    path: 'branches',
    title: 'Branches',
    data: { breadcrumb: 'Branches' },
    loadComponent: () =>
      import('./branches/branches-page.component').then((m) => m.BranchesPageComponent),
  },
  {
    path: 'departments',
    title: 'Departments',
    data: { breadcrumb: 'Departments' },
    loadComponent: () =>
      import('./departments/departments-page.component').then((m) => m.DepartmentsPageComponent),
  },
  {
    path: 'sections',
    title: 'Sections',
    data: { breadcrumb: 'Sections' },
    loadComponent: () =>
      import('./sections/sections-page.component').then((m) => m.SectionsPageComponent),
  },
  {
    path: 'positions',
    title: 'Positions',
    data: { breadcrumb: 'Positions' },
    loadComponent: () =>
      import('./positions/positions-page.component').then((m) => m.PositionsPageComponent),
  },
  {
    path: 'job-grades',
    title: 'Job Grades',
    data: { breadcrumb: 'Job Grades' },
    loadComponent: () =>
      import('./job-grades/job-grades-page.component').then((m) => m.JobGradesPageComponent),
  },
  {
    path: 'reporting-structure',
    title: 'Reporting Structure',
    data: { breadcrumb: 'Reporting Structure' },
    loadComponent: () =>
      import('./reporting-structure/reporting-structure.component').then(
        (m) => m.ReportingStructureComponent,
      ),
  },
];
