import { Entity } from './common.models';

/** Everything on the Settings screens is a UI simulation: it is stored in memory and enforces nothing. */

export type UserStatus = 'Active' | 'Inactive' | 'Locked';
export const USER_STATUSES: readonly UserStatus[] = ['Active', 'Inactive', 'Locked'];

export interface AppUser extends Entity {
  employeeId: number;
  /** Copied from the linked employee. */
  displayName: string;
  email: string;
  roleId: number;
  status: UserStatus;
  lastLoginOn: string | null;
}

export interface Role extends Entity {
  name: string;
  description: string;
  /** Built-in roles cannot be renamed or deleted. */
  isSystem: boolean;
  status: 'Active' | 'Inactive';
}

export type PermissionAction = 'View' | 'Create' | 'Edit' | 'Delete' | 'Approve';

export interface PermissionModule {
  key: string;
  label: string;
  /** Actions that make sense for the module (View is always first). */
  actions: readonly PermissionAction[];
}

export const PERMISSION_MODULES: readonly PermissionModule[] = [
  { key: 'dashboard', label: 'Dashboard', actions: ['View'] },
  { key: 'organization', label: 'Organization', actions: ['View', 'Create', 'Edit', 'Delete'] },
  { key: 'employees', label: 'Employees', actions: ['View', 'Create', 'Edit', 'Delete'] },
  { key: 'attendance', label: 'Attendance', actions: ['View', 'Create', 'Edit', 'Approve'] },
  { key: 'leave', label: 'Leave', actions: ['View', 'Create', 'Edit', 'Delete', 'Approve'] },
  { key: 'payroll', label: 'Payroll', actions: ['View', 'Create', 'Edit', 'Approve'] },
  { key: 'recruitment', label: 'Recruitment', actions: ['View', 'Create', 'Edit', 'Delete'] },
  { key: 'settings', label: 'Settings', actions: ['View', 'Create', 'Edit', 'Delete'] },
  // KPI master data and assignments (HR). The API module keys are `kpi-setup` and `kpi`.
  { key: 'kpi-setup', label: 'KPI Setup', actions: ['View', 'Create', 'Edit', 'Delete'] },
  // Scorecards: Create enters and submits your own results; Approve reviews them.
  { key: 'kpi', label: 'Performance', actions: ['View', 'Create', 'Approve'] },
  // Grievances. `grievance`: own cases (Create drafts and submits them). The three case-team modules
  // are confidential and granted to few people: investigation (assigned cases), management (every
  // case, categories; Delete archives) and review (escalations and resolution approval).
  { key: 'grievance', label: 'Grievances', actions: ['View', 'Create'] },
  { key: 'grievance-investigation', label: 'Grievance Investigation', actions: ['View', 'Edit'] },
  {
    key: 'grievance-management',
    label: 'Grievance Management',
    actions: ['View', 'Create', 'Edit', 'Delete'],
  },
  { key: 'grievance-review', label: 'Grievance Review', actions: ['View', 'Approve'] },
];

/** The four grievance module keys (confidential: never granted by a blanket "view everything"). */
export const GRIEVANCE_PERMISSION_MODULE_KEYS: readonly string[] = [
  'grievance',
  'grievance-investigation',
  'grievance-management',
  'grievance-review',
];

export const PERMISSION_ACTIONS: readonly PermissionAction[] = [
  'View',
  'Create',
  'Edit',
  'Delete',
  'Approve',
];

/** module key → granted actions */
export type PermissionGrants = Record<string, PermissionAction[]>;

/** The permission set of one role (`id` equals the role id). */
export interface RolePermissions extends Entity {
  roleId: number;
  grants: PermissionGrants;
}

export interface GeneralSettings extends Entity {
  organizationName: string;
  contactEmail: string;
  timezone: string;
  dateFormat: string;
  currencyCode: string;
  /** 1–12 */
  fiscalYearStartMonth: number;
  emailNotifications: boolean;
  weeklyDigest: boolean;
}

export const TIMEZONES: readonly string[] = [
  'Asia/Yangon',
  'Asia/Bangkok',
  'Asia/Singapore',
  'Asia/Kolkata',
  'Europe/London',
  'America/New_York',
  'UTC',
];
export const DATE_FORMATS: readonly string[] = [
  'yyyy-MM-dd',
  'dd/MM/yyyy',
  'MM/dd/yyyy',
  'dd MMM yyyy',
];

/** The built-in role that always keeps full access and at least one active user. */
export const ADMIN_ROLE_NAME = 'HR Administrator';

/**
 * Employee id of an account linked to no employee: the built-in administrator in empty-data mode,
 * or (API, sent as null) an account created after the seed scripts. Such an account can be linked
 * later with Settings > Users > Link to employee. Every real employee (including id 2) can still
 * be given a user account.
 */
export const BUILT_IN_ADMIN_EMPLOYEE_ID = 0;
