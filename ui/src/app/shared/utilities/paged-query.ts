import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';

/** The largest page the API serves. */
export const MAX_PAGE_SIZE = 200;

export interface AllPages<T> {
  items: T[];
  /** Rows that match, which can be more than `items` when the cap was reached. */
  total: number;
  truncated: boolean;
}

/**
 * Reads every page of a query (search, filters and sort stay as given), stopping at `cap` rows.
 * Works with any `query` of the data contracts, mock or API.
 */
export function readAllPages<T, F extends object>(
  query: (request: PageRequest<F>) => Observable<PagedResult<T>>,
  request: Omit<PageRequest<F>, 'pageIndex' | 'pageSize'>,
  cap: number,
): Observable<AllPages<T>> {
  const pageSize = Math.min(MAX_PAGE_SIZE, cap);
  const read = (pageIndex: number) => query({ ...request, pageIndex, pageSize });
  return read(0).pipe(
    expand((page, index) =>
      (index + 1) * pageSize < Math.min(page.total, cap) ? read(index + 1) : EMPTY,
    ),
    reduce((all, page) => ({ items: [...all.items, ...page.items], total: page.total }), {
      items: [] as T[],
      total: 0,
    }),
    map((all) => ({
      items: all.items.slice(0, cap),
      total: all.total,
      truncated: all.total > cap,
    })),
  );
}
