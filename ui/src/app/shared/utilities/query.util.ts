import { PagedResult, SortState } from '../../core/models/common.models';

/** Case-insensitive "contains" match of a search term across the given fields. */
export function matchesSearch<T>(
  item: T,
  term: string | undefined,
  fields: readonly (keyof T)[],
): boolean {
  const needle = term?.trim().toLowerCase();
  if (!needle) {
    return true;
  }
  return fields.some((field) => {
    const value = item[field];
    return value !== null && value !== undefined && String(value).toLowerCase().includes(needle);
  });
}

/** True when a filter value is "not set" and should be ignored. */
export function isEmptyFilterValue(value: unknown): boolean {
  return (
    value === null ||
    value === undefined ||
    value === '' ||
    value === 'all' ||
    (Array.isArray(value) && value.length === 0)
  );
}

/**
 * Equality filter: every non-empty filter key must equal the item's field
 * with the same name (arrays mean "one of").
 */
export function matchesEqualityFilters<T extends object>(
  item: T,
  filters: object | undefined,
): boolean {
  if (!filters) {
    return true;
  }
  return Object.entries(filters).every(([key, expected]) => {
    if (isEmptyFilterValue(expected)) {
      return true;
    }
    const actual = (item as Record<string, unknown>)[key];
    return Array.isArray(expected) ? expected.includes(actual) : actual === expected;
  });
}

export function compareValues(a: unknown, b: unknown): number {
  if (a === b) {
    return 0;
  }
  if (a === null || a === undefined) {
    return 1;
  }
  if (b === null || b === undefined) {
    return -1;
  }
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b;
  }
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' });
}

export function sortItems<T>(items: T[], sort: SortState | undefined): T[] {
  if (!sort?.active || !sort.direction) {
    return items;
  }
  const factor = sort.direction === 'asc' ? 1 : -1;
  const key = sort.active;
  return [...items].sort(
    (a, b) =>
      factor *
      compareValues((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key]),
  );
}

export function paginate<T>(items: T[], pageIndex: number, pageSize: number): PagedResult<T> {
  const size = Math.max(1, pageSize);
  const lastPage = Math.max(0, Math.ceil(items.length / size) - 1);
  const index = Math.min(Math.max(0, pageIndex), lastPage);
  return {
    items: items.slice(index * size, index * size + size),
    total: items.length,
    pageIndex: index,
    pageSize: size,
  };
}
