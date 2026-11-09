import { computed, signal } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { PageRequest, SortState } from '../../core/models/common.models';

export interface ListQueryOptions<TFilter extends object> {
  pageSize: number;
  sort?: SortState;
  filters?: Partial<TFilter>;
}

/**
 * Signal-based list state (paging, sorting, search, filters) producing a
 * {@link PageRequest}. Pair with `rxResource` to (re)load data reactively:
 *
 *   readonly query = new ListQuery<EmployeeFilter>({ pageSize: 10 });
 *   readonly page = rxResource({ params: () => this.query.request(), stream: ({ params }) => svc.query(params) });
 */
export class ListQuery<TFilter extends object> {
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly sort = signal<SortState>({ active: '', direction: '' });
  readonly search = signal('');
  readonly filters = signal<Partial<TFilter>>({});

  readonly request = computed<PageRequest<TFilter>>(() => ({
    pageIndex: this.pageIndex(),
    pageSize: this.pageSize(),
    sort: this.sort(),
    search: this.search(),
    filters: this.filters(),
  }));

  constructor(private readonly initial: ListQueryOptions<TFilter>) {
    this.pageSize.set(initial.pageSize);
    this.reset();
  }

  setSearch(term: string): void {
    this.search.set(term);
    this.pageIndex.set(0);
  }

  setFilter<K extends keyof TFilter>(key: K, value: TFilter[K] | null): void {
    this.filters.update((current) => ({ ...current, [key]: value }));
    this.pageIndex.set(0);
  }

  setSort(sort: SortState): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
  }

  onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  /** Number of non-empty filters (for "Clear filters" affordances). */
  readonly activeFilterCount = computed(
    () =>
      Object.values(this.filters()).filter((v) => v !== null && v !== undefined && v !== '')
        .length + (this.search() ? 1 : 0),
  );

  reset(): void {
    this.search.set('');
    this.filters.set({ ...(this.initial.filters ?? {}) });
    this.sort.set(this.initial.sort ?? { active: '', direction: '' });
    this.pageIndex.set(0);
  }
}
