import { inject } from '@angular/core';
import { RedirectFunction, Routes } from '@angular/router';
import { NAVIGATION, firstVisibleRoute } from '../../core/constants/navigation';
import { permissionGuard } from '../../core/guards/permission.guard';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';
import { PermissionService } from '../../core/services/permission.service';

/** The employee's own grievances: reading needs `grievance:View`. */
const OWN_VIEW = { permission: 'grievance:View' } as const;
/** Writing a draft (new, edit) needs `grievance:Create`. */
const OWN_CREATE = { permission: 'grievance:Create' } as const;

/**
 * `/grievances` opens the first Grievances screen the user's role may see, in sidebar order (the
 * overview for everyone with `grievance:View`). Without any, the overview, whose guard then shows the
 * no-access page.
 */
export const grievanceLanding: RedirectFunction = () => {
  const permissions = inject(PermissionService);
  const group = NAVIGATION.filter((item) => item.label === 'Grievances');
  return firstVisibleRoute(group, (key) => permissions.can(key)) ?? '/grievances/overview';
};

/**
 * Employee grievances, under `/grievances`. Every route carries its own permission (UX only; the
 * server enforces the same keys). The case-team screens (cases, investigations, reviews, categories,
 * reports) join in later slices with their own `grievance-*` keys.
 */
export const GRIEVANCE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: grievanceLanding },
  {
    path: 'overview',
    title: 'Grievances',
    data: { breadcrumb: 'Overview', ...OWN_VIEW },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./overview/grievance-overview.component').then((m) => m.GrievanceOverviewComponent),
  },
  {
    path: 'my',
    data: { breadcrumb: 'My Grievances', ...OWN_VIEW },
    canActivate: [permissionGuard],
    children: [
      {
        path: '',
        title: 'My Grievances',
        loadComponent: () =>
          import('./my/my-grievances.component').then((m) => m.MyGrievancesComponent),
      },
      {
        path: 'new',
        title: 'New Grievance',
        data: { breadcrumb: 'New', ...OWN_CREATE },
        canActivate: [permissionGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () =>
          import('./form/grievance-form.component').then((m) => m.GrievanceFormComponent),
      },
      {
        path: ':id',
        title: 'My Grievance',
        data: { breadcrumb: 'Grievance' },
        loadComponent: () =>
          import('./detail/my-grievance-detail.component').then(
            (m) => m.MyGrievanceDetailComponent,
          ),
      },
      {
        path: ':id/edit',
        title: 'Edit Grievance Draft',
        data: { breadcrumb: 'Edit draft', ...OWN_CREATE },
        canActivate: [permissionGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () =>
          import('./form/grievance-form.component').then((m) => m.GrievanceFormComponent),
      },
    ],
  },
  ...caseTeamRoutes('cases', 'Case Management', 'Case', 'management', 'grievance-management:View', [
    {
      // HR records a grievance an employee raised outside the system (before `:id`).
      path: 'new',
      title: 'Record Grievance',
      data: { breadcrumb: 'Record', permission: 'grievance-management:Create' },
      canActivate: [permissionGuard],
      canDeactivate: [unsavedChangesGuard],
      loadComponent: () =>
        import('./cases/grievance-intake.component').then((m) => m.GrievanceIntakeComponent),
    },
  ]),
  ...caseTeamRoutes(
    'investigations',
    'My Investigations',
    'Investigation',
    'investigation',
    'grievance-investigation:View',
  ),
  ...caseTeamRoutes('reviews', 'Grievance Reviews', 'Review', 'review', 'grievance-review:View'),
  {
    path: 'dashboard',
    title: 'Grievance Dashboard',
    data: { breadcrumb: 'Dashboard', permission: 'grievance-management:View' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./insights/grievance-dashboard.component').then((m) => m.GrievanceDashboardComponent),
  },
  {
    path: 'reports',
    title: 'Grievance Reports',
    data: { breadcrumb: 'Reports', permission: 'grievance-management:View' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./insights/grievance-reports.component').then((m) => m.GrievanceReportsComponent),
  },
  {
    // The module-wide access log: management View, and the screen itself asks for the HR
    // Administrator role (the API's AdministratorPolicy). Linked from Reports for administrators.
    path: 'access-log',
    title: 'Grievance Access Log',
    data: { breadcrumb: 'Access log', permission: 'grievance-management:View' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./insights/grievance-access-log.component').then(
        (m) => m.GrievanceAccessLogComponent,
      ),
  },
  {
    path: 'review-summary',
    title: 'Grievance Review Summary',
    data: { breadcrumb: 'Review summary', permission: 'grievance-review:View' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./insights/grievance-review-summary.component').then(
        (m) => m.GrievanceReviewSummaryComponent,
      ),
  },
  {
    path: 'categories',
    title: 'Grievance Categories',
    data: { breadcrumb: 'Categories', permission: 'grievance-management:View' },
    canActivate: [permissionGuard],
    loadComponent: () =>
      import('./categories/grievance-categories-page.component').then(
        (m) => m.GrievanceCategoriesPageComponent,
      ),
  },
];

/**
 * A case-team list and its case detail. One list and one detail component serve the three tiers;
 * the route's `workspace` (bound as an input) chooses the service call, the texts and the commands.
 * `extraChildren` adds screens to one workspace only (Case management's "Record grievance").
 */
function caseTeamRoutes(
  path: string,
  title: string,
  detailTitle: string,
  workspace: 'management' | 'investigation' | 'review',
  permission: string,
  /** Extra child routes of this workspace only, matched before the case detail (`:id`). */
  extraChildren: Routes = [],
): Routes {
  return [
    {
      path,
      data: { breadcrumb: title, permission, workspace },
      canActivate: [permissionGuard],
      children: [
        {
          path: '',
          title,
          loadComponent: () =>
            import('./cases/grievance-case-list.component').then(
              (m) => m.GrievanceCaseListComponent,
            ),
        },
        ...extraChildren,
        {
          path: ':id',
          title: detailTitle,
          data: { breadcrumb: detailTitle },
          loadComponent: () =>
            import('./cases/grievance-case-detail.component').then(
              (m) => m.GrievanceCaseDetailComponent,
            ),
        },
      ],
    },
  ];
}
