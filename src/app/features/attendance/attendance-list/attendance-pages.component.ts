import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AttendanceListComponent } from './attendance-list.component';

@Component({
  selector: 'app-attendance-records-page',
  imports: [PageHeaderComponent, AttendanceListComponent],
  template: `
    <app-page-header
      title="Attendance records"
      description="Daily check-in and check-out records. Filter by employee, department, branch, date range or status."
    />
    <app-attendance-list mode="all" />
  `,
})
export class AttendanceRecordsPageComponent {}

@Component({
  selector: 'app-attendance-exceptions-page',
  imports: [MatTabsModule, PageHeaderComponent, AttendanceListComponent],
  template: `
    <app-page-header
      title="Late arrivals and early departures"
      description="Employees who arrived after the grace period or left before the end of their shift."
    />
    <mat-tab-group animationDuration="0ms" mat-align-tabs="start" aria-label="Exception type">
      <mat-tab label="Late arrivals">
        <ng-template matTabContent>
          <div class="tab"><app-attendance-list mode="late" /></div>
        </ng-template>
      </mat-tab>
      <mat-tab label="Early departures">
        <ng-template matTabContent>
          <div class="tab"><app-attendance-list mode="early" /></div>
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
export class AttendanceExceptionsPageComponent {}
