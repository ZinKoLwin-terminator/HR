import { Injectable, Injector, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { CreateModel } from '../../core/models/common.models';
import {
  Application,
  ApplicationFilter,
  ApplicationStage,
  Candidate,
  Interview,
  InterviewFilter,
  JobOpening,
  JobOpeningStatus,
  STAGE_TRANSITIONS,
  candidateName,
} from '../../core/models/recruitment.models';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../../core/services/data/recruitment-data.services';
import {
  MOCK_APPLICATIONS,
  MOCK_CANDIDATES,
  MOCK_INTERVIEWS,
  MOCK_JOB_OPENINGS,
} from '../../mock-data/recruitment/recruitment.mock';
import { matchesEqualityFilters } from '../../shared/utilities/query.util';
import { isDateInRange, timeToMinutes, todayIso } from '../../shared/utilities/date.util';
import { DepartmentMockService } from '../organization/organization-mock.services';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { InMemoryCrudService } from '../in-memory-crud.service';

@Injectable({ providedIn: 'root' })
export class JobOpeningMockService
  extends InMemoryCrudService<JobOpening>
  implements JobOpeningDataService
{
  private readonly injector = inject(Injector);
  private readonly departments = inject(DepartmentMockService);
  private readonly employees = inject(EmployeeMockService);
  protected readonly entityLabel = 'Job opening';
  protected readonly activityCategory = 'recruitment';
  protected override readonly searchFields = ['code', 'title'] as const;

  constructor() {
    super(MOCK_JOB_OPENINGS);
  }

  private get applications(): ApplicationMockService {
    return this.injector.get(ApplicationMockService);
  }

  protected override describe(entity: JobOpening): string {
    return `${entity.code} ${entity.title}`;
  }

  protected override prepare(candidate: JobOpening, isNew: boolean): void {
    candidate.vacancies = Number(candidate.vacancies);
    if (isNew) {
      candidate.status = 'Draft';
      candidate.postedOn ||= todayIso();
      candidate.code = `JOB-${candidate.postedOn.slice(0, 4)}-${String(candidate.id).padStart(3, '0')}`;
    }
  }

  protected override validate(candidate: JobOpening, isNew: boolean): void {
    if (!isNew) {
      const stored = this.findById(candidate.id);
      if (stored && (stored.status === 'Closed' || stored.status === 'Filled')) {
        this.conflictError(`A ${stored.status.toLowerCase()} job opening cannot be edited.`);
      }
    }
    if (!this.departments.findById(candidate.departmentId)) {
      this.validationError('Choose a department.', { departmentId: 'Choose a department.' });
    }
    const manager = this.employees.findById(candidate.hiringManagerId);
    if (!manager || manager.exitDate) {
      this.validationError('Choose a current employee as hiring manager.', {
        hiringManagerId: 'Choose a current employee.',
      });
    }
    if (!(candidate.vacancies >= 1 && candidate.vacancies <= 50)) {
      this.validationError('Vacancies must be between 1 and 50.', {
        vacancies: 'Enter 1 to 50.',
      });
    }
    const selected = this.applications.countSelected(candidate.id);
    if (!isNew && candidate.vacancies < selected) {
      this.validationError(`${selected} candidate(s) are already selected for this job.`, {
        vacancies: `At least ${selected} (already selected).`,
      });
    }
    if (candidate.closingDate < candidate.postedOn) {
      this.validationError('The closing date cannot be before the posting date.', {
        closingDate: 'Must be on or after the posting date.',
      });
    }
  }

  protected override beforeDelete(entity: JobOpening): void {
    this.ensureNotReferenced(this.describe(entity), [
      [
        'application(s)',
        this.applications.records().filter((a) => a.jobOpeningId === entity.id).length,
      ],
    ]);
  }

  changeStatus(id: number, status: JobOpeningStatus): Observable<JobOpening> {
    return this.respond(() => {
      const job = this.require(id);
      const allowed: Record<JobOpeningStatus, readonly JobOpeningStatus[]> = {
        Draft: ['Open', 'Closed'],
        Open: ['On Hold', 'Closed'],
        'On Hold': ['Open', 'Closed'],
        Closed: ['Open'],
        Filled: [],
      };
      if (!allowed[job.status].includes(status)) {
        this.conflictError(`A ${job.status.toLowerCase()} job opening cannot be set to ${status}.`);
      }
      if (status === 'Open') {
        if (job.closingDate < todayIso()) {
          this.conflictError('The closing date has passed. Edit the opening before reopening it.');
        }
        if (this.applications.countSelected(id) >= job.vacancies) {
          this.conflictError('All vacancies already have a selected candidate.');
        }
      }
      const updated = { ...job, status };
      this.store.update((items) => items.map((j) => (j.id === id ? updated : j)));
      this.activity.record('recruitment', `Job opening "${this.describe(job)}" set to ${status}`);
      return updated;
    });
  }

  /** Marks an opening Filled once enough candidates are selected (and reopens it if one drops out). */
  syncFilled(id: number): void {
    const job = this.findById(id);
    if (!job) return;
    const full = this.applications.countSelected(id) >= job.vacancies;
    const next: JobOpeningStatus | null =
      full && (job.status === 'Open' || job.status === 'On Hold')
        ? 'Filled'
        : !full && job.status === 'Filled'
          ? 'Open'
          : null;
    if (next) {
      this.store.update((items) => items.map((j) => (j.id === id ? { ...j, status: next } : j)));
      this.activity.record(
        'recruitment',
        next === 'Filled'
          ? `Job opening "${this.describe(job)}" filled`
          : `Job opening "${this.describe(job)}" reopened`,
      );
    }
  }
}

@Injectable({ providedIn: 'root' })
export class CandidateMockService
  extends InMemoryCrudService<Candidate>
  implements CandidateDataService
{
  private readonly injector = inject(Injector);
  protected readonly entityLabel = 'Candidate';
  protected readonly activityCategory = 'recruitment';
  protected override readonly searchFields = [
    'firstName',
    'lastName',
    'email',
    'currentEmployer',
  ] as const;

  constructor() {
    super(MOCK_CANDIDATES);
  }

  protected override describe(entity: Candidate): string {
    return candidateName(entity);
  }

  protected override prepare(candidate: Candidate): void {
    candidate.experienceYears = Number(candidate.experienceYears);
    candidate.email = candidate.email.trim().toLowerCase();
  }

  protected override validate(candidate: Candidate): void {
    this.ensureUnique(candidate, 'email', 'Email');
    if (!(candidate.experienceYears >= 0 && candidate.experienceYears <= 50)) {
      this.validationError('Experience must be between 0 and 50 years.', {
        experienceYears: 'Enter 0 to 50.',
      });
    }
  }

  protected override beforeDelete(entity: Candidate): void {
    const applications = this.injector
      .get(ApplicationMockService)
      .records()
      .filter((a) => a.candidateId === entity.id).length;
    this.ensureNotReferenced(candidateName(entity), [['application(s)', applications]]);
  }
}

@Injectable({ providedIn: 'root' })
export class ApplicationMockService
  extends InMemoryCrudService<Application, Application, ApplicationFilter>
  implements ApplicationDataService
{
  private readonly injector = inject(Injector);
  private readonly candidates = inject(CandidateMockService);
  protected readonly entityLabel = 'Application';
  protected readonly activityCategory = 'recruitment';

  constructor() {
    super(MOCK_APPLICATIONS);
  }

  private get jobs(): JobOpeningMockService {
    return this.injector.get(JobOpeningMockService);
  }
  private get interviews(): InterviewMockService {
    return this.injector.get(InterviewMockService);
  }

  protected override describe(entity: Application): string {
    return this.label(entity);
  }

  /** "Candidate for Job title" — shared with the interview service for messages. */
  label(entity: Application): string {
    const candidate = this.candidates.findById(entity.candidateId);
    const job = this.jobs.findById(entity.jobOpeningId);
    return `${candidate ? candidateName(candidate) : `candidate #${entity.candidateId}`} for ${job?.title ?? `job #${entity.jobOpeningId}`}`;
  }

  countSelected(jobOpeningId: number): number {
    return this.store().filter((a) => a.jobOpeningId === jobOpeningId && a.stage === 'Selected')
      .length;
  }

  protected override prepare(candidate: Application, isNew: boolean): void {
    if (isNew) {
      candidate.stage = 'Applied';
      candidate.rejectionReason = null;
      candidate.appliedOn ||= todayIso();
    }
  }

  protected override validate(candidate: Application, isNew: boolean): void {
    const job = this.jobs.findById(candidate.jobOpeningId);
    if (!job) {
      this.validationError('Choose a job opening.', { jobOpeningId: 'Choose a job opening.' });
    }
    if (!this.candidates.findById(candidate.candidateId)) {
      this.validationError('Choose a candidate.', { candidateId: 'Choose a candidate.' });
    }
    if (isNew && job.status !== 'Open') {
      this.validationError(
        `"${job.title}" is ${job.status.toLowerCase()} and not accepting applications.`,
        {
          jobOpeningId: `This opening is ${job.status.toLowerCase()}.`,
        },
      );
    }
    if (!isNew) {
      const stored = this.findById(candidate.id);
      if (
        stored &&
        (stored.jobOpeningId !== candidate.jobOpeningId ||
          stored.candidateId !== candidate.candidateId)
      ) {
        this.validationError('The job opening and candidate cannot be changed.');
      }
    }
    const duplicate = this.store().some(
      (a) =>
        a.id !== candidate.id &&
        a.jobOpeningId === candidate.jobOpeningId &&
        a.candidateId === candidate.candidateId,
    );
    if (duplicate) {
      this.validationError('This candidate has already applied for this job.', {
        candidateId: 'Already applied for this opening.',
      });
    }
  }

  protected override beforeDelete(entity: Application): void {
    if (entity.stage === 'Selected') {
      this.conflictError('A selected application cannot be deleted. Reject it first.');
    }
    this.ensureNotReferenced(this.describe(entity), [
      [
        'interview(s)',
        this.interviews.records().filter((i) => i.applicationId === entity.id).length,
      ],
    ]);
  }

  private replace(id: number, changes: Partial<Application>): Application {
    const updated = { ...this.require(id), ...changes };
    this.store.update((items) => items.map((a) => (a.id === id ? updated : a)));
    return updated;
  }

  moveStage(id: number, stage: ApplicationStage, reason = ''): Observable<Application> {
    return this.respond(() => {
      const app = this.require(id);
      if (!STAGE_TRANSITIONS[app.stage].includes(stage)) {
        this.conflictError(`An application at "${app.stage}" cannot move to "${stage}".`);
      }
      const job = this.jobs.findById(app.jobOpeningId)!;
      if (stage === 'Rejected') {
        if (!reason.trim()) {
          this.validationError('Enter a reason for rejecting this application.', {
            rejectionReason: 'A reason is required.',
          });
        }
      } else {
        if (job.status !== 'Open' && job.status !== 'On Hold') {
          this.conflictError(
            `"${job.title}" is ${job.status.toLowerCase()}, so applications cannot move forward.`,
          );
        }
        if (stage === 'Selected') {
          const positive = this.interviews
            .records()
            .some(
              (i) =>
                i.applicationId === id &&
                i.status === 'Completed' &&
                (i.recommendation === 'Hire' || i.recommendation === 'Strong hire'),
            );
          if (!positive) {
            this.conflictError(
              'Selecting a candidate needs a completed interview with a "Hire" recommendation.',
            );
          }
          if (this.countSelected(job.id) >= job.vacancies) {
            this.conflictError(
              `All ${job.vacancies} vacancies for "${job.title}" already have a selected candidate.`,
            );
          }
        }
      }
      const updated = this.replace(id, {
        stage,
        rejectionReason: stage === 'Rejected' ? reason.trim() : null,
      });
      if (stage === 'Rejected') {
        this.interviews.cancelForApplication(id, 'Cancelled: the application was rejected.');
      }
      this.jobs.syncFilled(job.id);
      this.activity.record('recruitment', `${this.describe(app)} moved to ${stage}`);
      return updated;
    });
  }

  /** Scheduling an interview moves a screened application to the Interview stage. */
  advanceToInterview(id: number): void {
    const app = this.findById(id);
    if (app?.stage === 'Screening') {
      this.replace(id, { stage: 'Interview' });
    }
  }
}

@Injectable({ providedIn: 'root' })
export class InterviewMockService
  extends InMemoryCrudService<Interview, Interview, InterviewFilter>
  implements InterviewDataService
{
  private readonly applications = inject(ApplicationMockService);
  private readonly employees = inject(EmployeeMockService);
  protected readonly entityLabel = 'Interview';
  protected readonly activityCategory = 'recruitment';
  protected override readonly searchFields = ['location', 'feedback'] as const;

  constructor() {
    super(MOCK_INTERVIEWS);
  }

  protected override describe(entity: Interview): string {
    const app = this.applications.findById(entity.applicationId);
    return `${entity.round} · ${app ? this.applications.label(app) : `application #${entity.applicationId}`}`;
  }

  protected override matchesFilters(item: Interview, filters: Partial<InterviewFilter>): boolean {
    const { dateFrom, dateTo, ...rest } = filters;
    return matchesEqualityFilters(item, rest) && isDateInRange(item.date, dateFrom, dateTo);
  }

  protected override prepare(candidate: Interview): void {
    candidate.durationMinutes = Number(candidate.durationMinutes);
    const rawScore = candidate.score as unknown;
    candidate.score =
      rawScore === null || rawScore === undefined || rawScore === '' ? null : Number(rawScore);
    candidate.recommendation = candidate.recommendation || null;
    candidate.feedback ??= '';
  }

  protected override validate(candidate: Interview, isNew: boolean): void {
    const today = todayIso();
    const stored = isNew ? undefined : this.findById(candidate.id);
    if (stored?.status === 'Cancelled') {
      this.conflictError('A cancelled interview cannot be changed. Schedule a new one instead.');
    }
    const app = this.applications.findById(candidate.applicationId);
    if (!app) {
      this.validationError('Choose an application.', { applicationId: 'Choose an application.' });
    }
    const interviewer = this.employees.findById(candidate.interviewerId);
    if (!interviewer || interviewer.exitDate) {
      this.validationError('Choose a current employee as interviewer.', {
        interviewerId: 'Choose a current employee.',
      });
    }
    if (!(candidate.durationMinutes >= 15 && candidate.durationMinutes <= 240)) {
      this.validationError('The duration must be between 15 and 240 minutes.', {
        durationMinutes: 'Enter 15 to 240 minutes.',
      });
    }

    if (candidate.status === 'Scheduled') {
      if (isNew && app.stage !== 'Screening' && app.stage !== 'Interview') {
        this.validationError(
          `Interviews can be scheduled for candidates in Screening or Interview (this one is ${app.stage}).`,
          { applicationId: `Application is at "${app.stage}".` },
        );
      }
      const changedSlot =
        !stored || stored.date !== candidate.date || stored.startTime !== candidate.startTime;
      if (changedSlot && candidate.date < today) {
        this.validationError('A scheduled interview cannot be in the past.', {
          date: 'Choose today or a later date.',
        });
      }
      this.ensureInterviewerFree(candidate);
    }
    if (candidate.status === 'Completed') {
      if (candidate.date > today) {
        this.validationError('A future interview cannot be marked completed.', {
          status: 'The interview date is in the future.',
        });
      }
      const score = candidate.score ?? 0;
      if (!Number.isInteger(score) || score < 1 || score > 5) {
        this.validationError('A completed interview needs a score from 1 to 5.', {
          score: 'Enter a score from 1 to 5.',
        });
      }
      if (!candidate.recommendation) {
        this.validationError('A completed interview needs a recommendation.', {
          recommendation: 'Choose a recommendation.',
        });
      }
    } else if (candidate.score !== null || candidate.recommendation !== null) {
      this.validationError('Only completed interviews can have a score or recommendation.', {
        score: 'Complete the interview first.',
      });
    }
  }

  private ensureInterviewerFree(candidate: Interview): void {
    const start = timeToMinutes(candidate.startTime);
    const end = start + candidate.durationMinutes;
    const clash = this.store().find(
      (i) =>
        i.id !== candidate.id &&
        i.status === 'Scheduled' &&
        i.interviewerId === candidate.interviewerId &&
        i.date === candidate.date &&
        timeToMinutes(i.startTime) < end &&
        start < timeToMinutes(i.startTime) + i.durationMinutes,
    );
    if (clash) {
      this.validationError(
        `The interviewer already has an interview at ${clash.startTime} on that day.`,
        {
          startTime: `Interviewer is busy from ${clash.startTime}.`,
        },
      );
    }
  }

  protected override beforeDelete(entity: Interview): void {
    if (entity.status === 'Completed') {
      this.conflictError(
        'A completed interview is part of the hiring record and cannot be deleted.',
      );
    }
  }

  override create(model: CreateModel<Interview>): Observable<Interview> {
    return super
      .create(model)
      .pipe(tap((i) => this.applications.advanceToInterview(i.applicationId)));
  }

  /** Cancels scheduled interviews (used when an application is rejected). */
  cancelForApplication(applicationId: number, note: string): void {
    this.store.update((items) =>
      items.map((i) =>
        i.applicationId === applicationId && i.status === 'Scheduled'
          ? { ...i, status: 'Cancelled', feedback: note }
          : i,
      ),
    );
  }
}
