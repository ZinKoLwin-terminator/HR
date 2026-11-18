import {
  GrievanceCategoryCount,
  GrievanceDepartmentCount,
  GrievancePriorityCount,
  GrievanceStatusCount,
} from '../../../core/models/grievance-insight.models';
import { GRIEVANCE_STATUS_LABELS } from '../../../core/models/grievance.models';
import { GrievanceBar } from './grievance-bar-chart.component';

/** The server's counts as chart bars, in the server's order (labels only, no figure is computed). */
export const categoryBars = (rows: readonly GrievanceCategoryCount[]): GrievanceBar[] =>
  rows.map((r) => ({ label: r.categoryName, value: r.count }));

/** Departments, then "Not disclosed" (anonymous) and "Other / suppressed" (groups under 3). */
export const departmentBars = (rows: readonly GrievanceDepartmentCount[]): GrievanceBar[] =>
  rows.map((r) => ({ label: r.departmentName, value: r.count }));

export const priorityBars = (rows: readonly GrievancePriorityCount[]): GrievanceBar[] =>
  rows.map((r) => ({ label: r.priority ?? 'Not set', value: r.count }));

export const statusBars = (rows: readonly GrievanceStatusCount[]): GrievanceBar[] =>
  rows.map((r) => ({ label: GRIEVANCE_STATUS_LABELS[r.status], value: r.count }));

/** "33.33%" from a percentage figure. */
export const percentText = (value: number): string => `${value.toFixed(2)}%`;

/** "16.8 days", or "—" when nothing has been resolved. */
export const daysText = (value: number | null): string =>
  value === null ? '—' : `${value.toFixed(1)} days`;
