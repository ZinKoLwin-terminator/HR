import { Entity } from './common.models';

export type JobOpeningStatus = 'Draft' | 'Open' | 'On Hold' | 'Closed' | 'Filled';
export const JOB_OPENING_STATUSES: readonly JobOpeningStatus[] = [
  'Draft',
  'Open',
  'On Hold',
  'Closed',
  'Filled',
];

export type RecruitmentEmploymentType = 'Full-time' | 'Part-time' | 'Contract' | 'Internship';
export const RECRUITMENT_EMPLOYMENT_TYPES: readonly RecruitmentEmploymentType[] = [
  'Full-time',
  'Part-time',
  'Contract',
  'Internship',
];

export interface JobOpening extends Entity {
  /** Generated, e.g. JOB-2026-004. */
  code: string;
  title: string;
  departmentId: number;
  hiringManagerId: number;
  employmentType: RecruitmentEmploymentType;
  vacancies: number;
  postedOn: string;
  closingDate: string;
  description: string;
  status: JobOpeningStatus;
}

export type CandidateSource = 'Career site' | 'Referral' | 'Job board' | 'LinkedIn' | 'Agency';
export const CANDIDATE_SOURCES: readonly CandidateSource[] = [
  'Career site',
  'Referral',
  'Job board',
  'LinkedIn',
  'Agency',
];

export interface Candidate extends Entity {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  source: CandidateSource;
  currentEmployer: string;
  experienceYears: number;
  /** Demo: only the file name is kept. */
  resumeName: string | null;
  notes: string;
}

export const candidateName = (c: Pick<Candidate, 'firstName' | 'lastName'>): string =>
  `${c.firstName} ${c.lastName}`;

export type ApplicationStage = 'Applied' | 'Screening' | 'Interview' | 'Selected' | 'Rejected';
export const APPLICATION_STAGES: readonly ApplicationStage[] = [
  'Applied',
  'Screening',
  'Interview',
  'Selected',
  'Rejected',
];

/** Stages an application may move to from each stage. */
export const STAGE_TRANSITIONS: Record<ApplicationStage, readonly ApplicationStage[]> = {
  Applied: ['Screening', 'Rejected'],
  Screening: ['Interview', 'Rejected'],
  Interview: ['Selected', 'Rejected'],
  Selected: ['Rejected'],
  Rejected: [],
};

export interface Application extends Entity {
  jobOpeningId: number;
  candidateId: number;
  stage: ApplicationStage;
  appliedOn: string;
  /** Set when the stage is Rejected. */
  rejectionReason: string | null;
  notes: string;
}

export type InterviewRound = 'Phone screen' | 'Technical' | 'HR' | 'Final';
export const INTERVIEW_ROUNDS: readonly InterviewRound[] = [
  'Phone screen',
  'Technical',
  'HR',
  'Final',
];
export type InterviewMode = 'Onsite' | 'Video' | 'Phone';
export const INTERVIEW_MODES: readonly InterviewMode[] = ['Onsite', 'Video', 'Phone'];
export type InterviewStatus = 'Scheduled' | 'Completed' | 'Cancelled';
export type InterviewRecommendation = 'Strong hire' | 'Hire' | 'No hire';
export const INTERVIEW_RECOMMENDATIONS: readonly InterviewRecommendation[] = [
  'Strong hire',
  'Hire',
  'No hire',
];

export interface Interview extends Entity {
  applicationId: number;
  round: InterviewRound;
  date: string;
  startTime: string;
  durationMinutes: number;
  mode: InterviewMode;
  interviewerId: number;
  /** Room or meeting link (sample text only). */
  location: string;
  status: InterviewStatus;
  /** 1–5, required once completed. */
  score: number | null;
  recommendation: InterviewRecommendation | null;
  feedback: string;
}

export interface InterviewFilter {
  status: InterviewStatus;
  interviewerId: number;
  applicationId: number;
  dateFrom: string;
  dateTo: string;
}

export interface ApplicationFilter {
  jobOpeningId: number;
  candidateId: number;
  stage: ApplicationStage;
}
