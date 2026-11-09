import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { AttendanceRecordDataService } from '../../../../core/services/data/attendance-data.services';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { StatCardComponent } from '../../../../shared/components/stat-card/stat-card.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { addDays, formatDuration, todayIso } from '../../../../shared/utilities/date.util';

const ATTENDED = new Set(['Present', 'Late', 'Half Day']);
const NON_WORKING = new Set(['Weekend', 'Holiday']);
const WINDOW_DAYS = 30;

@Component({
  selector: 'app-profile-attendance-tab',
  imports: [
    DatePipe,
    RouterLink,
    MatProgressBarModule,
    StatCardComponent,
    StatusBadgeComponent,
    EmptyStateComponent,
  ],
  template: `
    @if (records.isLoading() && !records.value()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading attendance" />
    } @else {
      <section class="kpis" aria-label="Attendance summary, last 30 days">
        <app-stat-card
          label="Attendance rate"
          [value]="summary().rate + '%'"
          icon="how_to_reg"
          tone="success"
          [hint]="summary().attended + ' of ' + summary().workingDays + ' working days'"
        />
        <app-stat-card
          label="Late arrivals"
          [value]="summary().late"
          icon="alarm"
          tone="warning"
          hint="Last 30 days"
        />
        <app-stat-card
          label="Absences"
          [value]="summary().absent"
          icon="person_off"
          tone="danger"
          hint="Last 30 days"
        />
        <app-stat-card
          label="On leave"
          [value]="summary().onLeave"
          icon="beach_access"
          tone="neutral"
          [hint]="'Avg. ' + summary().averageHours + ' h worked per day'"
        />
      </section>

      <section class="card" aria-labelledby="att-heading">
        <div class="card__header">
          <div>
            <h2 id="att-heading">Recent attendance</h2>
            <p>Last 14 days</p>
          </div>
          <a routerLink="/attendance/records" class="link-button">All attendance records</a>
        </div>
        @if (recent().length) {
          <div class="scroll">
            <table class="simple-table">
              <caption class="cdk-visually-hidden">
                Recent attendance
              </caption>
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Status</th>
                  <th scope="col">Check-in</th>
                  <th scope="col">Check-out</th>
                  <th scope="col" class="num">Worked</th>
                  <th scope="col" class="num">Late (min)</th>
                </tr>
              </thead>
              <tbody>
                @for (r of recent(); track r.id) {
                  <tr>
                    <th scope="row">{{ r.date | date: 'EEE, dd MMM' }}</th>
                    <td><app-status-badge [status]="r.status" /></td>
                    <td>{{ r.checkIn ?? '—' }}</td>
                    <td>{{ r.checkOut ?? '—' }}</td>
                    <td class="num">{{ r.workedMinutes ? format(r.workedMinutes) : '—' }}</td>
                    <td class="num">{{ r.lateMinutes || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <app-empty-state icon="event_busy" title="No attendance records" />
        }
      </section>
    }
  `,
  styles: `
    .kpis {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 16px;
      margin-bottom: 20px;
    }
    .scroll {
      overflow-x: auto;
    }
    @media (max-width: 1023.98px) {
      .kpis {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
})
export class ProfileAttendanceTabComponent {
  readonly employeeId = input.required<number>();
  private readonly attendanceApi = inject(AttendanceRecordDataService);

  protected readonly records = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      this.attendanceApi.query({
        pageIndex: 0,
        pageSize: 120,
        filters: { employeeId: params.id },
        sort: { active: 'date', direction: 'desc' },
      }),
  });

  protected readonly recent = computed(() => (this.records.value()?.items ?? []).slice(0, 14));

  protected readonly summary = computed(() => {
    const from = addDays(todayIso(), -(WINDOW_DAYS - 1));
    const rows = (this.records.value()?.items ?? []).filter(
      (r) => r.date >= from && !NON_WORKING.has(r.status),
    );
    const attendedRows = rows.filter((r) => ATTENDED.has(r.status));
    const worked = attendedRows.reduce((sum, r) => sum + r.workedMinutes, 0);
    const withHours = attendedRows.filter((r) => r.workedMinutes > 0).length;
    return {
      workingDays: rows.length,
      attended: attendedRows.length,
      late: rows.filter((r) => r.status === 'Late').length,
      absent: rows.filter((r) => r.status === 'Absent').length,
      onLeave: rows.filter((r) => r.status === 'On Leave').length,
      rate: rows.length ? Math.round((attendedRows.length / rows.length) * 1000) / 10 : 0,
      averageHours: withHours ? Math.round((worked / withHours / 60) * 10) / 10 : 0,
    };
  });

  protected format(minutes: number): string {
    return formatDuration(minutes);
  }
}
