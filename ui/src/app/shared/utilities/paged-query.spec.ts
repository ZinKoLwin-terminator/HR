import { firstValueFrom, of } from 'rxjs';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { readAllPages } from './paged-query';

function source(total: number) {
  const calls: PageRequest<{ q: string }>[] = [];
  const query = (request: PageRequest<{ q: string }>) => {
    calls.push(request);
    const start = request.pageIndex * request.pageSize;
    const items = Array.from(
      { length: Math.max(0, Math.min(request.pageSize, total - start)) },
      (_, i) => start + i,
    );
    const result: PagedResult<number> = {
      items,
      total,
      pageIndex: request.pageIndex,
      pageSize: request.pageSize,
    };
    return of(result);
  };
  return { query, calls };
}

describe('readAllPages', () => {
  it('reads every page and keeps the search and filters', async () => {
    const { query, calls } = source(450);
    const all = await firstValueFrom(
      readAllPages(query, { search: 'x', filters: { q: 'a' } }, 1000),
    );
    expect(all.items).toHaveLength(450);
    expect(all.truncated).toBe(false);
    expect(calls).toHaveLength(3);
    expect(calls.every((c) => c.search === 'x' && c.filters?.q === 'a')).toBe(true);
  });

  it('stops at the cap and says so', async () => {
    const { query } = source(1500);
    const all = await firstValueFrom(readAllPages(query, {}, 1000));
    expect(all.items).toHaveLength(1000);
    expect(all.total).toBe(1500);
    expect(all.truncated).toBe(true);
  });

  it('handles an empty result', async () => {
    const { query } = source(0);
    const all = await firstValueFrom(readAllPages(query, {}, 1000));
    expect(all).toEqual({ items: [], total: 0, truncated: false });
  });
});
