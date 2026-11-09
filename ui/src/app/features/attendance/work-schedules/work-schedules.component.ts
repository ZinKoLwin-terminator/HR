import { DatePipe } from '@angular/common';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { WorkSchedule } from '../../../core/models/attendance.models';
import { WorkScheduleDataService } from '../../../core/services/data/attendance-data.services';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { addDays, dayOfWeek, todayIso } from '../../../shared/utilities/date.util';
import { OrganizationLookupService, options } from '../../organization/organization-lookups';

/** Monday of the week containing `date`. */
export function mondayOf(date: string): string {
  return addDays(date, -((dayOfWeek(date) + 6) % 7));
}

/** Weekly roster grid: shifts, days off, holidays and approved leave per employee. */
import {
  FilterFieldComponent,
  FilterPopupComponent,
} from '../../../shared/components/filter-popup/filter-popup.component';

@Component({
  selector: 'app-work-schedules',
  imports: [
    FilterFieldComponent,
    FilterPopupComponent,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeaderComponent,
    EmptyStateComponent,
  ],
  templateUrl: './work-schedules.component.html',
  styleUrl: './work-schedules.component.scss',
})
export class WorkSchedulesComponent {
  private readonly scheduleApi = inject(WorkScheduleDataService);
  private readonly lookupService = inject(OrganizationLookupService);

  protected readonly today = todayIso();
  protected readonly weekStart = signal(mondayOf(todayIso()));
  protected readonly departmentId = signal<number | null>(null);
  protected readonly branchId = signal<number | null>(null);

  protected readonly filterCount = computed(
    () => (this.departmentId() === null ? 0 : 1) + (this.branchId() === null ? 0 : 1),
  );

  protected clearFilters(): void {
    this.departmentId.set(null);
    this.branchId.set(null);
  }

  private readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly departmentOptions = computed(() =>
    options(this.lookups.value()?.departments ?? [], (d) => d.name),
  );
  protected readonly branchOptions = computed(() =>
    options(this.lookups.value()?.branches ?? [], (b) => b.name),
  );

  protected readonly schedule = rxResource({
    params: () => ({
      weekStart: this.weekStart(),
      departmentId: this.departmentId(),
      branchId: this.branchId(),
    }),
    stream: ({ params }) => this.scheduleApi.getWeek(params),
  });
  /** Keeps the previous week on screen while the next one loads (no flash). */
  protected readonly data = linkedSignal<WorkSchedule | undefined, WorkSchedule | undefined>({
    source: this.schedule.value,
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  protected readonly isThisWeek = computed(() => this.weekStart() === mondayOf(this.today));

  protected shiftWeek(weeks: number): void {
    this.weekStart.set(addDays(this.weekStart(), weeks * 7));
  }

  protected goToThisWeek(): void {
    this.weekStart.set(mondayOf(this.today));
  }
}
