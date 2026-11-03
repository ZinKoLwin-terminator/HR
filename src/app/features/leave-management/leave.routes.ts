import { Routes } from '@angular/router';

export const LEAVE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'applications' },
  {
    path: 'types',
    title: 'Leave Types',
    data: { breadcrumb: 'Leave Types' },
    loadComponent: () =>
      import('./leave-types/leave-types-page.component').then((m) => m.LeaveTypesPageComponent),
  },
  {
    path: 'policies',
    title: 'Leave Policies',
    data: { breadcrumb: 'Leave Policies' },
    loadComponent: () =>
      import('./leave-policies/leave-policies-page.component').then(
        (m) => m.LeavePoliciesPageComponent,
      ),
  },
  {
    path: 'balances',
    title: 'Leave Balances',
    data: { breadcrumb: 'Leave Balances' },
    loadComponent: () =>
      import('./leave-balances/leave-balances.component').then((m) => m.LeaveBalancesComponent),
  },
  {
    path: 'applications',
    title: 'Leave Applications',
    data: { breadcrumb: 'Leave Applications' },
    loadComponent: () =>
      import('./leave-applications/leave-pages.component').then(
        (m) => m.LeaveApplicationsPageComponent,
      ),
  },
  {
    path: 'approvals',
    title: 'Leave Approvals',
    data: { breadcrumb: 'Leave Approvals' },
    loadComponent: () =>
      import('./leave-applications/leave-pages.component').then(
        (m) => m.LeaveApprovalsPageComponent,
      ),
  },
  {
    path: 'calendar',
    title: 'Leave Calendar',
    data: { breadcrumb: 'Leave Calendar' },
    loadComponent: () =>
      import('./leave-calendar/leave-calendar.component').then((m) => m.LeaveCalendarComponent),
  },
];
