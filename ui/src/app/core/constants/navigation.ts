export interface NavItem {
  label: string;
  icon: string;
  /** Absolute route. Groups omit it. */
  route?: string;
  children?: NavItem[];
  /** Section name for grouping in the sidebar. */
  section?: string;
  /** Sub-heading shown above a run of consecutive children that share it. */
  group?: string;
  /**
   * Server permission key (`module:Action`) needed to see the entry. On a group it is the default
   * for its children; a child may name its own. Entries without one are always shown. UX only: the
   * server enforces the same keys.
   */
  permission?: string;
}

/** The entries (and groups with at least one entry) the user may see. */
export function visibleNavigation(
  items: NavItem[],
  can: (permission: string) => boolean,
): NavItem[] {
  return items.flatMap((item) => {
    if (!item.children) {
      return !item.permission || can(item.permission) ? [item] : [];
    }
    const children = item.children.filter((child) => {
      const key = child.permission ?? item.permission;
      return !key || can(key);
    });
    return children.length > 0 ? [{ ...item, children }] : [];
  });
}

/** Single source of truth for the sidebar. Routes must exist in the route config. */
export const NAVIGATION: NavItem[] = [
  {
    label: 'Dashboard',
    icon: 'space_dashboard',
    route: '/dashboard',
    section: 'MAIN',
    permission: 'dashboard:View',
  },
  {
    label: 'Organization',
    permission: 'organization:View',
    icon: 'corporate_fare',
    section: 'OPERATIONS',
    children: [
      { label: 'Companies', icon: 'domain', route: '/organization/companies', group: 'Setup' },
      { label: 'Branches', icon: 'store', route: '/organization/branches', group: 'Setup' },
      { label: 'Departments', icon: 'lan', route: '/organization/departments', group: 'Setup' },
      { label: 'Sections', icon: 'workspaces', route: '/organization/sections', group: 'Setup' },
      { label: 'Positions', icon: 'work', route: '/organization/positions', group: 'Setup' },
      { label: 'Job Grades', icon: 'stairs', route: '/organization/job-grades', group: 'Setup' },
      {
        label: 'Reporting Structure',
        icon: 'account_tree',
        route: '/organization/reporting-structure',
        group: 'Report',
      },
    ],
  },
  {
    label: 'Employees',
    permission: 'employees:View',
    icon: 'groups',
    section: 'PEOPLE',
    children: [
      {
        label: 'Add Employee',
        icon: 'person_add',
        route: '/employees/new',
        group: 'Operations',
        permission: 'employees:Create',
      },
      {
        label: 'Employee Documents',
        icon: 'folder_shared',
        route: '/employees/documents',
        permission: 'employees:ViewSensitive',
        group: 'Operations',
      },
      { label: 'Employee List', icon: 'badge', route: '/employees/list', group: 'Report' },
    ],
  },
  {
    label: 'Attendance',
    permission: 'attendance:View',
    icon: 'schedule',
    section: 'PEOPLE',
    children: [
      {
        label: 'Shift Management',
        icon: 'view_timeline',
        route: '/attendance/shifts',
        group: 'Setup',
      },
      {
        label: 'Holiday Calendar',
        icon: 'event',
        route: '/attendance/holidays',
        group: 'Setup',
      },
      {
        label: 'Shift Assignment',
        icon: 'assignment_ind',
        route: '/attendance/shift-assignment',
        group: 'Setup',
      },
      {
        label: 'Work Schedules',
        icon: 'calendar_view_week',
        route: '/attendance/schedules',
        group: 'Setup',
      },
      {
        label: 'Check In / Out',
        icon: 'touch_app',
        route: '/attendance/check-in',
        permission: 'attendance:Create',
        group: 'Operations',
      },
      { label: 'Overtime', icon: 'more_time', route: '/attendance/overtime', group: 'Operations' },
      {
        label: 'Adjustments',
        icon: 'edit_calendar',
        route: '/attendance/adjustments',
        group: 'Operations',
      },
      {
        label: 'Dashboard',
        icon: 'monitoring',
        route: '/attendance/overview',
        group: 'Report',
      },
      {
        label: 'Attendance Records',
        icon: 'fact_check',
        route: '/attendance/records',
        group: 'Report',
      },
      {
        label: 'Late & Early',
        icon: 'running_with_errors',
        route: '/attendance/exceptions',
        group: 'Report',
      },
    ],
  },
  {
    label: 'Leave Management',
    permission: 'leave:View',
    icon: 'beach_access',
    section: 'PEOPLE',
    children: [
      { label: 'Leave Types', icon: 'category', route: '/leave/types', group: 'Setup' },
      { label: 'Leave Policies', icon: 'policy', route: '/leave/policies', group: 'Setup' },
      {
        label: 'Leave Applications',
        icon: 'note_add',
        route: '/leave/applications',
        group: 'Operations',
      },
      {
        label: 'Leave Approvals',
        icon: 'task_alt',
        route: '/leave/approvals',
        permission: 'leave:Approve',
        group: 'Operations',
      },
      {
        label: 'Leave Balances',
        icon: 'account_balance_wallet',
        route: '/leave/balances',
        group: 'Report',
      },
      {
        label: 'Leave Calendar',
        icon: 'calendar_month',
        route: '/leave/calendar',
        group: 'Report',
      },
    ],
  },
  {
    label: 'Payroll',
    permission: 'payroll:View',
    icon: 'payments',
    section: 'OPERATIONS',
    children: [
      {
        label: 'Salary Components',
        icon: 'list_alt',
        route: '/payroll/salary-components',
        group: 'Setup',
      },
      {
        label: 'Salary Structures',
        icon: 'account_balance',
        route: '/payroll/salary-structures',
        group: 'Setup',
      },
      {
        label: 'Employee Salaries',
        icon: 'request_quote',
        route: '/payroll/employee-salaries',
        group: 'Setup',
      },
      {
        label: 'Payroll Processing',
        icon: 'calculate',
        route: '/payroll/processing',
        group: 'Operations',
      },
      { label: 'Payslips', icon: 'receipt_long', route: '/payroll/payslips', group: 'Report' },
      { label: 'Payroll History', icon: 'history', route: '/payroll/history', group: 'Report' },
    ],
  },
  {
    label: 'Recruitment',
    permission: 'recruitment:View',
    icon: 'person_search',
    section: 'OPERATIONS',
    children: [
      { label: 'Job Openings', icon: 'work_outline', route: '/recruitment/job-openings' },
      { label: 'Candidates', icon: 'contacts', route: '/recruitment/candidates' },
      { label: 'Interviews', icon: 'event_available', route: '/recruitment/interviews' },
      { label: 'Pipeline', icon: 'view_kanban', route: '/recruitment/pipeline' },
    ],
  },
  {
    label: 'Data Exchange',
    permission: 'excel:View',
    icon: 'swap_vert',
    section: 'SYSTEM',
    children: [
      {
        label: 'Import Data',
        icon: 'upload_file',
        route: '/data-exchange/import',
        permission: 'excel:Import',
      },
      { label: 'Import History', icon: 'history', route: '/data-exchange/history' },
    ],
  },
  {
    label: 'Settings',
    permission: 'settings:View',
    icon: 'settings',
    section: 'SYSTEM',
    children: [
      { label: 'General Settings', icon: 'tune', route: '/settings/general' },
      { label: 'Users', icon: 'manage_accounts', route: '/settings/users' },
      { label: 'Roles', icon: 'admin_panel_settings', route: '/settings/roles' },
      { label: 'Permissions', icon: 'key', route: '/settings/permissions' },
    ],
  },
];
