import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { PunchKind } from '../../../core/models/attendance.models';
import { fullName } from '../../../core/models/employee.models';
import { CurrentUserService } from '../../../core/services/current-user.service';
import {
  AttendanceRecordDataService,
  HolidayDataService,
  ShiftDataService,
} from '../../../core/services/data/attendance-data.services';
import { dayOffOn } from '../../../core/utilities/attendance-calc';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { formatDuration, todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions } from '../../organization/organization-lookups';

type DayState = 'idle' | 'working' | 'done' | 'leave';

const pad = (n: number) => String(n).padStart(2, '0');

@Component({
  selector: 'app-check-in',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './check-in.component.html',
  styleUrl: './check-in.component.scss',
})
export class CheckInComponent {
  private readonly attendanceApi = inject(AttendanceRecordDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly shiftsApi = inject(ShiftDataService);
  private readonly holidaysApi = inject(HolidayDataService);
  private readonly toast = inject(UiNotificationService);
  private readonly user = inject(CurrentUserService);

  // Live clock
  protected readonly now = signal(new Date());
  protected readonly clock = computed(() => {
    const d = this.now();
    return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  });

  /** The demo has no login, so the person punching can be chosen. */
  protected readonly employeeId = signal<number>(this.user.current().employeeId ?? 2);
  protected readonly busy = signal(false);

  private readonly people = rxResource({
    stream: () =>
      forkJoin({ employees: this.employeesApi.getAll(), shifts: this.shiftsApi.getAll() }),
  });
  protected readonly employeeOptions = computed(() =>
    employeeOptions(this.people.value()?.employees ?? []),
  );
  protected readonly employee = computed(() =>
    this.people.value()?.employees.find((e) => e.id === this.employeeId()),
  );
  protected readonly shift = computed(() => {
    const shiftId = this.employee()?.shiftId;
    return this.people.value()?.shifts.find((s) => s.id === shiftId) ?? null;
  });

  protected readonly today = todayIso();

  /** Loaded on its own: if it fails, the screen still works and only the day-off notice is missing. */
  private readonly holidays = rxResource({ stream: () => this.holidaysApi.getAll() });
  /** Today is a holiday, or not one of the shift's working days (same rule as the server). */
  protected readonly dayOff = computed(() =>
    dayOffOn(this.today, this.shift(), this.holidays.hasValue() ? this.holidays.value() : []),
  );
  protected readonly dayOffNotice = computed(() => {
    const off = this.dayOff();
    if (!off) return '';
    const day = off.holiday
      ? `Today is a holiday: ${off.holiday.name}.`
      : `Today is not a working day of the ${this.shift()?.name ?? 'shift'}.`;
    return `${day} Working today is recorded as Present, with no late minutes.`;
  });

  protected readonly recent = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      this.attendanceApi.query({
        pageIndex: 0,
        pageSize: 8,
        filters: { employeeId: params.id },
        sort: { active: 'date', direction: 'desc' },
      }),
  });
  protected readonly history = computed(() => this.recent.value()?.items ?? []);
  protected readonly todayRecord = computed(
    () => this.history().find((r) => r.date === this.today) ?? null,
  );

  protected readonly state = computed<DayState>(() => {
    const record = this.todayRecord();
    if (record?.status === 'On Leave') return 'leave';
    if (!record?.checkIn) return 'idle';
    return record.checkOut ? 'done' : 'working';
  });

  protected readonly heading = computed(() => {
    const record = this.todayRecord();
    switch (this.state()) {
      case 'leave':
        return 'You are on approved leave today';
      case 'working':
        return `Checked in at ${record?.checkIn}`;
      case 'done':
        return `Day complete · ${formatDuration(record?.workedMinutes ?? 0)} worked`;
      default: {
        const off =
          record?.status === 'Weekend' || record?.status === 'Holiday'
            ? record.status
            : this.dayOff()?.status;
        return off ? `Today is a ${off.toLowerCase()}` : 'Ready to check in';
      }
    }
  });

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected selectEmployee(id: number): void {
    this.employeeId.set(id);
  }

  protected punch(kind: PunchKind): void {
    if (this.busy()) {
      return; // prevent double submission
    }
    const d = new Date();
    const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
    this.busy.set(true);
    this.attendanceApi
      .punch({ employeeId: this.employeeId(), date: todayIso(), time, kind })
      .subscribe({
        next: (record) => {
          this.busy.set(false);
          const name = this.employee() ? fullName(this.employee()!) : 'Employee';
          const off = this.dayOff();
          const note =
            record.status === 'Late'
              ? ` — marked late (${record.lateMinutes} min)`
              : off
                ? ` — worked on a ${off.status.toLowerCase()}, recorded as Present`
                : '';
          this.toast.success(
            kind === 'in'
              ? `${name} checked in at ${time}${note}.`
              : `${name} checked out at ${time}. Worked ${formatDuration(record.workedMinutes)}.`,
          );
          this.recent.reload();
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.toast.error(error);
        },
      });
  }

  protected duration(minutes: number): string {
    return minutes > 0 ? formatDuration(minutes) : '—';
  }
}
