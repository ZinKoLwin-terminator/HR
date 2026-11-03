export interface NavItem {
  label: string;
  icon: string;
  /** Absolute route. Groups omit it. */
  route?: string;
  children?: NavItem[];
}

/** Single source of truth for the sidebar. Routes must exist in the route config. */
export const NAVIGATION: NavItem[] = [
  { label: 'Dashboard', icon: 'space_dashboard', route: '/dashboard' },
  {
    label: 'Organization',
    icon: 'corporate_fare',
    children: [
      { label: 'Companies', icon: 'domain', route: '/organization/companies' },
      { label: 'Branches', icon: 'store', route: '/organization/branches' },
      { label: 'Departments', icon: 'lan', route: '/organization/departments' },
      { label: 'Sections', icon: 'workspaces', route: '/organization/sections' },
      { label: 'Positions', icon: 'work', route: '/organization/positions' },
      { label: 'Job Grades', icon: 'stairs', route: '/organization/job-grades' },
      {
        label: 'Reporting Structure',
        icon: 'account_tree',
        route: '/organization/reporting-structure',
      },
    ],
  },
  {
    label: 'Employees',
    icon: 'groups',
    children: [
      { label: 'Employee List', icon: 'badge', route: '/employees/list' },
      { label: 'Add Employee', icon: 'person_add', route: '/employees/new' },
      { label: 'Employee Documents', icon: 'folder_shared', route: '/employees/documents' },
    ],
  },
  {
    label: 'Attendance',
    icon: 'schedule',
    children: [
      { label: 'Dashboard', icon: 'monitoring', route: '/attendance/overview' },
      { label: 'Check In / Out', icon: 'touch_app', route: '/attendance/check-in' },
      { label: 'Attendance Records', icon: 'fact_check', route: '/attendance/records' },
      { label: 'Late & Early', icon: 'running_with_errors', route: '/attendance/exceptions' },
      { label: 'Shift Management', icon: 'view_timeline', route: '/attendance/shifts' },
      { label: 'Shift Assignment', icon: 'assignment_ind', route: '/attendance/shift-assignment' },
      { label: 'Work Schedules', icon: 'calendar_view_week', route: '/attendance/schedules' },
      { label: 'Overtime', icon: 'more_time', route: '/attendance/overtime' },
      { label: 'Adjustments', icon: 'edit_calendar', route: '/attendance/adjustments' },
    ],
  },
  {
    label: 'Leave Management',
    icon: 'beach_access',
    children: [
      { label: 'Leave Types', icon: 'category', route: '/leave/types' },
      { label: 'Leave Policies', icon: 'policy', route: '/leave/policies' },
      { label: 'Leave Balances', icon: 'account_balance_wallet', route: '/leave/balances' },
      { label: 'Leave Applications', icon: 'note_add', route: '/leave/applications' },
      { label: 'Leave Approvals', icon: 'task_alt', route: '/leave/approvals' },
      { label: 'Leave Calendar', icon: 'calendar_month', route: '/leave/calendar' },
    ],
  },
  {
    label: 'Payroll',
    icon: 'payments',
    children: [
      { label: 'Salary Structures', icon: 'account_balance', route: '/payroll/salary-structures' },
      { label: 'Salary Components', icon: 'list_alt', route: '/payroll/salary-components' },
      { label: 'Employee Salaries', icon: 'request_quote', route: '/payroll/employee-salaries' },
      { label: 'Payroll Processing', icon: 'calculate', route: '/payroll/processing' },
      { label: 'Payslips', icon: 'receipt_long', route: '/payroll/payslips' },
      { label: 'Payroll History', icon: 'history', route: '/payroll/history' },
    ],
  },
  {
    label: 'Recruitment',
    icon: 'person_search',
    children: [
      { label: 'Job Openings', icon: 'work_outline', route: '/recruitment/job-openings' },
      { label: 'Candidates', icon: 'contacts', route: '/recruitment/candidates' },
      { label: 'Interviews', icon: 'event_available', route: '/recruitment/interviews' },
      { label: 'Pipeline', icon: 'view_kanban', route: '/recruitment/pipeline' },
    ],
  },
  {
    label: 'Data Exchange',
    icon: 'swap_vert',
    children: [
      { label: 'Import Data', icon: 'upload_file', route: '/data-exchange/import' },
      { label: 'Import History', icon: 'history', route: '/data-exchange/history' },
    ],
  },
  {
    label: 'Settings',
    icon: 'settings',
    children: [
      { label: 'General Settings', icon: 'tune', route: '/settings/general' },
      { label: 'Users', icon: 'manage_accounts', route: '/settings/users' },
      { label: 'Roles', icon: 'admin_panel_settings', route: '/settings/roles' },
      { label: 'Permissions', icon: 'key', route: '/settings/permissions' },
    ],
  },
];
