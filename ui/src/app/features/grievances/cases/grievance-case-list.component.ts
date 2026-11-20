import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { PagedResult, PageRequest, SortState } from '../../../core/models/common.models';
import {
  GRIEVANCE_NOT_DISCLOSED_LABEL,
  GRIEVANCE_PRIORITIES,
  GRIEVANCE_STATUSES,
  GRIEVANCE_STATUS_LABELS,
  GrievanceCase,
  GrievanceCaseFilter,
} from '../../../core/models/grievance.models';
import {
  GrievanceCaseDataService,
  MyGrievanceDataService,
} from '../../../core/services/data/grievance-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../../shared/components/error-banner/error-banner.component';
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { GrievanceWorkspace } from '../shared/grievance-case-actions.component';
import { GrievancePriorityComponent } from '../shared/grievance-priority.component';
import { grievanceStatusLabel } from '../shared/grievance-status-badge.component';
import { safeValue } from '../../../shared/utilities/resource-value';

interface WorkspaceText {
  title: string;
  description: string;
  caption: string;
  link: string;
  emptyTitle: string;
  emptyMessage: string;
}

const TEXT: Record<GrievanceWorkspace, WorkspaceText> = {
  management: {
    title: 'Case management',
    description: 'Every submitted grievance. Archived cases appear only with the Archived filter.',
    caption: 'Grievance cases',
    link: '/grievances/cases',
    emptyTitle: 'No grievance case matches',
    emptyMessage: 'Adjust the search or the filters.',
  },
  investigation: {
    title: 'My investigations',
    description: 'The cases you are assigned to investigate.',
    caption: 'Cases assigned to me',
    link: '/grievances/investigations',
    emptyTitle: 'No case is assigned to you',
    emptyMessage: 'HR assigns investigators from Case management.',
  },
  review: {
    title: 'Reviews',
    description: 'Proposed resolutions waiting for a decision and escalated cases, oldest first.',
    caption: 'Cases waiting for review',
    link: '/grievances/reviews',
    emptyTitle: 'Nothing to review',
    emptyMessage: 'No proposal or escalation is waiting.',
  },
};

/** The columns of the case lists (sortable ones are the API's `GRIEVANCE_CASE_SORT_KEYS`). */
export const GRIEVANCE_CASE_COLUMNS: TableColumn<GrievanceCase>[] = [
  { key: 'caseNo', header: 'Case', sortable: true, nowrap: true },
  { key: 'subject', header: 'Subject' },
  { key: 'categoryName', header: 'Category', sortable: true, hideBelow: 'md' },
  { key: 'submitterLabel', header: 'Submitter', hideBelow: 'md' },
  {
    key: 'departmentName',
    header: 'Department',
    hideBelow: 'lg',
    value: (row) => (row.isAnonymous ? GRIEVANCE_NOT_DISCLOSED_LABEL : (row.departmentName ?? '—')),
  },
  {
    key: 'status',
    header: 'Status',
    type: 'status',
    sortable: true,
    value: (row) => grievanceStatusLabel(row.status),
  },
  { key: 'priority', header: 'Priority', sortable: true, hideBelow: 'sm' },
  { key: 'targetResolutionDate', header: 'Target', sortable: true, nowrap: true, hideBelow: 'sm' },
  { key: 'submittedAtUtc', header: 'Submitted', sortable: true, nowrap: true, hideBelow: 'lg' },
  {
    key: 'leadInvestigatorName',
    header: 'Lead',
    hideBelow: 'lg',
    value: (r) => r.leadInvestigatorName ?? '—',
  },
];

type YesNo = 'yes' | 'no' | null;
const yesNo = (value: boolean | undefined): YesNo =>
  value === undefined || value === null ? null : value ? 'yes' : 'no';

/**
 * The case-team lists: Case management (`grievance-management:View`), My investigations
 * (`grievance-investigation:View`) and Reviews (`grievance-review:View`), chosen by the route's
 * `workspace`. Search, the API's filters, sorting and paging; status pills, priority, overdue and
 * escalated markers, and "Anonymous" for an anonymous submitter.
 */
@Component({
  selector: 'app-grievance-case-list',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    ErrorBannerComponent,
    FilterFieldComponent,
    FilterPopupComponent,
    GrievancePriorityComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
    SearchFieldComponent,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header [title]="text().title" [description]="text().description">
      @if (canRecord()) {
        <a pageActions mat-flat-button routerLink="/grievances/cases/new">
          <mat-icon>add</mat-icon>Record grievance
        </a>
      }
      <ng-container pageSearch>
        <app-search-field
          label="Search by case number, subject or category"
          [value]="query.search()"
          (searchChange)="query.setSearch($event)"
        />
        <app-filter-popup [activeCount]="query.activeFilterCount()" (cleared)="query.reset()">
          <app-filter-field label="Status">
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                placeholder="All"
                aria-label="Status"
                [value]="filters().status ?? null"
                (selectionChange)="query.setFilter('status', $event.value)"
              >
                <mat-option [value]="null">All</mat-option>
                @for (s of statuses; track s) {
                  <mat-option [value]="s">{{ statusLabels[s] }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Priority">
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                placeholder="All"
                aria-label="Priority"
                [value]="filters().priority ?? null"
                (selectionChange)="query.setFilter('priority', $event.value)"
              >
                <mat-option [value]="null">All</mat-option>
                @for (p of priorities; track p) {
                  <mat-option [value]="p">{{ p }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Category">
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                placeholder="All"
                aria-label="Category"
                [value]="filters().categoryId ?? null"
                (selectionChange)="query.setFilter('categoryId', $event.value)"
              >
                <mat-option [value]="null">All</mat-option>
                @for (
                  c of (options.hasValue() ? options.value() : undefined)?.categories ?? [];
                  track c.id
                ) {
                  <mat-option [value]="c.id">{{ c.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Department">
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                placeholder="All"
                aria-label="Department"
                [value]="filters().departmentId ?? null"
                (selectionChange)="query.setFilter('departmentId', $event.value)"
              >
                <mat-option [value]="null">All</mat-option>
                @for (
                  d of (options.hasValue() ? options.value() : undefined)?.departments ?? [];
                  track d.id
                ) {
                  <mat-option [value]="d.id">{{ d.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </app-filter-field>
          @for (flag of flags(); track flag.key) {
            <app-filter-field [label]="flag.label">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  placeholder="All"
                  [attr.aria-label]="flag.label"
                  [value]="flagValue(flag.key)"
                  (selectionChange)="setFlag(flag.key, $event.value)"
                >
                  <mat-option [value]="null">All</mat-option>
                  <mat-option value="yes">Yes</mat-option>
                  <mat-option value="no">No</mat-option>
                </mat-select>
              </mat-form-field>
            </app-filter-field>
          }
          @if (workspace() === 'management') {
            <app-filter-field label="Investigator">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  placeholder="All"
                  aria-label="Investigator"
                  [value]="filters().investigatorEmployeeId ?? null"
                  (selectionChange)="query.setFilter('investigatorEmployeeId', $event.value)"
                >
                  <mat-option [value]="null">All</mat-option>
                  @for (
                    o of (investigators.hasValue() ? investigators.value() : undefined) ?? [];
                    track o.employeeId
                  ) {
                    <mat-option [value]="o.employeeId">{{ o.displayName }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            </app-filter-field>
          }
          <app-filter-field label="Submitted from">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                aria-label="Submitted from"
                [matDatepicker]="fromPicker"
                [value]="filters().submittedFrom ?? null"
                (dateChange)="query.setFilter('submittedFrom', $event.value)"
              />
              <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
              <mat-datepicker #fromPicker />
            </mat-form-field>
          </app-filter-field>
          <app-filter-field label="Submitted to">
            <mat-form-field subscriptSizing="dynamic">
              <input
                matInput
                aria-label="Submitted to"
                [matDatepicker]="toPicker"
                [value]="filters().submittedTo ?? null"
                (dateChange)="query.setFilter('submittedTo', $event.value)"
              />
              <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
              <mat-datepicker #toPicker />
            </mat-form-field>
          </app-filter-field>
        </app-filter-popup>
      </ng-container>
    </app-page-header>

    @if (page.error() && !result()) {
      <app-empty-state icon="error" title="The cases could not be loaded" [message]="errorText()">
        <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
      </app-empty-state>
    } @else {
      @if (page.error()) {
        <app-error-banner [message]="errorText()" (retry)="page.reload()" />
      }
      <app-data-table
        [caption]="text().caption"
        [columns]="columns"
        [rows]="result()?.items ?? []"
        [total]="result()?.total ?? 0"
        [pageIndex]="query.pageIndex()"
        [pageSize]="query.pageSize()"
        [pageSizeOptions]="pageSizeOptions"
        [sort]="query.sort()"
        [loading]="page.isLoading()"
        [rowLabel]="rowLabel"
        [clickableRows]="true"
        emptyIcon="gavel"
        [emptyTitle]="text().emptyTitle"
        [emptyMessage]="text().emptyMessage"
        (pageChange)="query.onPage($event)"
        (sortChange)="sortBy($event)"
        (rowClick)="open($event)"
      >
        <ng-template appCellTemplate="caseNo" let-row>
          <a
            class="row-link"
            [routerLink]="[text().link, row.id]"
            [attr.aria-label]="'Open case ' + row.caseNo"
            >{{ row.caseNo }}</a
          >
          @if (row.archivedAtUtc) {
            <span class="chip">Archived</span>
          }
        </ng-template>
        <ng-template appCellTemplate="subject" let-row>
          <span class="subject">
            {{ row.subject }}
            @if (row.isEscalated) {
              <span class="chip chip--alert"><span aria-hidden="true">▲</span> Escalated</span>
            }
          </span>
        </ng-template>
        <ng-template appCellTemplate="submitterLabel" let-row>
          @if (row.isAnonymous) {
            <span class="chip">Anonymous</span>
          } @else {
            {{ row.submitterLabel }}
          }
        </ng-template>
        <ng-template appCellTemplate="priority" let-row>
          <app-grievance-priority [priority]="row.priority" />
        </ng-template>
        <ng-template appCellTemplate="targetResolutionDate" let-row>
          {{ row.targetResolutionDate ? (row.targetResolutionDate | appDate) : '—' }}
          @if (row.isOverdue) {
            <span class="chip chip--alert">Overdue</span>
          }
        </ng-template>
        <ng-template appCellTemplate="submittedAtUtc" let-row>
          {{ row.submittedAtUtc | appDate }}
        </ng-template>
      </app-data-table>
    }
  `,
  styles: `
    .row-link {
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    .row-link:hover,
    .row-link:focus-visible {
      text-decoration: underline;
    }
    .subject {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .chip {
      display: inline-block;
      margin-left: 4px;
      padding: 0 8px;
      border-radius: 999px;
      background: var(--mat-sys-surface-container-high);
      font: var(--mat-sys-label-small);
      white-space: nowrap;
    }
    .chip--alert {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class GrievanceCaseListComponent {
  /** From the route's data. */
  readonly workspace = input<GrievanceWorkspace>('management');

  private readonly cases = inject(GrievanceCaseDataService);
  private readonly mine = inject(MyGrievanceDataService);
  private readonly router = inject(Router);
  private readonly appConfig = inject(APP_CONFIG);
  private readonly permissions = inject(PermissionService);

  protected readonly columns = GRIEVANCE_CASE_COLUMNS;
  protected readonly statuses = GRIEVANCE_STATUSES.filter((s) => s !== 'Draft');
  protected readonly statusLabels = GRIEVANCE_STATUS_LABELS;
  protected readonly priorities = GRIEVANCE_PRIORITIES;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly text = computed(() => TEXT[this.workspace()]);
  /** HR records a grievance an employee raised outside the system (Case management only). */
  protected readonly canRecord = computed(
    () => this.workspace() === 'management' && this.permissions.can('grievance-management:Create'),
  );
  protected readonly rowLabel = (row: GrievanceCase) => `Case ${row.caseNo}`;

  protected readonly query = new ListQuery<GrievanceCaseFilter>({
    pageSize: this.appConfig.defaultPageSize,
  });
  protected readonly filters = this.query.filters;
  protected readonly flags = computed(() => [
    { key: 'isAnonymous' as const, label: 'Anonymous' },
    { key: 'isEscalated' as const, label: 'Escalated' },
    { key: 'overdue' as const, label: 'Overdue' },
    ...(this.workspace() === 'management' ? [{ key: 'archived' as const, label: 'Archived' }] : []),
  ]);

  /** Category and department choices (every role holds `grievance:View`); empty when refused. */
  protected readonly options = rxResource({
    stream: () => this.mine.options().pipe(catchError(() => of(null))),
  });
  /** The investigator filter (management only, `grievance-management:Edit`). */
  protected readonly investigators = rxResource({
    params: () => (this.workspace() === 'management' ? {} : undefined),
    stream: () => this.cases.investigators().pipe(catchError(() => of([]))),
  });

  protected readonly page = rxResource({
    params: () => ({ workspace: this.workspace(), request: this.query.request() }),
    stream: ({ params }) => {
      const request = params.request as PageRequest<GrievanceCaseFilter>;
      switch (params.workspace) {
        case 'investigation':
          return this.cases.investigations(request);
        case 'review':
          return this.cases.pendingReviews(request);
        default:
          return this.cases.query(request);
      }
    },
  });
  protected readonly result = linkedSignal<
    PagedResult<GrievanceCase> | undefined,
    PagedResult<GrievanceCase> | undefined
  >({
    source: () => safeValue(this.page),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );

  protected flagValue(key: 'isAnonymous' | 'isEscalated' | 'overdue' | 'archived'): YesNo {
    return yesNo(this.filters()[key]);
  }

  protected setFlag(
    key: 'isAnonymous' | 'isEscalated' | 'overdue' | 'archived',
    value: YesNo,
  ): void {
    this.query.setFilter(key, value === null ? null : value === 'yes');
  }

  protected sortBy(sort: SortState): void {
    this.query.setSort(sort);
  }

  protected open(row: GrievanceCase): void {
    void this.router.navigate([this.text().link, row.id]);
  }
}
