import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateModel,
  PagedResult,
  PageRequest,
  UpdateModel,
} from '../../core/models/common.models';
import { GrievanceCategory, GrievanceCategoryFilter } from '../../core/models/grievance.models';
import { GrievanceCategoryDataService } from '../../core/services/data/grievance-data.services';
import { MOCK_GRIEVANCE_CATEGORIES } from '../../mock-data/grievance/grievance-categories.mock';
import { nextGrievanceRowVersion } from '../../mock-data/grievance/grievance-records';
import { paginate, sortItems } from '../../shared/utilities/query.util';
import { InMemoryCrudService } from '../in-memory-crud.service';
import { GrievanceMockAccess } from './grievance-mock-access';
import { GrievanceMockStore } from './grievance-mock-store';

const STALE_MESSAGE = 'The record changed. Reload and try again.';
const CODE_PATTERN = /^[A-Z0-9-]+$/;
const CODE_MESSAGE = 'Code must use capital letters, numbers and dashes only.';

/**
 * Grievance categories (`grievance-management`): CRUD with the API's concurrency emulation (a stale
 * `rowVersion` is a conflict). Reads need `grievance-management:View`, writes Create/Edit/Delete; a
 * category used by a case cannot be deleted (deactivate it instead).
 *
 * BROWSER SIMULATION: the permission checks follow the simulated session's role; nothing is enforced
 * outside the demo. The activity feed gets nothing (decision D6: no `app.ActivityLog` entries).
 */
@Injectable({ providedIn: 'root' })
export class GrievanceCategoryMockService
  extends InMemoryCrudService<GrievanceCategory, GrievanceCategory, GrievanceCategoryFilter>
  implements GrievanceCategoryDataService
{
  private readonly access = inject(GrievanceMockAccess);
  private readonly cases = inject(GrievanceMockStore);
  protected readonly entityLabel = 'Grievance category';
  protected readonly activityCategory = 'system';
  protected override readonly searchFields = ['code', 'name'] as const;

  constructor() {
    super(MOCK_GRIEVANCE_CATEGORIES, 'grievance_categories', MOCK_GRIEVANCE_CATEGORIES);
  }

  private requireView(): void {
    this.access.require(this.access.actor(), ['grievance-management', 'View']);
  }

  private requireWrite(action: 'Create' | 'Edit' | 'Delete'): void {
    this.access.require(this.access.actor(), ['grievance-management', action]);
  }

  // ---- Permission-checked contract (the activity feed is never written: D6) ------------------

  override getAll(): Observable<GrievanceCategory[]> {
    return this.respond(() => {
      this.requireView();
      return [...this.store()].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
    });
  }

  override getById(id: number): Observable<GrievanceCategory> {
    return this.respond(() => {
      this.requireView();
      return this.require(id);
    });
  }

  override query(
    request: PageRequest<GrievanceCategoryFilter>,
  ): Observable<PagedResult<GrievanceCategory>> {
    return this.respond(() => {
      this.requireView();
      const rows = this.filterNow(request.search, request.filters);
      const sorted = request.sort?.active
        ? sortItems(rows, request.sort)
        : [...rows].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
      return paginate(sorted, request.pageIndex, request.pageSize);
    });
  }

  override create(model: CreateModel<GrievanceCategory>): Observable<GrievanceCategory> {
    return this.respond(() => {
      this.requireWrite('Create');
      return this.createNow(model, false);
    });
  }

  override update(
    id: number,
    changes: UpdateModel<GrievanceCategory>,
  ): Observable<GrievanceCategory> {
    return this.respond(() => {
      this.requireWrite('Edit');
      return this.updateNow(id, changes, false);
    });
  }

  override delete(id: number): Observable<void> {
    return this.respond(() => {
      this.requireWrite('Delete');
      const existing = this.require(id);
      this.beforeDelete(existing);
      this.store.update((items) => items.filter((item) => item.id !== id));
    });
  }

  // ---- Hooks ----------------------------------------------------------------------------------

  protected override describe(entity: GrievanceCategory): string {
    return entity.name;
  }

  protected override prepare(candidate: GrievanceCategory, isNew: boolean): void {
    candidate.code = (candidate.code ?? '').trim();
    candidate.name = (candidate.name ?? '').trim();
    candidate.description = (candidate.description ?? '').trim();
    if (isNew) {
      candidate.rowVersion = nextGrievanceRowVersion(undefined);
      return;
    }
    const current = this.findById(candidate.id);
    if (current && candidate.rowVersion !== current.rowVersion) {
      this.conflictError(STALE_MESSAGE);
    }
    candidate.rowVersion = nextGrievanceRowVersion(current?.rowVersion);
  }

  protected override validate(candidate: GrievanceCategory): void {
    if (candidate.code === '') {
      this.validationError('Code is required.', { code: 'Code is required.' });
    }
    if (candidate.code.length > 20) {
      this.validationError('Code must be at most 20 characters.', {
        code: 'Use at most 20 characters.',
      });
    }
    if (!CODE_PATTERN.test(candidate.code)) {
      this.validationError(CODE_MESSAGE, { code: CODE_MESSAGE });
    }
    if (candidate.name === '' || candidate.name.length > 100) {
      this.validationError('Category name is required (at most 100 characters).', {
        name: 'Use 1 to 100 characters.',
      });
    }
    if (candidate.description.length > 300) {
      this.validationError('Description must be at most 300 characters.', {
        description: 'Use at most 300 characters.',
      });
    }
    if (
      !Number.isInteger(candidate.defaultTargetDays) ||
      candidate.defaultTargetDays < 1 ||
      candidate.defaultTargetDays > 365
    ) {
      this.validationError('Default target days must be a whole number from 1 to 365.', {
        defaultTargetDays: 'Use a whole number from 1 to 365.',
      });
    }
    if (!Number.isInteger(candidate.sortOrder) || candidate.sortOrder < 0) {
      this.validationError('Sort order must be a whole number of 0 or more.', {
        sortOrder: 'Use a whole number of 0 or more.',
      });
    }
    if (typeof candidate.allowsAnonymous !== 'boolean') {
      this.validationError('Say whether anonymous reports are allowed.', {
        allowsAnonymous: 'Required.',
      });
    }
    if (candidate.status !== 'Active' && candidate.status !== 'Inactive') {
      this.validationError('Status must be Active or Inactive.', { status: 'Active or Inactive.' });
    }
    this.ensureUnique(candidate, 'code', 'Grievance category code');
    this.ensureUnique(candidate, 'name', 'Grievance category name');
  }

  protected override beforeDelete(entity: GrievanceCategory): void {
    const used = this.cases.records().filter((c) => c.categoryId === entity.id).length;
    this.ensureNotReferenced(entity.name, [['grievance case(s)', used]]);
  }
}
