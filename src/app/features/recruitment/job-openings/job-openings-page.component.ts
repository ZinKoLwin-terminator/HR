import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import {
  JOB_OPENING_STATUSES,
  JobOpening,
  JobOpeningStatus,
  RECRUITMENT_EMPLOYMENT_TYPES,
} from '../../../core/models/recruitment.models';
import { DepartmentDataService } from '../../../core/services/data/organization-data.services';
import { EmployeeDataService } from '../../../core/services/data/employee-data.service';
import {
  ApplicationDataService,
  JobOpeningDataService,
} from '../../../core/services/data/recruitment-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import {
  CrudExtraAction,
  CrudPageConfig,
  FieldOption,
} from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';
import { employeeOptions, nameMap, options } from '../../organization/organization-lookups';

const toOptions = (values: readonly string[]): FieldOption[] =>
  values.map((v) => ({ value: v, label: v }));

/** Job openings (vacancies). Status changes go through actions so the rules are always applied. */
@Component({
  selector: 'app-job-openings-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading job openings" />
    }
  `,
})
export class JobOpeningsPageComponent {
  private readonly jobs = inject(JobOpeningDataService);
  private readonly departmentsApi = inject(DepartmentDataService);
  private readonly employeesApi = inject(EmployeeDataService);
  private readonly applicationsApi = inject(ApplicationDataService);
  private readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        departments: this.departmentsApi.getAll(),
        employees: this.employeesApi.getAll(),
        applications: this.applicationsApi.getAll(),
      }),
  });

  private statusAction(
    id: string,
    label: string,
    icon: string,
    from: JobOpeningStatus[],
    to: JobOpeningStatus,
    extra: Partial<CrudExtraAction<JobOpening>> = {},
  ): CrudExtraAction<JobOpening> {
    return {
      id,
      label,
      icon,
      visible: (j) => from.includes(j.status),
      run: (j) => this.jobs.changeStatus(j.id, to),
      successMessage: (j) => `"${j.title}" is now ${to.toLowerCase()}.`,
      ...extra,
    };
  }

  protected readonly config = computed<CrudPageConfig<JobOpening> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const departmentName = nameMap(l.departments, (d) => d.name);
    const applicants = (j: JobOpening) => l.applications.filter((a) => a.jobOpeningId === j.id);
    const today = todayIso();

    return {
      title: 'Job openings',
      description:
        'Vacancies across the company. Publish an opening to start receiving applications.',
      entityLabel: 'Job opening',
      service: this.jobs,
      hasStatus: false,
      searchLabel: 'Search by code or title',
      defaultSort: { active: 'postedOn', direction: 'desc' },
      filters: [
        { key: 'status', label: 'Status', options: toOptions(JOB_OPENING_STATUSES) },
        {
          key: 'departmentId',
          label: 'Department',
          options: options(l.departments, (d) => d.name),
        },
        { key: 'employmentType', label: 'Type', options: toOptions(RECRUITMENT_EMPLOYMENT_TYPES) },
      ],
      nameOf: (j) => j.title,
      canEdit: (j) => j.status !== 'Closed' && j.status !== 'Filled',
      canDelete: (j) => applicants(j).length === 0,
      extraActions: [
        this.statusAction('publish', 'Publish', 'campaign', ['Draft'], 'Open'),
        this.statusAction('hold', 'Put on hold', 'pause_circle', ['Open'], 'On Hold'),
        this.statusAction('resume', 'Resume', 'play_circle', ['On Hold'], 'Open'),
        this.statusAction('reopen', 'Reopen', 'lock_open', ['Closed'], 'Open'),
        this.statusAction(
          'close',
          'Close opening',
          'cancel',
          ['Draft', 'Open', 'On Hold'],
          'Closed',
          {
            destructive: true,
            confirm: {
              title: 'Close job opening?',
              message: (j) =>
                `"${j.title}" will stop accepting applications. You can reopen it later.`,
              confirmText: 'Close opening',
            },
          },
        ),
      ],
      columns: [
        { key: 'code', header: 'Code', sortable: true, nowrap: true, width: '130px' },
        { key: 'title', header: 'Job title', sortable: true },
        {
          key: 'departmentId',
          header: 'Department',
          hideBelow: 'md',
          value: (j) => departmentName(j.departmentId),
        },
        { key: 'employmentType', header: 'Type', hideBelow: 'lg', nowrap: true },
        {
          key: 'vacancies',
          header: 'Vacancies',
          align: 'end',
          hideBelow: 'sm',
          value: (j) =>
            `${applicants(j).filter((a) => a.stage === 'Selected').length} / ${j.vacancies} filled`,
        },
        {
          key: 'applicants',
          header: 'Applicants',
          type: 'number',
          align: 'end',
          hideBelow: 'md',
          value: (j) => applicants(j).length,
        },
        {
          key: 'closingDate',
          header: 'Closes',
          type: 'date',
          sortable: true,
          hideBelow: 'lg',
          nowrap: true,
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({ employmentType: 'Full-time', vacancies: 1, postedOn: today }),
      fields: [
        {
          key: 'title',
          label: 'Job title',
          type: 'text',
          required: true,
          maxLength: 80,
          fullWidth: true,
        },
        {
          key: 'departmentId',
          label: 'Department',
          type: 'select',
          required: true,
          options: () => options(l.departments, (d) => d.name),
        },
        {
          key: 'hiringManagerId',
          label: 'Hiring manager',
          type: 'select',
          required: true,
          options: () => employeeOptions(l.employees.filter((e) => !e.exitDate)),
        },
        {
          key: 'employmentType',
          label: 'Employment type',
          type: 'select',
          required: true,
          options: () => toOptions(RECRUITMENT_EMPLOYMENT_TYPES),
        },
        { key: 'vacancies', label: 'Vacancies', type: 'number', required: true, min: 1, max: 50 },
        { key: 'postedOn', label: 'Posted on', type: 'date', required: true, notInFuture: true },
        { key: 'closingDate', label: 'Closing date', type: 'date', required: true },
        {
          key: 'description',
          label: 'Description',
          type: 'textarea',
          maxLength: 500,
          fullWidth: true,
        },
        { key: 'code', label: 'Code', type: 'text', detailsOnly: true },
        { key: 'status', label: 'Status', type: 'text', detailsOnly: true },
      ],
    };
  });
}
