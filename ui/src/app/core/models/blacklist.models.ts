import { Entity } from './common.models';

/**
 * One entry of the do-not-rehire list. `liftedOn === null` means the entry is in force.
 * `reason` is sensitive: only screens guarded by `employees:ViewBlacklist` may show it.
 */
export interface BlacklistEntry extends Entity {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  reason: string;
  blacklistedOn: string;
  /** Display name kept at the time. */
  blacklistedBy: string;
  liftedOn: string | null;
  liftedBy: string | null;
  liftReason: string | null;
  rowVersion?: string;
}

/** `active`: "true" = in force, "false" = lifted, omitted = both. */
export interface BlacklistFilter {
  active: 'true' | 'false';
}

export const BLACKLIST_VIEW_PERMISSION = 'employees:ViewBlacklist';
export const BLACKLIST_EDIT_PERMISSION = 'employees:EditBlacklist';
export const BLACKLIST_REASON_MAX_LENGTH = 500;
/** Longest reason for approving the hire of a candidate who matches the list. */
export const BLACKLIST_OVERRIDE_MAX_LENGTH = 300;
export const BLACKLIST_MATCH_LABEL = 'Matches do-not-rehire list — contact HR';

export function isActiveEntry(entry: Pick<BlacklistEntry, 'liftedOn'>): boolean {
  return entry.liftedOn === null;
}
