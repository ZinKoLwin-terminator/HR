import { Component, computed, inject, input, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { HasUnsavedChanges } from '../../../core/guards/unsaved-changes.guard';
import { Employee, fullName } from '../../../core/models/employee.models';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import { ShiftDataService } from '../../../core/services/data/attendance-data.services';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { EmployeeFormComponent, EmployeeFormData } from './employee-form.component';

/** Routed wrapper: loads lookups (and the employee when editing), then shows the form. */
@Component({
  selector: 'app-employee-form-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    EmptyStateComponent,
    EmployeeFormComponent,
  ],
  template: `
    <app-page-header [title]="title()" [description]="description()">
      <a pageActions mat-stroked-button [routerLink]="backLink()">
        <mat-icon>arrow_back</mat-icon>Back
      </a>
    </app-page-header>

    @if (resource.error()) {
      <app-empty-state
        icon="person_off"
        title="Employee could not be loaded"
        message="The employee may have been deleted, or the data could not be read."
      >
        <a mat-flat-button routerLink="/employees/list">Back to employees</a>
      </app-empty-state>
    } @else if (formData(); as data) {
      <app-employee-form [data]="data" (saved)="onSaved($event)" (cancelled)="cancel()" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading form" />
    }
  `,
})
export class EmployeeFormPageComponent implements HasUnsavedChanges {
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly shiftsApi = inject(ShiftDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly router = inject(Router);
  private readonly toast = inject(UiNotificationService);

  /** `:id` route parameter (absent when adding). */
  readonly id = input<string>();
  private readonly employeeId = computed(() => {
    const raw = this.id();
    return raw ? Number(raw) : null;
  });

  protected readonly resource = rxResource({
    params: () => ({ id: this.employeeId() }),
    stream: ({ params }) =>
      forkJoin({
        org: this.lookupService.load(),
        shifts: this.shiftsApi.getAll(),
        employee: params.id ? this.employeesApi.getById(params.id) : of(null),
        suggestedCode: params.id ? of(null) : this.employeesApi.nextEmployeeCode(),
      }),
  });

  protected readonly formData = computed<EmployeeFormData | null>(() => {
    const v = this.resource.value();
    return v
      ? {
          lookups: { org: v.org, shifts: v.shifts },
          employee: v.employee,
          suggestedCode: v.suggestedCode,
        }
      : null;
  });

  protected readonly title = computed(() => (this.employeeId() ? 'Edit employee' : 'Add employee'));
  protected readonly description = computed(() => {
    const employee = this.resource.value()?.employee;
    return employee
      ? `${fullName(employee)} · ${employee.employeeCode}`
      : 'Register a new employee. Fields marked * are required.';
  });
  protected readonly backLink = computed(() =>
    this.employeeId() ? ['/employees', this.employeeId()] : ['/employees/list'],
  );

  private readonly form = viewChild(EmployeeFormComponent);

  hasUnsavedChanges(): boolean {
    return this.form()?.hasUnsavedChanges() ?? false;
  }

  protected onSaved(employee: Employee): void {
    this.toast.success(
      `Employee "${fullName(employee)}" ${this.employeeId() ? 'updated' : 'created'} successfully.`,
    );
    this.router.navigate(['/employees', employee.id]);
  }

  protected cancel(): void {
    this.router.navigate(this.backLink());
  }
}
