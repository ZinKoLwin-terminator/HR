/** The three KPI dashboards. */
export type KpiDashboardView = 'employee' | 'manager' | 'hr';

export interface KpiDashboardTab {
  view: KpiDashboardView;
  label: string;
  icon: string;
}

const TABS: readonly KpiDashboardTab[] = [
  { view: 'employee', label: 'My performance', icon: 'person' },
  { view: 'manager', label: 'My team', icon: 'groups' },
  { view: 'hr', label: 'HR overview', icon: 'corporate_fare' },
];

/**
 * The dashboards the signed-in user may open, as tabs (UX only; the server enforces the same keys):
 * their own with `kpi:View`, their team's with `kpi:Approve`, and the HR overview with `kpi:Approve`
 * and `employees:Edit` (the HR tier; anyone else gets a 403 from the server, so the tab is hidden).
 */
export function kpiDashboardTabs(can: (key: string) => boolean): KpiDashboardTab[] {
  const approve = can('kpi:Approve');
  const allowed: Record<KpiDashboardView, boolean> = {
    employee: can('kpi:View'),
    manager: approve,
    hr: approve && can('employees:Edit'),
  };
  return TABS.filter((tab) => allowed[tab.view]);
}

/** "80.88 · Exceeds Expectations", or a dash: the server's score and rating label as text. */
export function scoreText(score: number | null, rating?: string | null): string {
  if (score === null) return '—';
  const value = score.toFixed(2);
  return rating ? `${value} · ${rating}` : value;
}

/** A number with 2 decimals, or a dash. */
export function fixed2(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : value.toFixed(2);
}

/** A value with its unit ("30 tasks"), or a dash. */
export function withUnit(value: number | null | undefined, unit: string | null): string {
  if (value === null || value === undefined) return '—';
  return unit ? `${value} ${unit}` : String(value);
}
