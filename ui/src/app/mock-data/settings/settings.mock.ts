import {
  AppUser,
  GeneralSettings,
  PERMISSION_MODULES,
  PermissionAction,
  PermissionGrants,
  Role,
  RolePermissions,
} from '../../core/models/settings.models';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';

/** SAMPLE DATA — a simulated access model. Nothing here is enforced anywhere in the app. */

type RoleSeed = [
  name: string,
  description: string,
  isSystem: boolean,
  status: 'Active' | 'Inactive',
];
const ROLE_SEED: RoleSeed[] = [
  ['HR Administrator', 'Full access to every module, including settings.', true, 'Active'],
  ['HR Officer', 'Day-to-day HR operations without payroll approval or settings.', false, 'Active'],
  ['Department Manager', 'Views the team and approves attendance and leave.', false, 'Active'],
  ['Payroll Officer', 'Prepares and approves payroll.', false, 'Active'],
  ['Recruiter', 'Manages job openings, candidates and interviews.', false, 'Active'],
  ['Employee', 'Self-service basics for every employee.', true, 'Active'],
  ['Auditor', 'Read-only access (not in use yet).', false, 'Inactive'],
];

export const MOCK_ROLES: Role[] = ROLE_SEED.map(([name, description, isSystem, status], i) => ({
  id: i + 1,
  name,
  description,
  isSystem,
  status,
}));

const everything = (): PermissionGrants =>
  Object.fromEntries(PERMISSION_MODULES.map((m) => [m.key, [...m.actions]]));
const viewOnly = (keys: string[]): PermissionGrants =>
  Object.fromEntries(keys.map((k) => [k, ['View' as PermissionAction]]));
const merge = (...sets: PermissionGrants[]): PermissionGrants => {
  const result: PermissionGrants = {};
  for (const set of sets) Object.assign(result, set);
  return result;
};
const all = PERMISSION_MODULES.map((m) => m.key);

const GRANT_SEED: PermissionGrants[] = [
  everything(),
  merge(viewOnly(all), {
    employees: ['View', 'Create', 'Edit'],
    attendance: ['View', 'Create', 'Edit', 'Approve'],
    leave: ['View', 'Create', 'Edit', 'Approve'],
    organization: ['View', 'Create', 'Edit'],
    recruitment: ['View', 'Create', 'Edit'],
    settings: [],
    payroll: ['View'],
  }),
  merge(viewOnly(['dashboard', 'employees', 'organization', 'recruitment']), {
    attendance: ['View', 'Approve'],
    leave: ['View', 'Approve'],
  }),
  merge(viewOnly(['dashboard', 'employees']), { payroll: ['View', 'Create', 'Edit', 'Approve'] }),
  merge(viewOnly(['dashboard']), { recruitment: ['View', 'Create', 'Edit', 'Delete'] }),
  merge(viewOnly(['dashboard', 'attendance']), { leave: ['View', 'Create'] }),
  viewOnly(all),
];

export const MOCK_ROLE_PERMISSIONS: RolePermissions[] = GRANT_SEED.map((grants, i) => ({
  id: i + 1,
  roleId: i + 1,
  grants,
}));

// employeeId, roleId, status, lastLoginDaysAgo
type UserSeed = [number, number, AppUser['status'], number | null];
const USER_SEED: UserSeed[] = [
  [2, 1, 'Active', 0],
  [1, 3, 'Active', 1],
  [3, 3, 'Active', 0],
  [4, 4, 'Active', 2],
  [7, 3, 'Active', 3],
  [9, 2, 'Active', 1],
  [10, 3, 'Active', 5],
  [11, 5, 'Active', 4],
  [12, 6, 'Locked', 21],
  [26, 6, 'Inactive', 90],
  [5, 6, 'Active', 0],
];

export const MOCK_USERS: AppUser[] = USER_SEED.map(([employeeId, roleId, status, daysAgo], i) => {
  const employee = MOCK_EMPLOYEES.find((e) => e.id === employeeId)!;
  return {
    id: i + 1,
    employeeId,
    displayName: `${employee.firstName} ${employee.lastName}`,
    email: employee.workEmail,
    roleId,
    status,
    lastLoginOn: daysAgo === null ? null : addDays(todayIso(), -daysAgo),
  };
});

export const MOCK_GENERAL_SETTINGS: GeneralSettings = {
  id: 1,
  organizationName: 'APEX Group',
  contactEmail: 'hr@apexhorizon.example',
  timezone: 'Asia/Yangon',
  dateFormat: 'yyyy-MM-dd',
  currencyCode: 'MMK',
  fiscalYearStartMonth: 1,
  emailNotifications: true,
  weeklyDigest: false,
};
