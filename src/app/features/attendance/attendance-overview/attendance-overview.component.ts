import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { BarController, BarElement, CategoryScale, Legend, LinearScale, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';
import { AttendanceOverview } from '../../../core/models/attendance.models';
import { AttendanceOverviewDataService } from '../../../core/services/data/attendance-data.services';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { addDays, todayIso } from '../../../shared/utilities/date.util';
import { AttendanceTrendChartComponent } from '../../dashboard/widgets/attendance-trend-chart.component';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';
import { DepartmentAttendanceChartComponent } from './department-attendance-chart.component';

/** How many days back to look for the latest working day when today has no records. */
const MAX_STEP_BACK = 7;

import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';

@Component({
  selector: 'app-attendance-overview',
  imports: [
    FilterFieldComponent,
    FilterPopupComponent,
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    StatCardComponent,
    EmptyStateComponent,
    AvatarComponent,
    AttendanceTrendChartComponent,
    DepartmentAttendanceChartComponent,
  ],
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend],
    }),
  ],
  templateUrl: './attendance-overview.component.html',
  styleUrl: './attendance-overview.component.scss',
})
export class AttendanceOverviewComponent {
  private readonly overviewApi = inject(AttendanceOverviewDataService);
  private readonly lookupService = inject(OrganizationLookupService);

  protected readonly today = todayIso();
  protected readonly date = signal(this.today);
  protected readonly branchId = signal<number | null>(null);
  private userPickedDate = false;
  private steppedBack = 0;

  protected readonly filterCount = computed(() => (this.branchId() === null ? 0 : 1));

  protected clearFilters(): void {
    this.branchId.set(null);
  }

  private readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.branches ?? [], (b) => b.name),
  );

  protected readonly resource = rxResource({
    params: () => ({ date: this.date(), branchId: this.branchId() }),
    stream: ({ params }) => this.overviewApi.getOverview(params.date, params.branchId),
  });
  /** Keep the previous day's figures on screen while the next day loads. */
  protected readonly overview = linkedSignal<
    AttendanceOverview | undefined,
    AttendanceOverview | undefined
  >({
    source: this.resource.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorText = computed(() =>
    this.resource.error() ? errorMessage(this.resource.error()) : '',
  );
  protected readonly isToday = computed(() => this.date() === this.today);
  protected readonly checkedIn = computed(() => {
    const s = this.overview()?.summary;
    return s ? s.present + s.late + s.halfDay : 0;
  });

  constructor() {
    // Weekend / holiday: step back to the most recent day that has attendance.
    effect(() => {
      const value = this.resource.value();
      if (
        value &&
        !this.userPickedDate &&
        value.summary.date === this.date() &&
        value.summary.scheduled === 0 &&
        this.steppedBack < MAX_STEP_BACK
      ) {
        this.steppedBack++;
        this.date.set(addDays(this.date(), -1));
      }
    });
  }

  protected pickDate(value: string): void {
    if (value && value <= this.today) {
      this.userPickedDate = true;
      this.date.set(value);
    }
  }

  protected shiftDay(days: number): void {
    const next = addDays(this.date(), days);
    if (next <= this.today) {
      this.userPickedDate = true;
      this.date.set(next);
    }
  }
}
