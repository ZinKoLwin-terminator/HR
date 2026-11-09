import { matchesEqualityFilters, matchesSearch, paginate, sortItems } from './query.util';

interface Row {
  id: number;
  name: string;
  dept: number;
  status: string;
}

const rows: Row[] = [
  { id: 1, name: 'Charlie', dept: 1, status: 'Active' },
  { id: 2, name: 'alice', dept: 2, status: 'Inactive' },
  { id: 3, name: 'Bob', dept: 1, status: 'Active' },
];

describe('query.util', () => {
  it('matches search case-insensitively across fields', () => {
    expect(rows.filter((r) => matchesSearch(r, 'ALI', ['name'])).map((r) => r.id)).toEqual([2]);
    expect(rows.filter((r) => matchesSearch(r, '  ', ['name']))).toHaveLength(3);
  });

  it('applies equality filters and ignores empty values', () => {
    expect(
      rows.filter((r) => matchesEqualityFilters(r, { dept: 1, status: null })).map((r) => r.id),
    ).toEqual([1, 3]);
    expect(
      rows.filter((r) => matchesEqualityFilters(r, { status: ['Inactive'] })).map((r) => r.id),
    ).toEqual([2]);
  });

  it('sorts ascending and descending without mutating input', () => {
    expect(sortItems(rows, { active: 'name', direction: 'asc' }).map((r) => r.name)).toEqual([
      'alice',
      'Bob',
      'Charlie',
    ]);
    expect(sortItems(rows, { active: 'id', direction: 'desc' }).map((r) => r.id)).toEqual([
      3, 2, 1,
    ]);
    expect(rows[0].id).toBe(1);
  });

  it('paginates and clamps out-of-range pages', () => {
    const page = paginate(rows, 1, 2);
    expect(page).toMatchObject({ total: 3, pageIndex: 1, pageSize: 2 });
    expect(page.items.map((r) => r.id)).toEqual([3]);
    expect(paginate(rows, 9, 2).pageIndex).toBe(1);
  });
});
