import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable, throwError } from 'rxjs';
import {
  BLACKLIST_EDIT_PERMISSION,
  BLACKLIST_REASON_MAX_LENGTH,
  BlacklistEntry,
} from '../../../core/models/blacklist.models';
import {
  AppError,
  CreateModel,
  PagedResult,
  PageRequest,
} from '../../../core/models/common.models';
import { fullName } from '../../../core/models/employee.models';
import { BlacklistDataService } from '../../../core/services/data/blacklist-data.service';
import { CrudDataService } from '../../../core/services/data/crud-data.service';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';

const HISTORY_KEPT: AppError = {
  code: 'Forbidden',
  message: 'The do-not-rehire list keeps its history: entries are lifted, never edited or deleted.',
};

/**
 * Lets the generic list page drive the do-not-rehire contract. "Create" is `add`; there is no edit
 * or delete (the page switches both off), so those two only refuse.
 */
class BlacklistListService extends CrudDataService<BlacklistEntry> {
  private readonly api: BlacklistDataService;
  constructor(api: BlacklistDataService) {
    super();
    this.api = api;
  }
  getAll(): Observable<BlacklistEntry[]> {
    return throwError(() => HISTORY_KEPT);
  }
  getById(id: number): Observable<BlacklistEntry> {
    return this.api.getById(id);
  }
  query(request: PageRequest<Record<string, unknown>>): Observable<PagedResult<BlacklistEntry>> {
    return this.api.query(request as PageRequest<{ active: 'true' | 'false' }>);
  }
  create(model: CreateModel<BlacklistEntry>): Observable<BlacklistEntry> {
    return this.api.add(model.employeeId, model.reason);
  }
  update(): Observable<BlacklistEntry> {
    return throwError(() => HISTORY_KEPT);
  }
  delete(): Observable<void> {
    return throwError(() => HISTORY_KEPT);
  }
}

/**
 * Do-not-rehire list: former employees who must not be hired again. The route needs
 * `employees:ViewBlacklist`; adding and lifting need `employees:EditBlacklist`. The reason is shown
 * in the details only (never in the table, so it is not printed), and nowhere else in the app.
 */
@Component({
  selector: 'app-do-not-rehire-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading the do-not-rehire list" />
    }
  `,
})
export class DoNotRehirePageComponent {
  private readonly api = inject(BlacklistDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly service = new BlacklistListService(this.api);
  private readonly employees = rxResource({ stream: () => this.employeesApi.getAll() });

  protected readonly config = computed<CrudPageConfig<BlacklistEntry> | null>(() => {
    const all = this.employees.value();
    if (!all) {
      return null;
    }
    const former = all
      .filter((e) => !!e.exitDate)
      .map((e) => ({ value: e.id, label: `${fullName(e)} (${e.employeeCode})` }))
      .sort((a, b) => a.label.localeCompare(b.label));

    return {
      title: 'Do-not-rehire list',
      description:
        'Former employees who must not be hired again. Recruitment only sees a warning that a candidate matches, never who or why. Sample data only.',
      entityLabel: 'do-not-rehire entry',
      service: this.service,
      hasStatus: false,
      editable: false,
      canEdit: () => false,
      canDelete: () => false,
      searchLabel: 'Search by employee code or name',
      defaultSort: { active: 'blacklistedOn', direction: 'desc' },
      permissionKeys: {
        Create: BLACKLIST_EDIT_PERMISSION,
        Edit: BLACKLIST_EDIT_PERMISSION,
        Delete: BLACKLIST_EDIT_PERMISSION,
      },
      filters: [
        {
          key: 'active',
          label: 'Status',
          options: [
            { value: 'true', label: 'Active (in force)' },
            { value: 'false', label: 'Lifted' },
          ],
        },
      ],
      nameOf: (e) => `${e.employeeName} (${e.employeeCode})`,
      columns: [
        {
          key: 'employeeId',
          header: 'Employee',
          sortable: true,
          value: (e) => `${e.employeeName} (${e.employeeCode})`,
        },
        {
          key: 'blacklistedOn',
          header: 'Listed on',
          type: 'date',
          sortable: true,
          nowrap: true,
        },
        { key: 'blacklistedBy', header: 'Listed by', hideBelow: 'md' },
        {
          key: 'liftedOn',
          header: 'Status',
          type: 'status',
          sortable: true,
          value: (e) => (e.liftedOn ? 'Lifted' : 'In force'),
        },
        {
          key: 'liftedDate',
          header: 'Lifted on',
          type: 'date',
          hideBelow: 'lg',
          nowrap: true,
          value: (e) => e.liftedOn,
        },
      ],
      newItem: () => ({}),
      extraActions: [
        {
          id: 'lift',
          label: 'Lift entry',
          icon: 'lock_open',
          permission: 'EditBlacklist',
          visible: (e) => e.liftedOn === null,
          prompt: {
            title: 'Lift do-not-rehire entry?',
            message: (e) =>
              `${e.employeeName} will no longer be flagged in recruitment. The entry stays here as history.`,
            label: 'Reason for lifting',
            required: true,
            confirmText: 'Lift entry',
            maxLength: BLACKLIST_REASON_MAX_LENGTH,
          },
          run: (e, reason) => this.api.lift(e.id, reason, e.rowVersion),
          successMessage: (e) => `The entry for ${e.employeeName} was lifted.`,
        },
      ],
      fields: [
        {
          key: 'employeeId',
          label: 'Former employee',
          type: 'select',
          required: true,
          fullWidth: true,
          hint: 'Only employees with an exit date can be listed.',
          options: () => former,
        },
        {
          key: 'reason',
          label: 'Reason',
          type: 'textarea',
          required: true,
          maxLength: BLACKLIST_REASON_MAX_LENGTH,
          fullWidth: true,
          hint: 'Only people with access to this list can read it.',
        },
        { key: 'blacklistedOn', label: 'Listed on', type: 'date', detailsOnly: true },
        { key: 'blacklistedBy', label: 'Listed by', type: 'text', detailsOnly: true },
        { key: 'liftedOn', label: 'Lifted on', type: 'date', detailsOnly: true },
        { key: 'liftedBy', label: 'Lifted by', type: 'text', detailsOnly: true },
        {
          key: 'liftReason',
          label: 'Reason for lifting',
          type: 'textarea',
          detailsOnly: true,
          fullWidth: true,
        },
      ],
    };
  });
}
