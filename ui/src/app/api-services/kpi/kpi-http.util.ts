import { HttpParams } from '@angular/common/http';
import { PageRequest } from '../../core/models/common.models';
import { MAX_SEARCH_LENGTH } from '../http-crud.service';

/**
 * Query string of a paged KPI list: paging, `sort=field:direction` (only for keys the API sorts by),
 * the search text (cut to the length the API accepts) and the filters it knows. Empty filters and
 * "all" are not sent.
 */
export function buildPageParams(
  request: PageRequest<object>,
  sortKeys: readonly string[],
  filterKeys: readonly string[],
): HttpParams {
  let params = new HttpParams()
    .set('pageIndex', request.pageIndex)
    .set('pageSize', request.pageSize);
  if (request.sort?.active && request.sort.direction && sortKeys.includes(request.sort.active)) {
    params = params.set('sort', `${request.sort.active}:${request.sort.direction}`);
  }
  const search = request.search?.trim().slice(0, MAX_SEARCH_LENGTH);
  if (search) {
    params = params.set('search', search);
  }
  const filters = (request.filters ?? {}) as Record<string, unknown>;
  for (const key of filterKeys) {
    const value = filters[key];
    if (value === null || value === undefined) {
      continue;
    }
    const text = String(value).trim();
    if (text !== '' && text.toLowerCase() !== 'all') {
      params = params.set(key, text);
    }
  }
  return params;
}

/**
 * The file name from a `Content-Disposition` header: the RFC 5987 `filename*=UTF-8''...` form when
 * present, otherwise `filename="..."`. Null when the header has neither.
 */
export function fileNameFromDisposition(header: string | null): string | null {
  if (!header) {
    return null;
  }
  const encoded = /filename\*\s*=\s*(?:UTF-8|utf-8)''([^;]+)/.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // fall through to the plain form
    }
  }
  const quoted = /filename\s*=\s*"((?:[^"\\]|\\.)*)"/.exec(header);
  if (quoted) {
    return quoted[1].replace(/\\(.)/g, '$1');
  }
  const bare = /filename\s*=\s*([^;]+)/.exec(header);
  return bare ? bare[1].trim() : null;
}
