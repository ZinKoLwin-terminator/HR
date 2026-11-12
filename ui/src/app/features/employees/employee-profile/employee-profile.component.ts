import { DatePipe } from '@angular/common';
import {
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { filter, forkJoin } from 'rxjs';
import { fullName } from '../../../core/models/employee.models';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { PermissionService } from '../../../core/services/permission.service';
import { PrintService } from '../../../core/services/print.service';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { formatTenure, todayIso } from '../../../shared/utilities/date.util';
import { OrganizationLookupService, nameMap } from '../../organization/organization-lookups';
import { ProfilePrintComponent } from './profile-print.component';
import { ProfileAttendanceTabComponent } from './tabs/profile-attendance-tab.component';
import { ProfileDocumentsTabComponent } from './tabs/profile-documents-tab.component';
import { ProfileEmploymentTabComponent } from './tabs/profile-employment-tab.component';
import { ProfileHistoryTabComponent } from './tabs/profile-history-tab.component';
import { ProfileLeaveTabComponent } from './tabs/profile-leave-tab.component';
import { ProfileOverviewTabComponent } from './tabs/profile-overview-tab.component';
import { ProfileSalaryTabComponent } from './tabs/profile-salary-tab.component';

const TABS = ['overview', 'employment', 'documents', 'history', 'attendance', 'leave', 'salary'];

@Component({
  selector: 'app-employee-profile',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
    AvatarComponent,
    StatusBadgeComponent,
    EmptyStateComponent,
    ProfileOverviewTabComponent,
    ProfileEmploymentTabComponent,
    ProfileDocumentsTabComponent,
    ProfileHistoryTabComponent,
    ProfileAttendanceTabComponent,
    ProfileLeaveTabComponent,
    ProfileSalaryTabComponent,
    ProfilePrintComponent,
  ],
  templateUrl: './employee-profile.component.html',
  styleUrl: './employee-profile.component.scss',
})
export class EmployeeProfileComponent {
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly toast = inject(UiNotificationService);
  protected readonly permissions = inject(PermissionService);
  private readonly printer = inject(PrintService);
  private readonly injector = inject(Injector);

  /** True while the print layout of the profile is on the page. */
  protected readonly printing = signal(false);

  /** `:id` route parameter. */
  readonly id = input.required<string>();
  /** Optional `?tab=` query parameter (overview, employment, documents, …). */
  readonly tab = input<string>();

  protected readonly employeeId = computed(() => Number(this.id()));
  protected readonly tabIndex = computed(() => Math.max(0, TABS.indexOf(this.tab() ?? 'overview')));

  protected readonly data = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      forkJoin({
        employee: this.employeesApi.getById(params.id),
        org: this.lookupService.load(),
      }),
  });

  protected readonly view = computed(() => {
    const d = this.data.value();
    if (!d) {
      return null;
    }
    const { employee, org } = d;
    const position = nameMap(org.positions, (p) => p.title)(employee.positionId);
    const department = nameMap(org.departments, (x) => x.name)(employee.departmentId);
    const branch = nameMap(org.branches, (b) => b.name)(employee.branchId);
    return {
      employee,
      org,
      name: fullName(employee),
      position,
      department,
      branch,
      tenure: formatTenure(employee.joiningDate, employee.exitDate ?? todayIso()),
    };
  });

  constructor() {
    this.printer.afterPrint$.pipe(takeUntilDestroyed()).subscribe(() => this.printing.set(false));
  }

  /** Lays out the print version (it loads the document list if the user may see it), then prints. */
  protected printProfile(): void {
    if (this.printing()) {
      this.printer.print();
      return;
    }
    this.printing.set(true);
  }

  protected onPrintReady(): void {
    afterNextRender(() => this.printer.print(), { injector: this.injector });
  }

  protected onTab(index: number): void {
    this.router.navigate([], {
      queryParams: { tab: TABS[index] === 'overview' ? null : TABS[index] },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected deleteEmployee(): void {
    const view = this.view();
    if (!view) {
      return;
    }
    this.confirm
      .confirmDelete('Employee', `${view.name} (${view.employee.employeeCode})`)
      .pipe(filter(Boolean))
      .subscribe(() =>
        this.employeesApi.delete(view.employee.id).subscribe({
          next: () => {
            this.toast.success(`Employee "${view.name}" deleted.`);
            this.router.navigate(['/employees/list']);
          },
          error: (error: unknown) => this.toast.error(error),
        }),
      );
  }
}
