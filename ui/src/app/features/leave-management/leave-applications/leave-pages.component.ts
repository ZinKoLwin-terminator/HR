import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { forkJoin } from 'rxjs';
import { APP_CONFIG } from '../../../core/configuration/app-config';
import { LeaveApplication } from '../../../core/models/leave.models';
import { HolidayDataService } from '../../../core/services/data/attendance-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  LeaveApplicationDataService,
  LeaveTypeDataService,
} from '../../../core/services/data/leave-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { buildLeaveApplicationsConfig, LeaveListMode } from './leave-applications.config';

/** Loads the lookups a leave list needs and builds the page config for a mode. */
function useLeaveConfig(mode: LeaveListMode) {
  const applications = inject(LeaveApplicationDataService);
  const employeesApi = inject(EmployeeDataService);
  const typesApi = inject(LeaveTypeDataService);
  const holidaysApi = inject(HolidayDataService);
  const weekendDays = inject(APP_CONFIG).weekendDays;
  const lookups = rxResource({
    stream: () =>
      forkJoin({
        employees: employeesApi.getAll(),
        types: typesApi.getAll(),
        holidays: holidaysApi.getAll(),
      }),
  });
  return computed<CrudPageConfig<LeaveApplication> | null>(() => {
    const l = lookups.value();
    return l
      ? buildLeaveApplicationsConfig({ mode, service: applications, weekendDays, ...l })
      : null;
  });
}

/** Leave applications: create drafts, submit, cancel. */
@Component({
  selector: 'app-leave-applications-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading leave applications" />
    }
  `,
})
export class LeaveApplicationsPageComponent {
  protected readonly config = useLeaveConfig('applications');
}

@Component({
  selector: 'app-leave-pending-list',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading pending approvals" />
    }
  `,
})
export class LeavePendingListComponent {
  protected readonly config = useLeaveConfig('pending');
}

@Component({
  selector: 'app-leave-history-list',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading decision history" />
    }
  `,
})
export class LeaveHistoryListComponent {
  protected readonly config = useLeaveConfig('history');
}

/** Approvals queue and decision history. */
@Component({
  selector: 'app-leave-approvals-page',
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    LeavePendingListComponent,
    LeaveHistoryListComponent,
  ],
  template: `
    <app-page-header
      title="Leave approvals"
      description="Approve or reject submitted leave, and review past decisions."
    />
    <mat-tab-group
      animationDuration="0ms"
      mat-align-tabs="start"
      [selectedIndex]="tab() === 'history' ? 1 : 0"
      aria-label="Approvals"
    >
      <mat-tab label="Pending approval">
        <ng-template matTabContent>
          <div class="tab"><app-leave-pending-list /></div>
        </ng-template>
      </mat-tab>
      <mat-tab label="Decision history">
        <ng-template matTabContent>
          <div class="tab"><app-leave-history-list /></div>
        </ng-template>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .tab {
      padding-top: 20px;
    }
  `,
})
export class LeaveApprovalsPageComponent {
  /** `?tab=history` opens the history tab. */
  readonly tab = input<string>();
}
