import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { forkJoin, map } from 'rxjs';
import { fullName } from '../../../core/models/employee.models';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  LeaveApplicationDataService,
  LeaveTypeDataService,
} from '../../../core/services/data/leave-data.services';
import { PermissionService } from '../../../core/services/permission.service';
import { PrintService } from '../../../core/services/print.service';
import {
  LETTER_KINDS,
  LetterData,
  LetterKind,
  buildLetter,
  formatLetterDate,
} from '../../../core/utilities/letter-templates';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { todayIso } from '../../../shared/utilities/date.util';
import { OrganizationLookupService, nameMap } from '../../organization/organization-lookups';

/**
 * Letters and certificates for one employee, printed from the browser. The wording lives in
 * `core/utilities/letter-templates`; this screen collects the typed details and shows the preview.
 */
@Component({
  selector: 'app-employee-letters',
  imports: [
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    EmptyStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './employee-letters.component.html',
  styleUrl: './employee-letters.component.scss',
})
export class EmployeeLettersComponent {
  /** `:id` route parameter. */
  readonly id = input.required<string>();

  private readonly employeesApi = inject(EmployeeDataService);
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly applicationsApi = inject(LeaveApplicationDataService);
  private readonly leaveTypesApi = inject(LeaveTypeDataService);
  private readonly permissions = inject(PermissionService);
  private readonly printer = inject(PrintService);

  /** Employment and experience letters need employees:View (the route); the leave letter also leave:View. */
  protected readonly canLeaveLetter = computed(() => this.permissions.can('leave:View'));
  protected readonly kinds = computed(() =>
    LETTER_KINDS.filter((k) => k.kind !== 'leave' || this.canLeaveLetter()),
  );

  protected readonly kind = signal<LetterKind>('employment');
  protected readonly referenceNo = signal('');
  protected readonly signatoryName = signal('');
  protected readonly signatoryTitle = signal('');
  protected readonly letterDate = signal(todayIso());
  protected readonly leaveId = signal<number | null>(null);

  protected readonly data = rxResource({
    params: () => ({ id: Number(this.id()) }),
    stream: ({ params }) =>
      forkJoin({
        employee: this.employeesApi.getById(params.id),
        org: this.lookupService.load(),
      }),
  });

  /** Approved leave of this employee; not read at all without leave:View. */
  protected readonly leave = rxResource({
    params: () => (this.canLeaveLetter() ? { id: Number(this.id()) } : undefined),
    stream: ({ params }) =>
      forkJoin({
        types: this.leaveTypesApi.getAll(),
        applications: this.applicationsApi.query({
          pageIndex: 0,
          pageSize: 200,
          filters: { employeeId: params.id, status: 'Approved' },
          sort: { active: 'startDate', direction: 'desc' },
        }),
      }).pipe(
        map(({ types, applications }) => {
          const typeName = nameMap(types, (t) => t.name);
          return applications.items
            .filter((a) => a.employeeId === params.id && a.status === 'Approved')
            .map((a) => ({
              application: a,
              typeName: typeName(a.leaveTypeId),
              label: `${a.applicationNo} · ${typeName(a.leaveTypeId)} · ${formatLetterDate(a.startDate)} to ${formatLetterDate(a.endDate)}`,
            }));
        }),
      ),
  });
  protected readonly leaveChoices = computed(() => this.leave.value() ?? []);

  protected readonly employeeName = computed(() => {
    const d = this.data.value();
    return d ? fullName(d.employee) : '';
  });

  private readonly letterData = computed<LetterData | null>(() => {
    const d = this.data.value();
    if (!d) {
      return null;
    }
    const { employee, org } = d;
    const choice = this.leaveChoices().find((c) => c.application.id === this.leaveId());
    return {
      organizationName: this.printer.organizationName(),
      date: this.letterDate(),
      referenceNo: this.referenceNo().trim(),
      signatoryName: this.signatoryName().trim(),
      signatoryTitle: this.signatoryTitle().trim(),
      employeeName: fullName(employee),
      employeeCode: employee.employeeCode,
      positionTitle: nameMap(org.positions, (p) => p.title)(employee.positionId),
      departmentName: nameMap(org.departments, (x) => x.name)(employee.departmentId),
      employmentType: employee.employmentType,
      joiningDate: employee.joiningDate,
      exitDate: employee.exitDate,
      leave: choice
        ? {
            applicationNo: choice.application.applicationNo,
            leaveTypeName: choice.typeName,
            startDate: choice.application.startDate,
            endDate: choice.application.endDate,
            days: choice.application.days,
            status: choice.application.status,
            actionedOn: choice.application.actionedOn,
          }
        : null,
    };
  });

  protected readonly result = computed(() => {
    const data = this.letterData();
    return data ? buildLetter(this.kind(), data) : null;
  });
  protected readonly dateText = computed(() => formatLetterDate(this.letterDate()));
  protected readonly organizationName = this.printer.organizationName;
  protected readonly missingDetails = computed(
    () => !this.referenceNo().trim() || !this.signatoryName().trim(),
  );
  protected readonly canPrint = computed(() => !!this.result()?.letter && !this.missingDetails());

  protected setKind(kind: LetterKind): void {
    this.kind.set(kind);
  }

  protected typed(target: { set(value: string): void }, event: Event): void {
    target.set((event.target as HTMLInputElement).value);
  }

  protected print(): void {
    if (this.canPrint()) {
      this.printer.print();
    }
  }
}
