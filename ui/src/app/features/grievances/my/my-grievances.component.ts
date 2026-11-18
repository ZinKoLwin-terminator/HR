import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../../core/models/common.models';
import {
  GRIEVANCE_STATUSES,
  GRIEVANCE_STATUS_LABELS,
  GrievanceStatus,
  MyGrievance,
  MyGrievanceFilter,
} from '../../../core/models/grievance.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { MyGrievanceDataService } from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { grievanceStatusLabel } from '../shared/grievance-status-badge.component';
import { safeValue } from '../shared/resource-value';

/** The columns of My Grievances. The server's order (latest update first) is the default. */
export const MY_GRIEVANCE_COLUMNS: TableColumn<MyGrievance>[] = [
  { key: 'subject', header: 'Subject' },
  { key: 'caseNo', header: 'Case number', nowrap: true, value: (row) => row.caseNo ?? 'Draft' },
  { key: 'categoryName', header: 'Category', hideBelow: 'sm' },
  {
    key: 'status',
    header: 'Status',
    type: 'status',
    value: (row) => grievanceStatusLabel(row.status),
  },
  { key: 'submittedAtUtc', header: 'Submitted', nowrap: true, hideBelow: 'md' },
  { key: 'lastUpdateAtUtc', header: 'Last update', nowrap: true, hideBelow: 'sm' },
];

/**
 * The signed-in employee's grievances, drafts included: search by subject or case number, a status
 * filter, paging, status pills and a dot on cases with an update not yet opened. A row opens the
 * grievance.
 */
@Component({
  selector: 'app-my-grievances',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
    SearchFieldComponent,
  ],
  template: `
    <app-page-header
      title="My grievances"
      description="Everything you have raised with HR, newest update first. A dot marks an update you have not opened."
    >
      @if (canCreate()) {
        <a pageActions mat-flat-button routerLink="/grievances/my/new">
          <mat-icon>add</mat-icon>New grievance
        </a>
      }
      <ng-container pageSearch>
        <app-search-field
          label="Search by subject or case number"
          [value]="query.search()"
          (searchChange)="query.setSearch($event)"
        />
        <mat-form-field class="status-filter" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select
            [value]="query.filters().status ?? null"
            (selectionChange)="query.setFilter('status', $event.value)"
          >
            <mat-option [value]="null">All statuses</mat-option>
            @for (s of statuses; track s) {
              <mat-option [value]="s">{{ statusLabels[s] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </ng-container>
    </app-page-header>

    @if (!linked()) {
      <app-empty-state
        icon="person_off"
        title="No employee record is linked to your account"
        message="Grievances are raised by employees. Ask HR to link your user account to your employee record."
      />
    } @else if (page.error() && !result()) {
      <app-empty-state
        icon="error"
        title="Your grievances could not be loaded"
        [message]="errorText()"
      >
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      <app-data-table
        caption="My grievances"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyIcon="forum"
        [emptyTitle]="filtered() ? 'No grievance matches' : 'You have not raised a grievance'"
        [emptyMessage]="
          filtered()
            ? 'Try another search or status.'
            : 'Use New grievance to tell HR about a problem at work.'
        "
        (pageChange)="query.onPage($event)"
        (rowClick)="open($event)"
      >
        <ng-template appCellTemplate="subject" let-row>
          <span class="subject">
            @if (row.hasUnreadUpdate) {
              <span class="unread" title="New update"></span>
              <span class="cdk-visually-hidden">New update:</span>
            }
            <a
              class="row-link"
              [routerLink]="['/grievances/my', row.id]"
              [attr.aria-label]="'Open grievance ' + row.subject"
              >{{ row.subject }}</a
            >
            @if (row.isAnonymous) {
              <span class="chip">Anonymous</span>
            }
          </span>
        </ng-template>
        <ng-template appCellTemplate="submittedAtUtc" let-row>
          {{ row.submittedAtUtc ? (row.submittedAtUtc | appDate) : '—' }}
        </ng-template>
        <ng-template appCellTemplate="lastUpdateAtUtc" let-row>
          {{ row.lastUpdateAtUtc ?? row.createdAtUtc | appDate }}
        </ng-template>
      </app-data-table>
    }
  `,
  styles: `
    .status-filter {
      width: 200px;
    }
    .subject {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .row-link {
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .row-link:hover,
    .row-link:focus-visible {
      text-decoration: underline;
    }
    .unread {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--mat-sys-error);
    }
    .chip {
      padding: 0 8px;
      border-radius: 999px;
      background: var(--mat-sys-surface-container-high);
      font: var(--mat-sys-label-small);
    }
  `,
})
export class MyGrievancesComponent {
  private readonly grievances = inject(MyGrievanceDataService);
  private readonly router = inject(Router);
  private readonly user = inject(CurrentUserService);
  private readonly permissions = inject(PermissionService);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly columns = MY_GRIEVANCE_COLUMNS;
  protected readonly statuses: readonly GrievanceStatus[] = GRIEVANCE_STATUSES;
  protected readonly statusLabels = GRIEVANCE_STATUS_LABELS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly rowLabel = (row: MyGrievance) => `Grievance ${row.subject}`;

  protected readonly linked = computed(() => {
    const id = this.user.current().employeeId;
    return id !== null && id > 0;
  });
  protected readonly canCreate = computed(
    () => this.linked() && this.permissions.can('grievance:Create'),
  );

  protected readonly query = new ListQuery<MyGrievanceFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });
  protected readonly filtered = computed(() => this.query.activeFilterCount() > 0);

  protected readonly page = rxResource({
    params: () => (this.linked() ? this.query.request() : undefined),
    stream: ({ params }) => this.grievances.query(params as PageRequest<MyGrievanceFilter>),
  });
  /** Keeps the last page while the next one loads, so the table does not blank. */
  protected readonly result = linkedSignal<
    PagedResult<MyGrievance> | undefined,
    PagedResult<MyGrievance> | undefined
  >({
    source: () => safeValue(this.page),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected open(row: MyGrievance): void {
    void this.router.navigate(['/grievances/my', row.id]);
  }
}
