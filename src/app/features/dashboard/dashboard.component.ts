import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  BarController,
  BarElement,
  CategoryScale,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
} from 'chart.js';
import { provideCharts } from 'ng2-charts';
import { CurrentUserService } from '../../core/services/current-user.service';
import { DashboardDataService } from '../../core/services/data/dashboard-data.service';
import { errorMessage } from '../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { ActivityFeedCardComponent } from './widgets/activity-feed-card.component';
import { AttendanceTrendChartComponent } from './widgets/attendance-trend-chart.component';
import { DepartmentChartComponent } from './widgets/department-chart.component';
import { HeadcountGrowthChartComponent } from './widgets/headcount-growth-chart.component';
import { PayrollSummaryCardComponent } from './widgets/payroll-summary-card.component';
import { PendingLeavesCardComponent } from './widgets/pending-leaves-card.component';
import { UpcomingEventsCardComponent } from './widgets/upcoming-events-card.component';

@Component({
  selector: 'app-dashboard',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    StatCardComponent,
    EmptyStateComponent,
    AttendanceTrendChartComponent,
    DepartmentChartComponent,
    HeadcountGrowthChartComponent,
    PayrollSummaryCardComponent,
    PendingLeavesCardComponent,
    ActivityFeedCardComponent,
    UpcomingEventsCardComponent,
  ],
  // Chart.js parts are registered here so they load with this lazy route only.
  providers: [
    provideCharts({
      registerables: [
        BarController,
        BarElement,
        LineController,
        LineElement,
        PointElement,
        CategoryScale,
        LinearScale,
        Tooltip,
        Legend,
      ],
    }),
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly dashboardApi = inject(DashboardDataService);
  protected readonly user = inject(CurrentUserService);

  protected readonly summary = rxResource({ stream: () => this.dashboardApi.getSummary() });
  protected readonly errorText = computed(() =>
    this.summary.error() ? errorMessage(this.summary.error()) : '',
  );

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part = hour < 12 ? 'morning' : hour < 18 ? 'afternoon' : 'evening';
    return `Good ${part}, ${this.user.current().displayName.split(' ')[0]}`;
  });

  protected readonly today = new Date();

  protected percent(part: number, whole: number): string {
    return whole ? `${Math.round((part / whole) * 100)}%` : '0%';
  }
}
