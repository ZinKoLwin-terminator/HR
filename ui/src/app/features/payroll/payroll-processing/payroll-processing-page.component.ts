import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { PAYROLL_RUN_STATUSES, PayrollRun } from '../../../core/models/payroll.models';
import { PayrollRunDataService } from '../../../core/services/data/payroll-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudExtraAction, CrudPageConfig } from '../../../shared/components/crud/crud.models';

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
].map((label, i) => ({ value: i + 1, label }));

/** Payroll periods: create, process, review, approve and mark as paid (all sample data). */
@Component({
  selector: 'app-payroll-processing-page',
  imports: [CrudPageComponent],
  template: `<app-crud-page [config]="config" />`,
})
export class PayrollProcessingPageComponent {
  private readonly runs = inject(PayrollRunDataService);
  private readonly router = inject(Router);

  private readonly year = new Date().getFullYear();
  private readonly isDraft = (r: PayrollRun) => r.status === 'Draft';

  private readonly review: CrudExtraAction<PayrollRun> = {
    id: 'review',
    permission: 'View',
    label: 'Review',
    icon: 'fact_check',
    silent: true,
    run: (r) => {
      this.router.navigate(['/payroll/processing', r.id]);
      return of(null);
    },
    successMessage: () => '',
  };
  private readonly process: CrudExtraAction<PayrollRun> = {
    id: 'process',
    label: 'Process payroll',
    icon: 'calculate',
    visible: this.isDraft,
    confirm: {
      title: 'Process payroll?',
      message: (r) =>
        `Generate sample payslips for everyone employed in ${r.periodName}? You can review them before approving.`,
      confirmText: 'Process',
    },
    run: (r) => this.runs.process(r.id),
    successMessage: (r) => `${r.periodName} processed. Review the payslips, then approve.`,
  };
  private readonly approve: CrudExtraAction<PayrollRun> = {
    id: 'approve',
    permission: 'Approve',
    label: 'Approve',
    icon: 'check_circle',
    visible: (r) => r.status === 'Pending Approval',
    confirm: {
      title: 'Approve payroll?',
      message: (r) => `Approve ${r.periodName} for ${r.employeeCount} employees (sample figures)?`,
      confirmText: 'Approve',
    },
    run: (r) => this.runs.approve(r.id),
    successMessage: (r) => `${r.periodName} approved.`,
  };
  private readonly markPaid: CrudExtraAction<PayrollRun> = {
    id: 'paid',
    permission: 'Approve',
    label: 'Mark as paid (sample)',
    icon: 'paid',
    visible: (r) => r.status === 'Approved',
    confirm: {
      title: 'Mark as paid?',
      message: (r) =>
        `This only records ${r.periodName} as paid in the demo. No payment is made and no bank is contacted.`,
      confirmText: 'Mark as paid',
    },
    run: (r) => this.runs.markPaid(r.id),
    successMessage: (r) => `${r.periodName} marked as paid.`,
  };
  private readonly returnToDraft: CrudExtraAction<PayrollRun> = {
    id: 'return',
    label: 'Return to draft',
    icon: 'undo',
    destructive: true,
    visible: (r) => r.status === 'Pending Approval' || r.status === 'Approved',
    prompt: {
      title: 'Return to draft?',
      message: (r) => `The payslips for ${r.periodName} will be removed and the period reopened.`,
      label: 'Reason (optional)',
      required: false,
      confirmText: 'Return to draft',
    },
    run: (r, reason) => this.runs.returnToDraft(r.id, reason),
    successMessage: (r) => `${r.periodName} returned to draft.`,
  };

  protected readonly config: CrudPageConfig<PayrollRun> = {
    title: 'Payroll processing',
    description:
      'Create a period, process it to generate sample payslips, review, approve, then mark it as paid. No real payments are made.',
    entityLabel: 'Payroll period',
    service: this.runs,
    hasStatus: false,
    searchLabel: 'Search by period',
    defaultSort: { active: 'startDate', direction: 'desc' },
    nameOf: (r) => r.periodName,
    canEdit: this.isDraft,
    canDelete: this.isDraft,
    extraActions: [this.review, this.process, this.approve, this.markPaid, this.returnToDraft],
    filters: [
      {
        key: 'status',
        label: 'Status',
        options: PAYROLL_RUN_STATUSES.map((s) => ({ value: s, label: s })),
      },
    ],
    columns: [
      { key: 'startDate', header: 'Period', sortable: true, value: (r) => r.periodName },
      {
        key: 'payDate',
        header: 'Pay date',
        type: 'date',
        sortable: true,
        hideBelow: 'md',
        nowrap: true,
      },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
      {
        key: 'employeeCount',
        header: 'Employees',
        type: 'number',
        align: 'end',
        sortable: true,
        hideBelow: 'sm',
      },
      {
        key: 'totalGross',
        header: 'Gross',
        type: 'currency',
        align: 'end',
        sortable: true,
        hideBelow: 'md',
      },
      {
        key: 'totalDeductions',
        header: 'Deductions',
        type: 'currency',
        align: 'end',
        hideBelow: 'lg',
      },
      { key: 'totalNet', header: 'Net', type: 'currency', align: 'end', sortable: true },
    ],
    newItem: () => ({ year: this.year, month: new Date().getMonth() + 1 }),
    // Server-owned fields (the service derives names and dates from the year and month).
    fixedValues: {
      periodName: '',
      startDate: '',
      endDate: '',
      status: 'Draft',
      employeeCount: 0,
      totalBasic: 0,
      totalAllowances: 0,
      totalGross: 0,
      totalDeductions: 0,
      totalNet: 0,
      warnings: [],
      processedOn: null,
      approvedBy: null,
      approvedOn: null,
      paidOn: null,
    },
    fields: [
      {
        key: 'year',
        label: 'Year',
        type: 'select',
        required: true,
        options: () => [this.year, this.year - 1].map((y) => ({ value: y, label: String(y) })),
      },
      { key: 'month', label: 'Month', type: 'select', required: true, options: () => MONTHS },
      {
        key: 'payDate',
        label: 'Pay date',
        type: 'date',
        hint: 'Defaults to the last day of the month',
      },
      { key: 'periodName', label: 'Period', type: 'text', detailsOnly: true },
      { key: 'status', label: 'Status', type: 'text', detailsOnly: true },
      { key: 'employeeCount', label: 'Employees paid', type: 'number', detailsOnly: true },
      { key: 'totalGross', label: 'Total gross (sample)', type: 'number', detailsOnly: true },
      { key: 'totalNet', label: 'Total net (sample)', type: 'number', detailsOnly: true },
      { key: 'approvedBy', label: 'Approved by', type: 'text', detailsOnly: true },
      { key: 'approvedOn', label: 'Approved on', type: 'date', detailsOnly: true },
      { key: 'paidOn', label: 'Paid on', type: 'date', detailsOnly: true },
    ],
  };
}
