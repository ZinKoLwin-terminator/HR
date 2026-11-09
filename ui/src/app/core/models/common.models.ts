/** Every persisted record has a numeric identity. */
export interface Entity {
  id: number;
}

export type RecordStatus = 'Active' | 'Inactive';

export type SortDirection = 'asc' | 'desc' | '';

export interface SortState {
  active: string;
  direction: SortDirection;
}

/**
 * Standard list query. The same shape will be sent to the future API
 * (server-side paging / sorting / filtering) so UI code does not change.
 */
export interface PageRequest<TFilter extends object = Record<string, unknown>> {
  pageIndex: number;
  pageSize: number;
  sort?: SortState;
  search?: string;
  filters?: Partial<TFilter>;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export interface LookupOption<TValue = number> {
  value: TValue;
  label: string;
  disabled?: boolean;
}

/** Inclusive ISO date range (yyyy-MM-dd). */
export interface DateRange {
  from?: string | null;
  to?: string | null;
}

/** Uniform error shape surfaced by all data services (mock now, API later). */
export interface AppError {
  code: 'NotFound' | 'Validation' | 'Conflict' | 'Unknown';
  message: string;
  fieldErrors?: Record<string, string>;
}

export function isAppError(value: unknown): value is AppError {
  return (
    typeof value === 'object' &&
    value !== null &&
    'code' in value &&
    'message' in value &&
    typeof (value as AppError).message === 'string'
  );
}

export type CreateModel<T extends Entity> = Omit<T, 'id'>;
export type UpdateModel<T extends Entity> = Partial<Omit<T, 'id'>>;
