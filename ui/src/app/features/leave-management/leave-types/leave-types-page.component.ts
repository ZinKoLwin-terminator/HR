import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { LeaveType } from '../../../core/models/leave.models';
import { LeaveTypeDataService } from '../../../core/services/data/leave-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';
import { LeaveApplicationDataService } from '../../../core/services/data/leave-data.services';

const GENDER_OPTIONS: FieldOption[] = [
  { value: 'Female', label: 'Women only' },
  { value: 'Male', label: 'Men only' },
];

@Component({
  selector: 'app-leave-types-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading leave types" />
    }
  `,
})
export class LeaveTypesPageComponent {
  private readonly types = inject(LeaveTypeDataService);
  private readonly applicationsApi = inject(LeaveApplicationDataService);
  private readonly applications = rxResource({ stream: () => this.applicationsApi.getAll() });

  protected readonly config = computed<CrudPageConfig<LeaveType> | null>(() => {
    const apps = this.applications.value();
    if (!apps) {
      return null;
    }
    const used = new Map<number, number>();
    for (const a of apps) used.set(a.leaveTypeId, (used.get(a.leaveTypeId) ?? 0) + 1);

    return {
      title: 'Leave types',
      description: 'Categories of leave, their yearly entitlement and rules.',
      entityLabel: 'Leave type',
      service: this.types,
      searchLabel: 'Search by code or name',
      defaultSort: { active: 'name', direction: 'asc' },
      filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
      nameOf: (t) => t.name,
      columns: [
        { key: 'code', header: 'Code', sortable: true, width: '90px' },
        { key: 'name', header: 'Leave type', sortable: true },
        {
          key: 'defaultDaysPerYear',
          header: 'Days / year',
          type: 'number',
          align: 'end',
          sortable: true,
        },
        {
          key: 'isPaid',
          header: 'Pay',
          hideBelow: 'sm',
          value: (t) => (t.isPaid ? 'Paid' : 'Unpaid'),
        },
        {
          key: 'requiresDocument',
          header: 'Document',
          hideBelow: 'md',
          value: (t) => (t.requiresDocument ? 'Required' : '—'),
        },
        {
          key: 'maxConsecutiveDays',
          header: 'Max at a time',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          sortable: true,
        },
        {
          key: 'carryForward',
          header: 'Carry forward',
          hideBelow: 'lg',
          value: (t) => (t.carryForward ? `Up to ${t.maxCarryForwardDays} days` : 'No'),
        },
        {
          key: 'eligibleGender',
          header: 'Eligibility',
          hideBelow: 'lg',
          value: (t) =>
            t.eligibleGender
              ? t.eligibleGender === 'Female'
                ? 'Women only'
                : 'Men only'
              : 'Everyone',
        },
        {
          key: 'applications',
          header: 'Applications',
          type: 'number',
          align: 'end',
          hideBelow: 'lg',
          value: (t) => used.get(t.id) ?? 0,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({
        defaultDaysPerYear: 10,
        isPaid: true,
        requiresDocument: false,
        maxConsecutiveDays: 5,
        carryForward: false,
        maxCarryForwardDays: 0,
        eligibleGender: null,
        color: '#3f6fd8',
        status: 'Active',
      }),
      fields: [
        {
          key: 'code',
          label: 'Code',
          type: 'text',
          required: true,
          pattern: 'code',
          maxLength: 6,
          lockedOnEdit: true,
          hint: 'e.g. SAB',
        },
        { key: 'name', label: 'Leave type name', type: 'text', required: true, maxLength: 60 },
        { key: 'description', label: 'Description', type: 'textarea', maxLength: 250 },
        {
          key: 'defaultDaysPerYear',
          label: 'Default days per year',
          type: 'number',
          required: true,
          min: 0,
          max: 365,
          hint: 'Policies can override this per grade',
        },
        {
          key: 'maxConsecutiveDays',
          label: 'Maximum consecutive days',
          type: 'number',
          required: true,
          min: 1,
          max: 365,
        },
        {
          key: 'eligibleGender',
          label: 'Eligibility',
          type: 'select',
          nullable: true,
          options: () => GENDER_OPTIONS,
          hint: 'Empty = everyone',
        },
        { key: 'color', label: 'Calendar colour', type: 'color' },
        { key: 'isPaid', label: 'Paid leave', type: 'toggle' },
        { key: 'requiresDocument', label: 'Supporting document required', type: 'toggle' },
        { key: 'carryForward', label: 'Unused days carry forward', type: 'toggle' },
        {
          key: 'maxCarryForwardDays',
          label: 'Maximum carry-forward days',
          type: 'number',
          required: true,
          min: 0,
          max: 365,
        },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
      ],
    };
  });
}
