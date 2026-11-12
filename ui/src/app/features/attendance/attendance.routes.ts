import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';

export const ATTENDANCE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  {
    path: 'overview',
    title: 'Attendance Dashboard',
    data: { breadcrumb: 'Dashboard' },
    loadComponent: () =>
      import('./attendance-overview/attendance-overview.component').then(
        (m) => m.AttendanceOverviewComponent,
      ),
  },
  {
    path: 'check-in',
    title: 'Check In / Out',
    data: { breadcrumb: 'Check In / Out', permission: 'attendance:Create' },
    canActivate: [permissionGuard],
    loadComponent: () => import('./check-in/check-in.component').then((m) => m.CheckInComponent),
  },
  {
    path: 'records',
    title: 'Attendance Records',
    data: { breadcrumb: 'Attendance Records' },
    loadComponent: () =>
      import('./attendance-list/attendance-pages.component').then(
        (m) => m.AttendanceRecordsPageComponent,
      ),
  },
  {
    path: 'exceptions',
    title: 'Late & Early Departures',
    data: { breadcrumb: 'Late & Early' },
    loadComponent: () =>
      import('./attendance-list/attendance-pages.component').then(
        (m) => m.AttendanceExceptionsPageComponent,
      ),
  },
  {
    path: 'shifts',
    title: 'Shift Management',
    data: { breadcrumb: 'Shift Management' },
    loadComponent: () =>
      import('./shifts/shifts-page.component').then((m) => m.ShiftsPageComponent),
  },
  {
    path: 'shift-assignment',
    title: 'Shift Assignment',
    data: { breadcrumb: 'Shift Assignment' },
    loadComponent: () =>
      import('./shift-assignment/shift-assignment.component').then(
        (m) => m.ShiftAssignmentComponent,
      ),
  },
  {
    path: 'schedules',
    title: 'Work Schedules',
    data: { breadcrumb: 'Work Schedules' },
    loadComponent: () =>
      import('./work-schedules/work-schedules.component').then((m) => m.WorkSchedulesComponent),
  },
  {
    path: 'overtime',
    title: 'Overtime',
    data: { breadcrumb: 'Overtime' },
    loadComponent: () =>
      import('./overtime/overtime-page.component').then((m) => m.OvertimePageComponent),
  },
  {
    path: 'adjustments',
    title: 'Attendance Adjustments',
    data: { breadcrumb: 'Adjustments' },
    loadComponent: () =>
      import('./adjustments/adjustments-page.component').then((m) => m.AdjustmentsPageComponent),
  },
];
