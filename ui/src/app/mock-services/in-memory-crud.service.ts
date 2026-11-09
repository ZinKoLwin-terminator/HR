import { Signal, WritableSignal, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../core/configuration/app-config';
import { ActivityCategory } from '../core/models/activity.models';
import {
  AppError,
  CreateModel,
  Entity,
  isAppError,
  PagedResult,
  PageRequest,
  UpdateModel,
} from '../core/models/common.models';
import { CrudDataService } from '../core/services/data/crud-data.service';
import {
  matchesEqualityFilters,
  matchesSearch,
  paginate,
  sortItems,
} from '../shared/utilities/query.util';
import { ActivityMockService } from './activity/activity-mock.service';
import { mockError, mockResponse } from './mock-response';
import { persistedSignal } from './persistence';
import { initialRecords } from './seed-mode';

/**
 * Generic in-memory implementation of {@link CrudDataService}.
 *
 * - State lives in a signal seeded from `src/app/mock-data` (resets on reload).
 * - Every operation is asynchronous (simulated latency) and returns clones.
 * - Subclasses customise behaviour through small hooks:
 *   `toListItem`, `searchFields`, `matchesFilters`, `validate`, `beforeDelete`, `describe`.
 */
export abstract class InMemoryCrudService<
  T extends Entity,
  TList extends Entity = T,
  TFilter extends object = Record<string, unknown>,
> extends CrudDataService<T, TList, TFilter> {
  protected readonly latency = inject(APP_CONFIG).mockLatencyMs;
  protected readonly activity = inject(ActivityMockService);

  protected readonly store: WritableSignal<T[]>;
  /** Live read-only view of all records — for use by OTHER MOCK services only (joins, lookups). */
  readonly records: Signal<T[]>;

  /** Human-readable entity name used in messages, e.g. "Department". */
  protected abstract readonly entityLabel: string;
  protected abstract readonly activityCategory: ActivityCategory;
  /** List-item fields matched by the free-text search box. */
  protected readonly searchFields: readonly (keyof TList)[] = [];

  /**
   * @param seed Sample records the store starts with.
   * @param storageKey Unique name used when the store is saved in the browser (see persistence.ts).
   * @param emptyModeSeed Records to start with instead when empty-data mode is on (default none).
   */
  protected constructor(seed: readonly T[], storageKey: string, emptyModeSeed: readonly T[] = []) {
    super();
    this.store = persistedSignal(
      storageKey,
      () => structuredClone(initialRecords(seed, emptyModeSeed)) as T[],
    );
    this.records = this.store.asReadonly();
  }

  // ---- Hooks ---------------------------------------------------------------

  protected toListItem(entity: T): TList {
    return entity as unknown as TList;
  }

  protected matchesFilters(item: TList, filters: Partial<TFilter>): boolean {
    return matchesEqualityFilters(item, filters);
  }

  /** Fill derived fields (e.g. totals, snapshots) on the candidate before validation. */
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  protected prepare(candidate: T, isNew: boolean): void {
    /* nothing derived by default */
  }

  /** Throw an AppError (see `validationError`) to reject a create/update. */
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  protected validate(candidate: T, isNew: boolean): void {
    /* no rules by default */
  }

  /** Throw an AppError to block deletion (e.g. record still referenced). */
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  protected beforeDelete(entity: T): void {
    /* allowed by default */
  }

  protected describe(entity: T): string {
    return `#${entity.id}`;
  }

  // ---- Synchronous helpers (mock layer only) -------------------------------

  findById(id: number): T | undefined {
    return this.store().find((item) => item.id === id);
  }

  protected require(id: number): T {
    const entity = this.findById(id);
    if (!entity) {
      throw mockError('NotFound', `${this.entityLabel} #${id} was not found.`);
    }
    return entity;
  }

  protected validationError(message: string, fieldErrors?: Record<string, string>): never {
    throw mockError('Validation', message, fieldErrors);
  }

  protected conflictError(message: string): never {
    throw mockError('Conflict', message);
  }

  /** Case-insensitive uniqueness check for a string field. */
  protected ensureUnique(candidate: T, field: keyof T & string, label: string): void {
    const value = String(candidate[field] ?? '')
      .trim()
      .toLowerCase();
    const duplicate = this.store().some(
      (item) =>
        item.id !== candidate.id &&
        String(item[field] ?? '')
          .trim()
          .toLowerCase() === value,
    );
    if (duplicate) {
      this.validationError(`${label} "${String(candidate[field])}" already exists.`, {
        [field]: `${label} must be unique.`,
      });
    }
  }

  /** Blocks deletion while other records still point at this one. */
  protected ensureNotReferenced(
    name: string,
    usages: readonly [label: string, count: number][],
  ): void {
    const used = usages.filter(([, count]) => count > 0);
    if (used.length > 0) {
      const summary = used.map(([label, count]) => `${count} ${label}`).join(', ');
      this.conflictError(
        `"${name}" cannot be deleted because it is used by ${summary}. Deactivate it instead.`,
      );
    }
  }

  protected nextId(): number {
    return this.store().reduce((max, item) => Math.max(max, item.id), 0) + 1;
  }

  protected respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  // ---- CrudDataService -------------------------------------------------------

  getAll(): Observable<T[]> {
    return this.respond(() => this.store());
  }

  getById(id: number): Observable<T> {
    return this.respond(() => this.require(id));
  }

  query(request: PageRequest<TFilter>): Observable<PagedResult<TList>> {
    return this.respond(() => {
      const filters = request.filters ?? {};
      const rows = this.store()
        .map((entity) => this.toListItem(entity))
        .filter(
          (row) =>
            matchesSearch(row, request.search, this.searchFields) &&
            this.matchesFilters(row, filters),
        );
      return paginate(sortItems(rows, request.sort), request.pageIndex, request.pageSize);
    });
  }

  /** Every row matching a search/filter, unpaged and in store order (used by Excel exports). */
  filterNow(search: string | undefined, filters: Partial<TFilter> | undefined): TList[] {
    return this.store()
      .map((entity) => this.toListItem(entity))
      .filter(
        (row) =>
          matchesSearch(row, search, this.searchFields) &&
          this.matchesFilters(row, filters ?? ({} as Partial<TFilter>)),
      );
  }

  create(model: CreateModel<T>): Observable<T> {
    return this.respond(() => this.createNow(model));
  }

  update(id: number, changes: UpdateModel<T>): Observable<T> {
    return this.respond(() => this.updateNow(id, changes));
  }

  // ---- Synchronous core (also used by batch operations such as Excel import) ----------------

  /** Creates a record immediately with the same rules as `create`. */
  createNow(model: CreateModel<T>, log = true): T {
    const entity = { ...structuredClone(model), id: this.nextId() } as T;
    this.prepare(entity, true);
    this.validate(entity, true);
    this.store.update((items) => [...items, entity]);
    if (log) {
      this.activity.record(
        this.activityCategory,
        `${this.entityLabel} "${this.describe(entity)}" created`,
      );
    }
    return entity;
  }

  /** Updates a record immediately with the same rules as `update`. */
  updateNow(id: number, changes: UpdateModel<T>, log = true): T {
    const existing = this.require(id);
    const updated = { ...existing, ...structuredClone(changes), id } as T;
    this.prepare(updated, false);
    this.validate(updated, false);
    this.store.update((items) => items.map((item) => (item.id === id ? updated : item)));
    if (log) {
      this.activity.record(
        this.activityCategory,
        `${this.entityLabel} "${this.describe(updated)}" updated`,
      );
    }
    return updated;
  }

  /**
   * Runs the create/update rules WITHOUT saving anything (used to preview an import).
   * Returns the validation error, or null when the record would be accepted.
   */
  dryRun(model: CreateModel<T> | UpdateModel<T>, existingId: number | null): AppError | null {
    try {
      if (existingId === null) {
        const entity = { ...structuredClone(model), id: this.nextId() } as T;
        this.prepare(entity, true);
        this.validate(entity, true);
      } else {
        const updated = {
          ...this.require(existingId),
          ...structuredClone(model),
          id: existingId,
        } as T;
        this.prepare(updated, false);
        this.validate(updated, false);
      }
      return null;
    } catch (error) {
      return isAppError(error) ? error : mockError('Unknown', 'The record could not be validated.');
    }
  }

  /** Captures the current records; the returned function restores them (all-or-nothing imports). */
  snapshot(): () => void {
    const saved = structuredClone(this.store());
    return () => this.store.set(structuredClone(saved));
  }

  delete(id: number): Observable<void> {
    return this.respond(() => {
      const existing = this.require(id);
      this.beforeDelete(existing);
      this.store.update((items) => items.filter((item) => item.id !== id));
      this.activity.record(
        this.activityCategory,
        `${this.entityLabel} "${this.describe(existing)}" deleted`,
      );
    });
  }
}
