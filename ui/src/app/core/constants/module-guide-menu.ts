/** One module in the header's help menu: the light part of a `GuideModule`. */
export interface GuideMenuEntry {
  /** `/help/modules?module=<id>` opens the module's guide. */
  readonly id: string;
  readonly name: string;
  readonly icon: string;
}

/**
 * The modules of the help menu, in guide order. Kept apart from `MODULE_GUIDE` (`module-guide.ts`)
 * so eagerly loaded code such as the header does not pull every module's long bilingual article
 * into the initial bundle; only the lazy help feature imports the full guide. A spec checks that
 * both lists name the same modules.
 */
export const MODULE_GUIDE_MENU: readonly GuideMenuEntry[] = [
  { id: 'dashboard', name: 'Dashboard', icon: 'space_dashboard' },
  { id: 'organization', name: 'Organization', icon: 'corporate_fare' },
  { id: 'employees', name: 'Employees', icon: 'groups' },
  { id: 'attendance', name: 'Attendance', icon: 'schedule' },
  { id: 'leave', name: 'Leave Management', icon: 'beach_access' },
  { id: 'performance', name: 'Performance (KPI)', icon: 'trending_up' },
  { id: 'payroll', name: 'Payroll', icon: 'payments' },
  { id: 'recruitment', name: 'Recruitment', icon: 'person_search' },
  { id: 'data-exchange', name: 'Data Exchange', icon: 'swap_vert' },
  { id: 'settings', name: 'Settings', icon: 'settings' },
];
