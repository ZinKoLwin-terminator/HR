import {
  Application,
  ApplicationStage,
  Candidate,
  CandidateSource,
  Interview,
  InterviewMode,
  InterviewRecommendation,
  InterviewRound,
  InterviewStatus,
  JobOpening,
  JobOpeningStatus,
  RecruitmentEmploymentType,
} from '../../core/models/recruitment.models';
import { addDays, dayOfWeek, todayIso } from '../../shared/utilities/date.util';
import { seededDigits, seededInt } from '../mock-random';

/** SAMPLE DATA — fictional people, companies and feedback. Dates are relative to today. */

const today = todayIso();

/** `offset` days from today, moved off the weekend (forwards for the future, backwards for the past). */
function workday(offset: number): string {
  let date = addDays(today, offset);
  const step = offset >= 0 ? 1 : -1;
  while ([0, 6].includes(dayOfWeek(date))) {
    date = addDays(date, step);
  }
  return date;
}

type JobSeed = [
  title: string,
  departmentId: number,
  hiringManagerId: number,
  type: RecruitmentEmploymentType,
  vacancies: number,
  postedDaysAgo: number,
  closesInDays: number,
  status: JobOpeningStatus,
  description: string,
];

const JOB_SEED: JobSeed[] = [
  [
    'Senior Angular Developer',
    4,
    3,
    'Full-time',
    2,
    40,
    20,
    'Open',
    'Build and maintain the HR web applications.',
  ],
  [
    'QA Engineer',
    4,
    3,
    'Full-time',
    1,
    55,
    -5,
    'Filled',
    'Own test automation for the web platform.',
  ],
  [
    'Product Designer',
    5,
    10,
    'Full-time',
    1,
    30,
    30,
    'Open',
    'Design flows and components for the product suite.',
  ],
  [
    'Account Executive',
    6,
    7,
    'Full-time',
    2,
    25,
    15,
    'Open',
    'Grow enterprise accounts across the region.',
  ],
  [
    'Customer Support Agent',
    8,
    26,
    'Full-time',
    3,
    15,
    25,
    'Open',
    'Help customers through chat and email.',
  ],
  ['HR Intern', 2, 2, 'Internship', 1, 20, 40, 'On Hold', 'Support the people operations team.'],
  [
    'Financial Analyst',
    3,
    4,
    'Contract',
    1,
    70,
    -20,
    'Closed',
    'Six-month contract covering budget reporting.',
  ],
  [
    'DevOps Engineer',
    7,
    9,
    'Full-time',
    1,
    3,
    45,
    'Draft',
    'Automate builds, deployments and monitoring.',
  ],
];

export const MOCK_JOB_OPENINGS: JobOpening[] = JOB_SEED.map(
  (
    [
      title,
      departmentId,
      hiringManagerId,
      employmentType,
      vacancies,
      posted,
      closes,
      status,
      description,
    ],
    i,
  ) => {
    const postedOn = addDays(today, -posted);
    return {
      id: i + 1,
      code: `JOB-${postedOn.slice(0, 4)}-${String(i + 1).padStart(3, '0')}`,
      title,
      departmentId,
      hiringManagerId,
      employmentType,
      vacancies,
      postedOn,
      closingDate: addDays(today, closes),
      description,
      status,
    };
  },
);

type CandidateSeed = [
  firstName: string,
  lastName: string,
  source: CandidateSource,
  employer: string,
  years: number,
];

const CANDIDATE_SEED: CandidateSeed[] = [
  ['Mia', 'Hartley', 'LinkedIn', 'Northwind Labs', 6],
  ['Noah', 'Okafor', 'Career site', 'Blue Harbor Software', 4],
  ['Lena', 'Vogel', 'Job board', 'Pixel & Co', 3],
  ['Arjun', 'Mehta', 'Job board', 'Techbridge', 1],
  ['Sofia', 'Marin', 'Referral', 'Cloudline', 7],
  ['Daniel', 'Ruiz', 'Career site', 'Quanta Systems', 5],
  ['Hana', 'Sato', 'LinkedIn', 'Orbit Testing', 2],
  ['Omar', 'Haddad', 'Agency', 'Studio Nine', 5],
  ['Grace', 'Liu', 'Referral', 'Mosaic Design', 4],
  ['Ethan', 'Brooks', 'Career site', 'Freelance', 2],
  ['Priya', 'Nair', 'Job board', 'Lumen Creative', 3],
  ['Lucas', 'Meyer', 'LinkedIn', 'Redwood Trading', 6],
  ['Amara', 'Diallo', 'Career site', 'Sunrise Retail', 2],
  ['Tomas', 'Novak', 'Job board', 'Apex Distribution', 4],
  ['Chloe', 'Bennett', 'Referral', 'Vertex Sales', 5],
  ['Ravi', 'Kumar', 'Job board', 'HelpDesk Pro', 1],
  ['Emma', 'Larsson', 'Career site', 'CallPoint', 2],
  ['Yusuf', 'Aydin', 'Job board', 'ServiceNest', 0],
  ['Isabel', 'Costa', 'LinkedIn', 'Fresh Start Cafe', 1],
  ['Kai', 'Tanaka', 'Career site', 'Student', 0],
  ['Naomi', 'Reed', 'Agency', 'Ledger & Partners', 6],
  ['Victor', 'Petrov', 'LinkedIn', 'Summit Advisory', 3],
];

export const MOCK_CANDIDATES: Candidate[] = CANDIDATE_SEED.map(
  ([firstName, lastName, source, currentEmployer, experienceYears], i) => ({
    id: i + 1,
    firstName,
    lastName,
    email: `${firstName}.${lastName}@example.com`.toLowerCase(),
    phone: `+95 9 ${seededDigits(`cand-phone-a:${i}`, 3)} ${seededDigits(`cand-phone-b:${i}`, 5)}`,
    source,
    currentEmployer,
    experienceYears,
    resumeName: `${firstName}_${lastName}_CV.pdf`,
    notes: '',
  }),
);

// jobOpeningId, candidateId, stage, daysAgo, rejection reason
type ApplicationSeed = [number, number, ApplicationStage, number, string | null];
const APPLICATION_SEED: ApplicationSeed[] = [
  [1, 1, 'Interview', 30, null],
  [1, 2, 'Screening', 22, null],
  [1, 3, 'Applied', 5, null],
  [1, 4, 'Rejected', 33, 'Not enough Angular experience for the senior role.'],
  [1, 5, 'Interview', 18, null],
  [2, 6, 'Selected', 48, null],
  [2, 7, 'Rejected', 44, 'Test automation skills did not match the requirements.'],
  [3, 8, 'Interview', 14, null],
  [3, 9, 'Screening', 10, null],
  [3, 10, 'Applied', 4, null],
  [3, 11, 'Rejected', 12, 'Portfolio did not fit the product style.'],
  [4, 12, 'Screening', 12, null],
  [4, 13, 'Applied', 6, null],
  [4, 14, 'Applied', 3, null],
  [4, 15, 'Interview', 11, null],
  [5, 16, 'Applied', 5, null],
  [5, 17, 'Screening', 7, null],
  [5, 18, 'Applied', 2, null],
  [5, 19, 'Rejected', 9, 'Not available for the required shifts.'],
  [6, 20, 'Screening', 8, null],
  [7, 21, 'Selected', 60, null],
  [7, 22, 'Rejected', 58, 'Asked for compensation above the contract budget.'],
];

export const MOCK_APPLICATIONS: Application[] = APPLICATION_SEED.map(
  ([jobOpeningId, candidateId, stage, daysAgo, rejectionReason], i) => ({
    id: i + 1,
    jobOpeningId,
    candidateId,
    stage,
    appliedOn: addDays(today, -daysAgo),
    rejectionReason,
    notes: '',
  }),
);

// applicationId, round, dayOffset, start, minutes, mode, interviewerId, status, score, recommendation, feedback
type InterviewSeed = [
  number,
  InterviewRound,
  number,
  string,
  number,
  InterviewMode,
  number,
  InterviewStatus,
  number | null,
  InterviewRecommendation | null,
  string,
];
const INTERVIEW_SEED: InterviewSeed[] = [
  [
    1,
    'Phone screen',
    -18,
    '10:00',
    30,
    'Phone',
    3,
    'Completed',
    4,
    'Hire',
    'Clear communicator with solid Angular basics.',
  ],
  [
    1,
    'Technical',
    -10,
    '14:00',
    60,
    'Video',
    3,
    'Completed',
    4,
    'Hire',
    'Good RxJS and signals knowledge.',
  ],
  [1, 'Final', 4, '11:00', 45, 'Onsite', 2, 'Scheduled', null, null, ''],
  [
    4,
    'Phone screen',
    -30,
    '09:30',
    30,
    'Phone',
    3,
    'Completed',
    2,
    'No hire',
    'Limited framework experience.',
  ],
  [
    5,
    'Phone screen',
    -12,
    '15:00',
    30,
    'Phone',
    3,
    'Completed',
    4,
    'Hire',
    'Strong background in large applications.',
  ],
  [5, 'Technical', 2, '10:00', 60, 'Video', 9, 'Scheduled', null, null, ''],
  [
    6,
    'Technical',
    -35,
    '10:00',
    60,
    'Onsite',
    3,
    'Completed',
    5,
    'Strong hire',
    'Excellent test design skills.',
  ],
  [6, 'HR', -28, '11:00', 45, 'Onsite', 2, 'Completed', 4, 'Hire', 'Good team fit.'],
  [
    8,
    'Phone screen',
    -8,
    '13:00',
    30,
    'Phone',
    10,
    'Completed',
    4,
    'Hire',
    'Thoughtful about user research.',
  ],
  [8, 'Technical', 3, '14:00', 60, 'Video', 10, 'Scheduled', null, null, ''],
  [
    11,
    'Phone screen',
    -6,
    '16:00',
    30,
    'Phone',
    10,
    'Cancelled',
    null,
    null,
    'Candidate withdrew availability.',
  ],
  [
    15,
    'Phone screen',
    -5,
    '10:30',
    30,
    'Phone',
    7,
    'Completed',
    4,
    'Hire',
    'Strong track record in enterprise sales.',
  ],
  [15, 'Final', 1, '13:30', 60, 'Onsite', 7, 'Scheduled', null, null, ''],
  [
    21,
    'Technical',
    -50,
    '10:00',
    60,
    'Video',
    4,
    'Completed',
    4,
    'Hire',
    'Strong budgeting knowledge.',
  ],
];

export const MOCK_INTERVIEWS: Interview[] = INTERVIEW_SEED.map(
  (
    [
      applicationId,
      round,
      offset,
      startTime,
      durationMinutes,
      mode,
      interviewerId,
      status,
      score,
      recommendation,
      feedback,
    ],
    i,
  ) => ({
    id: i + 1,
    applicationId,
    round,
    date: workday(offset),
    startTime,
    durationMinutes,
    mode,
    interviewerId,
    location:
      mode === 'Onsite'
        ? `Meeting room ${seededInt(`room:${i}`, 1, 4)}`
        : mode === 'Video'
          ? 'Video call (sample link)'
          : 'Phone call',
    status,
    score,
    recommendation,
    feedback,
  }),
);
