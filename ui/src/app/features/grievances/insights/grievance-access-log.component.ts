import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { Entity, PagedResult } from '../../../core/models/common.models';
import {
  GrievanceAuditAction,
  GrievanceAuditEntry,
  GrievanceAuditFilter,
} from '../../../core/models/grievance.models';
import { ADMIN_ROLE_NAME } from '../../../core/models/settings.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { GrievanceCaseDataService } from '../../../core/services/data/grievance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { CellTemplateDirective } from '../../../shared/components/data-table/cell-template.directive';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ISO_DATE_FORMATS, IsoDateAdapter } from '../../../shared/forms/iso-date-adapter';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListQuery } from '../../../shared/utilities/list-query';
import { safeValue } from '../shared/resource-value';

/** The actions of the access log, for the filter. */
const ACTIONS: readonly GrievanceAuditAction[] = [
  'ViewCase',
  'ViewHistory',
  'DownloadAttachment',
  'UploadAttachment',
  'Export',
  'Assign',
  'Unassign',
  'StatusChange',
  'Triage',
  'AddNote',
  'SendMessage',
  'CorrectiveAction',
  'AccessRefused',
];

type AuditRow = GrievanceAuditEntry & Entity;

export const GRIEVANCE_ACCESS_LOG_COLUMNS: TableColumn<AuditRow>[] = [
  { key: 'occurredAtUtc', header: 'When', nowrap: true },
  { key: 'action', header: 'Action' },
  { key: 'caseNo', header: 'Case', value: (r) => r.caseNo ?? '—', nowrap: true },
  { key: 'actorName', header: 'Who', value: (r) => r.actorName ?? '—' },
  { key: 'actorRole', header: 'Role', hideBelow: 'sm' },
  { key: 'detail', header: 'Detail', value: (r) => r.detail ?? '', hideBelow: 'md' },
];

/**
 * The module-wide grievance access log (`grievance-management:View` and HR Administrator): who read,
 * downloaded, exported or changed what, newest first, with the action and a date range. Excel
 * exports have no case. A submitter's own actions carry no name. Other callers see an explanation
 * (the server answers 403 anyway).
 */
@Component({
  selector: 'app-grievance-access-log',
  imports: [
    AppDatePipe,
    CellTemplateDirective,
    DataTableComponent,
    EmptyStateComponent,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    RouterLink,
  ],
  providers: [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ],
  template: `
    <app-page-header
      title="Grievance access log"
      description="Every read, download, export and change on grievance cases, newest first. Logs carry ids and codes only."
    >
      <a pageActions mat-stroked-button routerLink="/grievances/reports">
        <mat-icon>arrow_back</mat-icon>Reports
      </a>
    </app-page-header>

    @if (!isAdmin()) {
      <app-empty-state
        icon="lock"
        title="Administrators only"
        message="The grievance access log is open to HR Administrators."
      />
    } @else {
      <div class="filters" role="group" aria-label="Access log filters">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Action</mat-label>
          <mat-select
            [value]="query.filters().action ?? null"
            (selectionChange)="query.setFilter('action', $event.value)"
          >
            <mat-option [value]="null">All</mat-option>
            @for (a of actions; track a) {
              <mat-option [value]="a">{{ a }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>From</mat-label>
          <input
            matInput
            [matDatepicker]="fromPicker"
            [value]="query.filters().from ?? null"
            (dateChange)="query.setFilter('from', $event.value)"
          />
          <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
          <mat-datepicker #fromPicker />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>To</mat-label>
          <input
            matInput
            [matDatepicker]="toPicker"
            [value]="query.filters().to ?? null"
            (dateChange)="query.setFilter('to', $event.value)"
          />
          <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
          <mat-datepicker #toPicker />
        </mat-form-field>
        <button mat-button type="button" (click)="query.reset()">Clear</button>
      </div>

      @if (page.error() && !result()) {
        <app-empty-state
          icon="error"
          title="The access log could not be loaded"
          [message]="errorText()"
        >
          <button mat-flat-button type="button" (click)="page.reload()">Try again</button>
        </app-empty-state>
      } @else {
        <app-data-table
          caption="Grievance access log"
          [columns]="columns"
          [rows]="result()?.items ?? []"
          [total]="result()?.total ?? 0"
          [pageIndex]="query.pageIndex()"
          [pageSize]="query.pageSize()"
          [pageSizeOptions]="pageSizeOptions"
          [loading]="page.isLoading()"
          [rowLabel]="rowLabel"
          emptyIcon="history"
          emptyTitle="Nothing logged"
          emptyMessage="Adjust the filters."
          (pageChange)="query.onPage($event)"
        >
          <ng-template appCellTemplate="occurredAtUtc" let-row>
            {{ row.occurredAtUtc | appDate }}
          </ng-template>
        </app-data-table>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
    }
  `,
})
export class GrievanceAccessLogComponent {
  private readonly cases = inject(GrievanceCaseDataService);
  private readonly user = inject(CurrentUserService);
  private readonly appConfig = inject(APP_CONFIG);

  protected readonly actions = ACTIONS;
  protected readonly columns = GRIEVANCE_ACCESS_LOG_COLUMNS;
  protected readonly pageSizeOptions = this.appConfig.pageSizeOptions;
  protected readonly rowLabel = (row: AuditRow) => `Access log entry ${row.id}`;
  protected readonly isAdmin = computed(() => this.user.current().roles.includes(ADMIN_ROLE_NAME));

  protected readonly query = new ListQuery<GrievanceAuditFilter>({ pageSize: 25 });
  protected readonly page = rxResource({
    params: () => (this.isAdmin() ? this.query.request() : undefined),
    stream: ({ params }) => this.cases.auditLog(params),
  });
  protected readonly result = linkedSignal<
    PagedResult<AuditRow> | undefined,
    PagedResult<AuditRow> | undefined
  >({
    source: () => safeValue(this.page),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.page.error() ? errorMessage(this.page.error()) : '',
  );
}
