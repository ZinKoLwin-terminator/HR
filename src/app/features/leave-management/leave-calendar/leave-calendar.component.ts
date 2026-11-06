import { DatePipe } from '@angular/common';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { forkJoin } from 'rxjs';
import { LeaveCalendarDay, LeaveCalendarMonth } from '../../../core/models/leave.models';
import {
  LeaveCalendarDataService,
  LeaveTypeDataService,
} from '../../../core/services/data/leave-data.services';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { addMonths, todayIso } from '../../../shared/utilities/date.util';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';

/** Everyone on leave on one day. */
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';

@Component({
  selector: 'app-leave-day-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule, StatusBadgeComponent],
  template: `
    <h2 mat-dialog-title>{{ data.date | date: 'fullDate' }}</h2>
    <mat-dialog-content>
      @if (data.holiday) {
        <p class="holiday">Holiday: {{ data.holiday }}</p>
      }
      <ul class="list">
        @for (e of data.entries; track e.applicationId) {
          <li>
            <span class="dot" [style.background]="e.color" aria-hidden="true"></span>
            <span class="who">
              <span class="who__name">{{ e.employeeName }}</span>
              <span class="who__meta"
                >{{ e.leaveTypeName }}{{ e.halfDay ? ' · half day' : '' }}</span
              >
            </span>
            <app-status-badge [status]="e.status" />
          </li>
        } @empty {
          <li class="muted">Nobody is on leave.</li>
        }
      </ul>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .holiday {
      margin: 0 0 12px;
      color: var(--app-on-success-container);
      font: var(--mat-sys-label-large);
    }
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 0;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    li:first-child {
      border-top: 0;
    }
    .dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      flex: none;
    }
    .who {
      display: flex;
      flex-direction: column;
      flex: 1;
    }
    .who__name {
      font: var(--mat-sys-label-large);
    }
    .who__meta {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class LeaveDayDialogComponent {
  protected readonly data = inject<LeaveCalendarDay>(MAT_DIALOG_DATA);
}

const MAX_CHIPS = 3;

/** Month calendar of approved (and optionally pending) leave. */
@Component({
  selector: 'app-leave-calendar',
  imports: [
    FilterFieldComponent,
    FilterPopupComponent,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    PageHeaderComponent,
    EmptyStateComponent,
  ],
  templateUrl: './leave-calendar.component.html',
  styleUrl: './leave-calendar.component.scss',
})
export class LeaveCalendarComponent {
  private readonly calendarApi = inject(LeaveCalendarDataService);
  private readonly typesApi = inject(LeaveTypeDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly dialog = inject(MatDialog);

  protected readonly today = todayIso();
  protected readonly maxChips = MAX_CHIPS;
  protected readonly month = signal(this.today.slice(0, 7));
  protected readonly departmentId = signal<number | null>(null);
  protected readonly leaveTypeId = signal<number | null>(null);
  protected readonly includePending = signal(true);

  protected readonly filterCount = computed(
    () =>
      (this.departmentId() === null ? 0 : 1) +
      (this.leaveTypeId() === null ? 0 : 1) +
      (this.includePending() ? 0 : 1),
  );

  protected clearFilters(): void {
    this.departmentId.set(null);
    this.leaveTypeId.set(null);
    this.includePending.set(true);
  }

  private readonly lookups = rxResource({
    stream: () => forkJoin({ org: this.lookupService.load(), types: this.typesApi.getAll() }),
  });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.org.departments ?? [], (d) => d.name),
  );
  protected readonly typeOptions = computed(() =>
    options(this.lookups.value()?.types ?? [], (t) => t.name),
  );

  protected readonly resource = rxResource({
    params: () => ({
      month: this.month(),
      departmentId: this.departmentId(),
      leaveTypeId: this.leaveTypeId(),
      includePending: this.includePending(),
    }),
    stream: ({ params }) => this.calendarApi.getMonth(params),
  });
  /** Keep the previous month visible while the next one loads. */
  protected readonly data = linkedSignal<
    LeaveCalendarMonth | undefined,
    LeaveCalendarMonth | undefined
  >({
    source: this.resource.value,
    computation: (value, previous) => value ?? previous?.value,
  });

  protected readonly monthStart = computed(() => `${this.month()}-01`);
  protected readonly isCurrentMonth = computed(() => this.month() === this.today.slice(0, 7));
  protected readonly weeks = computed(() => {
    const days = this.data()?.days ?? [];
    const weeks: LeaveCalendarDay[][] = [];
    for (let i = 0; i < days.length; i += 7) weeks.push(days.slice(i, i + 7));
    return weeks;
  });
  /** Days of the month that have leave (used by the compact list view on small screens). */
  protected readonly agenda = computed(() =>
    (this.data()?.days ?? []).filter((d) => d.inMonth && (d.entries.length > 0 || d.holiday)),
  );
  protected readonly totalLeaveDays = computed(
    () => (this.data()?.days ?? []).filter((d) => d.inMonth && d.entries.length > 0).length,
  );

  protected shiftMonth(delta: number): void {
    this.month.set(addMonths(this.monthStart(), delta).slice(0, 7));
  }

  protected goToToday(): void {
    this.month.set(this.today.slice(0, 7));
  }

  protected openDay(day: LeaveCalendarDay): void {
    if (day.entries.length === 0 && !day.holiday) {
      return;
    }
    this.dialog.open(LeaveDayDialogComponent, { data: day, width: '460px', maxWidth: '96vw' });
  }

  protected dayLabel(day: LeaveCalendarDay): string {
    const parts = [day.date];
    if (day.holiday) parts.push(day.holiday);
    if (day.entries.length) parts.push(`${day.entries.length} on leave`);
    return parts.join(', ');
  }
}
