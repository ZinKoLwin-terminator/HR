import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin, map } from 'rxjs';
import {
  INTERVIEW_MODES,
  INTERVIEW_RECOMMENDATIONS,
  INTERVIEW_ROUNDS,
  Interview,
  candidateName,
} from '../../../core/models/recruitment.models';
import { EmployeeLookupDataService } from '../../../core/services/data/employee-data.service';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../../core/services/data/recruitment-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig, FieldOption } from '../../../shared/components/crud/crud.models';
import { todayIso } from '../../../shared/utilities/date.util';

const toOptions = (values: readonly string[]): FieldOption[] =>
  values.map((v) => ({ value: v, label: v }));
const INTERVIEWER_PICKER_SIZE = 200;
const STATUS_OPTIONS = toOptions(['Scheduled', 'Completed', 'Cancelled']);

/**
 * Interview schedule and feedback. To record feedback, edit the interview, set it to Completed and
 * enter a score and recommendation.
 */
@Component({
  selector: 'app-interviews-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading interviews" />
    }
  `,
})
export class InterviewsPageComponent {
  private readonly interviews = inject(InterviewDataService);
  private readonly applicationsApi = inject(ApplicationDataService);
  private readonly candidatesApi = inject(CandidateDataService);
  private readonly jobsApi = inject(JobOpeningDataService);
  // Interviewers come from the non-personal lookup (current employees), so a recruiter without
  // access to the employee module can still schedule interviews.
  private readonly employeeLookup = inject(EmployeeLookupDataService);
  private readonly lookups = rxResource({
    stream: () =>
      forkJoin({
        applications: this.applicationsApi.getAll(),
        candidates: this.candidatesApi.getAll(),
        jobs: this.jobsApi.getAll(),
        employees: this.employeeLookup
          .query({ pageIndex: 0, pageSize: INTERVIEWER_PICKER_SIZE })
          .pipe(map((page) => page.items)),
      }),
  });

  protected readonly config = computed<CrudPageConfig<Interview> | null>(() => {
    const l = this.lookups.value();
    if (!l) {
      return null;
    }
    const people = new Map(l.employees.map((e) => [e.id, e.displayName]));
    const interviewerOptions: FieldOption[] = l.employees.map((e) => ({
      value: e.id,
      label: `${e.displayName} (${e.employeeCode})`,
    }));
    const candidates = new Map(l.candidates.map((c) => [c.id, candidateName(c)]));
    const jobs = new Map(l.jobs.map((j) => [j.id, j.title]));
    const appLabel = (id: number) => {
      const a = l.applications.find((x) => x.id === id);
      return a
        ? `${candidates.get(a.candidateId) ?? '—'} — ${jobs.get(a.jobOpeningId) ?? '—'}`
        : `#${id}`;
    };
    const today = todayIso();

    return {
      title: 'Interviews',
      description:
        'Schedule interviews and record feedback. Scheduling moves a screened candidate to the Interview stage.',
      entityLabel: 'Interview',
      service: this.interviews,
      hasStatus: false,
      searchLabel: 'Search location or feedback',
      defaultSort: { active: 'date', direction: 'desc' },
      filters: [
        { key: 'status', label: 'Status', options: STATUS_OPTIONS },
        { key: 'interviewerId', label: 'Interviewer', options: interviewerOptions },
      ],
      dateFilter: {
        fromKey: 'dateFrom',
        toKey: 'dateTo',
        fromLabel: 'From date',
        toLabel: 'To date',
      },
      nameOf: (i) => appLabel(i.applicationId),
      canEdit: (i) => i.status !== 'Cancelled',
      canDelete: (i) => i.status !== 'Completed',
      extraActions: [
        {
          id: 'cancel',
          label: 'Cancel interview',
          icon: 'event_busy',
          destructive: true,
          visible: (i) => i.status === 'Scheduled',
          confirm: {
            title: 'Cancel interview?',
            message: (i) =>
              `The ${i.round.toLowerCase()} interview on ${i.date} will be cancelled.`,
            confirmText: 'Cancel interview',
          },
          run: (i) => this.interviews.update(i.id, { status: 'Cancelled' }),
          successMessage: () => 'Interview cancelled.',
        },
      ],
      columns: [
        { key: 'date', header: 'Date', type: 'date', sortable: true, nowrap: true },
        { key: 'startTime', header: 'Time', nowrap: true, hideBelow: 'sm' },
        {
          key: 'applicationId',
          header: 'Candidate · Job',
          value: (i) => appLabel(i.applicationId),
        },
        { key: 'round', header: 'Round', sortable: true, hideBelow: 'md', nowrap: true },
        {
          key: 'interviewerId',
          header: 'Interviewer',
          hideBelow: 'lg',
          value: (i) => people.get(i.interviewerId) ?? '—',
        },
        { key: 'mode', header: 'Mode', hideBelow: 'lg' },
        {
          key: 'score',
          header: 'Score',
          align: 'end',
          hideBelow: 'md',
          value: (i) => (i.score === null ? '—' : `${i.score} / 5`),
        },
        { key: 'status', header: 'Status', type: 'status', sortable: true },
      ],
      newItem: () => ({
        round: 'Phone screen',
        mode: 'Video',
        date: today,
        startTime: '10:00',
        durationMinutes: 45,
        status: 'Scheduled',
      }),
      fields: [
        {
          key: 'applicationId',
          label: 'Application',
          type: 'select',
          required: true,
          fullWidth: true,
          lockedOnEdit: true,
          hint: 'Only candidates in Screening or Interview can be scheduled',
          options: (_values, ctx) =>
            l.applications
              .filter((a) => a.stage === 'Screening' || a.stage === 'Interview' || ctx.id !== null)
              .map((a) => ({ value: a.id, label: appLabel(a.id) }))
              .sort((a, b) => a.label.localeCompare(b.label)),
        },
        {
          key: 'round',
          label: 'Round',
          type: 'select',
          required: true,
          options: () => toOptions(INTERVIEW_ROUNDS),
        },
        {
          key: 'interviewerId',
          label: 'Interviewer',
          type: 'select',
          required: true,
          options: () => interviewerOptions,
        },
        { key: 'date', label: 'Date', type: 'date', required: true },
        { key: 'startTime', label: 'Start time', type: 'time', required: true },
        {
          key: 'durationMinutes',
          label: 'Duration (minutes)',
          type: 'number',
          required: true,
          min: 15,
          max: 240,
        },
        {
          key: 'mode',
          label: 'Mode',
          type: 'select',
          required: true,
          options: () => toOptions(INTERVIEW_MODES),
        },
        { key: 'location', label: 'Room or link', type: 'text', maxLength: 120 },
        {
          key: 'status',
          label: 'Status',
          type: 'select',
          required: true,
          options: () => STATUS_OPTIONS,
        },
        {
          key: 'score',
          label: 'Score (1–5)',
          type: 'number',
          min: 1,
          max: 5,
          hint: 'Completed interviews only',
        },
        {
          key: 'recommendation',
          label: 'Recommendation',
          type: 'select',
          nullable: true,
          options: () => toOptions(INTERVIEW_RECOMMENDATIONS),
        },
        { key: 'feedback', label: 'Feedback', type: 'textarea', maxLength: 500, fullWidth: true },
      ],
    };
  });
}
