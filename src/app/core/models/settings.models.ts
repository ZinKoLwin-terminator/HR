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
