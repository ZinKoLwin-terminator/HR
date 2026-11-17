import {
  ReviewAction,
  ReviewActionName,
  ReviewStep,
  ScorecardItem,
  ScorecardStatus,
} from '../../core/models/kpi.models';
import { fullName } from '../../core/models/employee.models';
import {
  kpiAchievement,
  kpiRatingLabel,
  kpiWeightedScore,
  scoreScorecard,
} from '../../core/utilities/kpi-calculation';
import { addDays, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { MOCK_USERS } from '../settings/settings.mock';
import { MOCK_EVALUATION_PERIODS } from './evaluation-periods.mock';
import { MOCK_KPI_CATEGORIES } from './kpi-categories.mock';
import { ScorecardRecord } from './kpi-records';
import { MOCK_KPI_TEMPLATES } from './kpi-templates.mock';
import { DEFAULT_RATING_BANDS } from './rating-bands.mock';

/**
 * SAMPLE DATA - fictional scorecards for the seeded employees, in several statuses. The reporting
 * line is the employees' `managerId` (EMP005 Daniel Carter manages EMP046 and EMP017 and EMP014).
 * Figures come from the same calculator the mock services use, so they reconcile.
 *
 * Template ids: 1 OUTPUT-TASKS, 2 DEFECT-RATE, 3 ATTENDANCE-RATE, 4 BUDGET-ADHERENCE,
 * 5 CUSTOMER-RATING, 6 TRAINING-DONE, 7 RESPONSE-HOURS, 8 ENG-REVIEW-HOURS.
 */

type PlanLine = [templateId: number, weight: number];

/** Engineering plan (weights add up to 100). */
const PLAN_ENGINEERING: PlanLine[] = [
  [8, 30],
  [1, 30],
  [2, 20],
  [6, 10],
  [3, 10],
];
/** General plan. */
const PLAN_GENERAL: PlanLine[] = [
  [1, 40],
  [5, 25],
  [7, 20],
  [6, 15],
];
/** Management / support plan. */
const PLAN_SUPPORT: PlanLine[] = [
  [3, 20],
  [4, 30],
  [5, 30],
  [6, 20],
];

interface CardSeed {
  employeeId: number;
  periodId: number;
  status: ScorecardStatus;
  plan: PlanLine[];
  /** One actual per plan line; a missing or null entry means no result yet. */
  actuals?: (number | null)[];
  employeeComment?: string;
  managerRating?: number;
  managerComment?: string;
  /** Reason of a Return or Reject. */
  reason?: string;
}

const SEEDS: CardSeed[] = [
  // ---- Current (Active) period: id 2 ----
  {
    employeeId: 46,
    periodId: 2,
    status: 'Submitted',
    plan: PLAN_ENGINEERING,
    actuals: [30, 92, 6, 1, 93],
    employeeComment: 'Sample comment: most sprint goals were met.',
  },
  { employeeId: 17, periodId: 2, status: 'Draft', plan: PLAN_ENGINEERING, actuals: [28, 90] },
  {
    employeeId: 19,
    periodId: 2,
    status: 'Planning',
    plan: [
      [8, 30],
      [1, 30],
    ],
  },
  {
    employeeId: 6,
    periodId: 2,
    status: 'Under Review',
    plan: PLAN_SUPPORT,
    actuals: [96, 52000, 4, 1],
    managerRating: 4,
    managerComment: 'Sample feedback: steady quarter.',
  },
  {
    employeeId: 14,
    periodId: 2,
    status: 'Returned',
    plan: PLAN_ENGINEERING,
    actuals: [26, 85, 7, 1, 94],
    reason: 'Sample reason: please recheck the task count for the output KPI.',
  },
  { employeeId: 56, periodId: 2, status: 'Draft', plan: PLAN_GENERAL },
  {
    employeeId: 2,
    periodId: 2,
    status: 'Submitted',
    plan: PLAN_SUPPORT,
    actuals: [98, 49000, 4.5, 1],
  },
  // ---- Previous (Closed) period: id 1, with an HR final review ----
  {
    employeeId: 46,
    periodId: 1,
    status: 'Finalized',
    plan: PLAN_ENGINEERING,
    actuals: [22, 97, 5, 1, 95],
    managerRating: 4,
    managerComment: 'Sample feedback: reliable and quick reviews.',
  },
  {
    employeeId: 17,
    periodId: 1,
    status: 'Finalized',
    plan: PLAN_ENGINEERING,
    actuals: [26, 88, 7, 1, 90],
    managerRating: 3,
  },
  {
    employeeId: 14,
    periodId: 1,
    status: 'Finalized',
    plan: PLAN_ENGINEERING,
    actuals: [24, 100, 5, 1, 96],
    managerRating: 5,
    managerComment: 'Sample feedback: excellent delivery.',
  },
  {
    employeeId: 6,
    periodId: 1,
    status: 'Finalized',
    plan: PLAN_SUPPORT,
    actuals: [95, 50000, 4.5, 1],
    managerRating: 4,
  },
  {
    employeeId: 56,
    periodId: 1,
    status: 'Rejected',
    plan: PLAN_GENERAL,
    actuals: [10, 2, 9, 0],
    reason: 'Sample reason: the figures could not be confirmed.',
  },
];

// ---- Builders -------------------------------------------------------------------------------

const today = todayIso();
const clamp = (date: string): string => (date > today ? today : date);
const at = (date: string): string => `${clamp(date)}T09:00:00.000Z`;

const employeeName = (id: number): string => {
  const employee = MOCK_EMPLOYEES.find((e) => e.id === id);
  return employee ? fullName(employee) : `#${id}`;
};
const userIdOf = (employeeId: number | null): number | null =>
  MOCK_USERS.find((u) => u.employeeId === employeeId)?.id ?? null;

/** HR actor of the seeded HR steps: Su Myat Noe, or Thura Aung when she is the manager herself. */
const hrActorFor = (reviewerId: number | null): number => (reviewerId === 2 ? 1 : 2);

const SCORED: ScorecardStatus[] = [
  'Submitted',
  'Under Review',
  'Approved',
  'Finalized',
  'Rejected',
  'Resubmitted',
];

let nextItemId = 1;

function buildItems(seed: CardSeed, start: string, end: string): ScorecardItem[] {
  return seed.plan.map(([templateId, weight], index) => {
    const template = MOCK_KPI_TEMPLATES.find((t) => t.id === templateId)!;
    const category = MOCK_KPI_CATEGORIES.find((c) => c.id === template.categoryId)!;
    const actual = seed.actuals?.[index] ?? null;
    const achievement =
      actual === null
        ? null
        : kpiAchievement(
            {
              measurementType: template.measurementType,
              direction: template.direction,
              scoringMethod: template.scoringMethod,
              target: template.defaultTarget,
              rangeMin: template.rangeMin,
              rangeMax: template.rangeMax,
              maxRating: template.maxRating,
              maxAchievementPct: template.maxAchievementPct,
            },
            actual,
          );
    const achievementPct = achievement && achievement.ok ? achievement.value : null;
    const reviewed = seed.managerRating !== undefined && index === 0;
    return {
      id: nextItemId++,
      templateId,
      categoryId: template.categoryId,
      categoryName: category.name,
      kpiCode: template.code,
      kpiName: template.name,
      measurementType: template.measurementType,
      unit: template.unit,
      direction: template.direction,
      scoringMethod: template.scoringMethod,
      target: template.defaultTarget,
      rangeMin: template.rangeMin,
      rangeMax: template.rangeMax,
      maxRating: template.maxRating,
      weight,
      maxAchievementPct: template.maxAchievementPct,
      startDate: start,
      endDate: end,
      sortOrder: index + 1,
      actualValue: actual,
      employeeComment: null,
      achievementPct,
      weightedScore: achievementPct === null ? null : kpiWeightedScore(achievementPct, weight),
      managerRating: reviewed ? (seed.managerRating ?? null) : null,
      managerComment: reviewed ? (seed.managerComment ?? null) : null,
      rowVersion: '1',
      attachments: [],
    };
  });
}

function buildCard(seed: CardSeed, index: number): ScorecardRecord {
  const employee = MOCK_EMPLOYEES.find((e) => e.id === seed.employeeId)!;
  const period = MOCK_EVALUATION_PERIODS.find((p) => p.id === seed.periodId)!;
  const items = buildItems(seed, period.startDate, period.endDate);
  const planned = seed.status !== 'Planning';
  const reviewerEmployeeId = planned ? employee.managerId : null;
  const scored = SCORED.includes(seed.status);

  let overallScore: number | null = null;
  let ratingLabel: string | null = null;
  if (scored) {
    const result = scoreScorecard(
      items.map((item, i) => ({
        rule: {
          measurementType: item.measurementType,
          direction: item.direction,
          scoringMethod: item.scoringMethod,
          target: item.target,
          rangeMin: item.rangeMin,
          rangeMax: item.rangeMax,
          maxRating: item.maxRating,
          maxAchievementPct: item.maxAchievementPct,
        },
        weight: item.weight,
        actual: seed.actuals?.[i] ?? null,
      })),
    );
    if (result.ok) {
      overallScore = result.value.overallScore;
      ratingLabel = kpiRatingLabel(overallScore, DEFAULT_RATING_BANDS);
    }
  }

  // Dates: the plan is finalized early in the period; closed periods submit just after they end.
  const closed = period.status === 'Closed';
  const planDate = clamp(addDays(period.startDate, 1));
  const submitDate = clamp(closed ? addDays(period.endDate, 3) : addDays(period.startDate, 45));
  const decideDate = clamp(addDays(submitDate, 2));
  const finalDate = clamp(addDays(submitDate, 5));

  const history: ReviewAction[] = [];
  const log = (
    step: ReviewStep,
    action: ReviewActionName,
    fromStatus: ScorecardStatus,
    toStatus: ScorecardStatus,
    actorName: string,
    date: string,
    comment: string | null,
    score: number | null,
  ): void => {
    history.push({
      step,
      action,
      fromStatus,
      toStatus,
      actorName,
      comment,
      overallScore: score,
      occurredAtUtc: at(date),
    });
  };
  const hrName = employeeName(hrActorFor(reviewerEmployeeId));
  const reviewerName = reviewerEmployeeId === null ? hrName : employeeName(reviewerEmployeeId);
  const afterPlan: ScorecardStatus[] = [
    'Draft',
    'Submitted',
    'Under Review',
    'Returned',
    'Approved',
    'Rejected',
    'Finalized',
  ];
  if (afterPlan.includes(seed.status)) {
    log('HR', 'Finalize Plan', 'Planning', 'Draft', hrName, planDate, null, null);
  }
  const submitted: ScorecardStatus[] = [
    'Submitted',
    'Under Review',
    'Returned',
    'Rejected',
    'Finalized',
  ];
  if (submitted.includes(seed.status)) {
    log(
      'Employee',
      'Submit',
      'Draft',
      'Submitted',
      employeeName(seed.employeeId),
      submitDate,
      null,
      overallScore,
    );
  }
  if (seed.status === 'Under Review' || seed.status === 'Finalized') {
    log(
      'Manager',
      'Start Review',
      'Submitted',
      'Under Review',
      reviewerName,
      decideDate,
      null,
      overallScore,
    );
  }
  if (seed.status === 'Finalized') {
    log(
      'Manager',
      'Approve',
      'Under Review',
      'Approved',
      reviewerName,
      decideDate,
      null,
      overallScore,
    );
    log('HR', 'Finalize', 'Approved', 'Finalized', hrName, finalDate, null, overallScore);
  }
  if (seed.status === 'Returned') {
    log(
      'Manager',
      'Return',
      'Submitted',
      'Returned',
      reviewerName,
      decideDate,
      seed.reason ?? null,
      null,
    );
  }
  if (seed.status === 'Rejected') {
    log(
      'Manager',
      'Reject',
      'Submitted',
      'Rejected',
      reviewerName,
      decideDate,
      seed.reason ?? null,
      overallScore,
    );
  }

  const submittedOn = submitted.includes(seed.status) ? submitDate : null;
  return {
    id: index + 1,
    employeeId: employee.id,
    periodId: period.id,
    companyId: employee.companyId,
    branchId: employee.branchId,
    departmentId: employee.departmentId,
    positionId: employee.positionId,
    status: seed.status,
    reviewerEmployeeId,
    planFinalizedOn: planned ? planDate : null,
    submittedOn,
    reviewedOn: seed.status === 'Finalized' ? decideDate : null,
    finalizedOn: seed.status === 'Finalized' ? finalDate : null,
    employeeComment: seed.employeeComment ?? null,
    managerRating: seed.managerRating ?? null,
    managerComment: seed.managerComment ?? null,
    hrComment: null,
    overallScore: seed.status === 'Returned' ? null : overallScore,
    ratingLabel: seed.status === 'Returned' ? null : ratingLabel,
    managerApprovedByUserId: seed.status === 'Finalized' ? userIdOf(reviewerEmployeeId) : null,
    items,
    history,
    rowVersion: '1',
  };
}

/** SAMPLE DATA - fictional scorecards of the seeded employees. */
export const MOCK_SCORECARDS: ScorecardRecord[] = SEEDS.map(buildCard);
