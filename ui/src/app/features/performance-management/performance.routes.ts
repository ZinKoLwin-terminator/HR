import { inject } from '@angular/core';
import { RedirectFunction, Routes } from '@angular/router';
import { NAVIGATION, firstVisibleRoute } from '../../core/constants/navigation';
import { permissionGuard } from '../../core/guards/permission.guard';
import { PermissionService } from '../../core/services/permission.service';

/** The employee's screens (dashboard, My KPIs, one scorecard, reports) need `kpi:View`. */
const EMPLOYEE = { permission: 'kpi:View' } as const;
/** The reviewer's screens (queue and review) need `kpi:Approve`. */
const REVIEWER = { permission: 'kpi:Approve' } as const;

/**
 * `/performance` opens the first Performance screen the user's role may see, in sidebar order
 * (the KPI dashboard for everyone with `kpi:View`), so an employee is never sent to the HR setup.
 * Without any, the dashboard, whose guard then shows the no-access page.
 */
export const performanceLanding: RedirectFunction = () => {
  const permissions = inject(PermissionService);
  const group = NAVIGATION.filter((item) => item.label === 'Performance');
  return firstVisibleRoute(group, (key) => permissions.can(key)) ?? '/performance/dashboard';
};

/** Setup and planning (HR) need `kpi-setup:View`; the actions inside use Create, Edit and Delete. */
const SETUP = { permission: 'kpi-setup:View' } as const;

/**
 * KPI / performance, under `/performance`. Every route carries its own permission (UX only; the
 * server enforces the same keys), so the employee and reviewer screens (My KPIs with `kpi:View`,
 * Reviews with `kpi:Approve`) can sit beside the HR setup without needing `kpi-setup`.
 */
export const PERFORMANCE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: performanceLanding },
  {
    path: 'dashboard',
    title: 'KPI Dashboard',
    data: { breadcrumb: 'KPI Dashboard', ...EMPLOYEE },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./dashboard/kpi-dashboard.component').then((m) => m.KpiDashboardComponent),
  },
  {
    path: 'my-kpis',
    data: { breadcrumb: 'My KPIs', ...EMPLOYEE },
    canActivate: [permissionGuard],
    children: [
      {
        path: '',
        title: 'My KPIs',
        loadComponent: () => import('./my-kpis/my-kpis.component').then((m) => m.MyKpisComponent),
      },
      {
        path: ':id',
        title: 'My Scorecard',
        data: { breadcrumb: 'Scorecard' },
        loadComponent: () =>
          import('./my-kpis/my-scorecard.component').then((m) => m.MyScorecardComponent),
      },
    ],
  },
  {
    path: 'reviews',
    data: { breadcrumb: 'KPI Reviews', ...REVIEWER },
    canActivate: [permissionGuard],
    children: [
      {
        path: '',
        title: 'KPI Reviews',
        loadComponent: () =>
          import('./reviews/review-queue.component').then((m) => m.ReviewQueueComponent),
      },
      {
        path: ':id',
        title: 'KPI Review',
        data: { breadcrumb: 'Review' },
        loadComponent: () =>
          import('./reviews/review-detail.component').then((m) => m.ReviewDetailComponent),
      },
    ],
  },
  {
    path: 'reports',
    title: 'KPI Reports',
    data: { breadcrumb: 'KPI Reports', ...EMPLOYEE },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./reports/kpi-reports.component').then((m) => m.KpiReportsComponent),
  },
  {
    path: 'assignments',
    data: { breadcrumb: 'KPI Assignments', ...SETUP },
    canActivate: [permissionGuard],
    children: [
      {
        path: '',
        title: 'KPI Assignments',
        loadComponent: () =>
          import('./assignments/assignment-list.component').then((m) => m.AssignmentListComponent),
      },
      {
        path: ':id',
        title: 'KPI Plan',
        data: { breadcrumb: 'Plan' },
        loadComponent: () =>
          import('./assignments/plan-detail.component').then((m) => m.PlanDetailComponent),
      },
    ],
  },
  {
    path: 'templates',
    title: 'KPI Templates',
    data: { breadcrumb: 'KPI Templates', ...SETUP },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./templates/kpi-templates-page.component').then((m) => m.KpiTemplatesPageComponent),
  },
  {
    path: 'categories',
    title: 'KPI Categories',
    data: { breadcrumb: 'KPI Categories', ...SETUP },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./categories/kpi-categories-page.component').then(
        (m) => m.KpiCategoriesPageComponent,
      ),
  },
  {
    path: 'periods',
    title: 'Evaluation Periods',
    data: { breadcrumb: 'Evaluation Periods', ...SETUP },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./periods/evaluation-periods-page.component').then(
        (m) => m.EvaluationPeriodsPageComponent,
      ),
  },
  {
    path: 'rating-bands',
    title: 'Rating Bands',
    data: { breadcrumb: 'Rating Bands', ...SETUP },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./rating-bands/rating-bands-page.component').then((m) => m.RatingBandsPageComponent),
  },
];
