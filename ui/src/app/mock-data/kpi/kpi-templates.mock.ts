import {
  KpiDirection,
  KpiFrequency,
  KpiMeasurementType,
  KpiScoringMethod,
  KpiTemplate,
} from '../../core/models/kpi.models';

interface TemplateSeed {
  code: string;
  name: string;
  description: string;
  categoryId: number;
  departmentId?: number;
  measurementType: KpiMeasurementType;
  unit: string | null;
  direction: KpiDirection;
  scoringMethod: KpiScoringMethod;
  target: number;
  rangeMin?: number;
  rangeMax?: number;
  maxRating?: number;
  weight: number;
  cap?: number;
  frequency: KpiFrequency;
}

// Category ids follow kpi-categories.mock.ts: 1 Productivity, 2 Quality, 3 Attendance,
// 5 Communication, 6 Teamwork, 8 Customer Satisfaction, 9 Technical Skills, 11 Project Delivery.
// Department 4 is Engineering (ENG).
const SEED: TemplateSeed[] = [
  {
    code: 'OUTPUT-TASKS',
    name: 'Tasks completed',
    description: 'Planned tasks finished in the period. Beating the target is rewarded in full.',
    categoryId: 1,
    measurementType: 'Number',
    unit: 'tasks',
    direction: 'Higher Is Better',
    scoringMethod: 'Direct Percentage',
    target: 100,
    weight: 30,
    frequency: 'Quarterly',
  },
  {
    code: 'DEFECT-RATE',
    name: 'Defect rate',
    description:
      'Share of delivered work that needed rework. Lower is better; zero scores the cap.',
    categoryId: 2,
    measurementType: 'Percentage',
    unit: '%',
    direction: 'Lower Is Better',
    scoringMethod: 'Capped Percentage',
    target: 5,
    weight: 20,
    cap: 120,
    frequency: 'Quarterly',
  },
  {
    code: 'ATTENDANCE-RATE',
    name: 'Attendance rate',
    description: 'Scheduled days attended.',
    categoryId: 3,
    measurementType: 'Percentage',
    unit: '%',
    direction: 'Higher Is Better',
    scoringMethod: 'Capped Percentage',
    target: 95,
    weight: 10,
    cap: 105,
    frequency: 'Quarterly',
  },
  {
    code: 'BUDGET-ADHERENCE',
    name: 'Budget adherence',
    description:
      'Spend close to the approved budget. Both overspending and underspending lose points.',
    categoryId: 11,
    measurementType: 'Currency',
    unit: 'MMK',
    direction: 'Exact Target',
    scoringMethod: 'Direct Percentage',
    target: 50000,
    weight: 30,
    frequency: 'Quarterly',
  },
  {
    code: 'CUSTOMER-RATING',
    name: 'Customer rating',
    description: 'Average customer rating on a 5 point scale.',
    categoryId: 8,
    measurementType: 'Rating',
    unit: 'points',
    direction: 'Higher Is Better',
    scoringMethod: 'Rating Based',
    target: 5,
    maxRating: 5,
    weight: 25,
    frequency: 'Quarterly',
  },
  {
    code: 'TRAINING-DONE',
    name: 'Mandatory training completed',
    description: 'Yes or no: the required training was completed.',
    categoryId: 6,
    measurementType: 'Boolean',
    unit: null,
    direction: 'Higher Is Better',
    scoringMethod: 'Boolean',
    target: 1,
    weight: 10,
    frequency: 'Yearly',
  },
  {
    code: 'RESPONSE-HOURS',
    name: 'Response time',
    description: 'Hours to first reply. Between 2 and 4 hours is on target.',
    categoryId: 5,
    measurementType: 'Hours',
    unit: 'hours',
    direction: 'Range Based',
    scoringMethod: 'Range Based',
    target: 3,
    rangeMin: 2,
    rangeMax: 4,
    weight: 15,
    frequency: 'Monthly',
  },
  {
    code: 'ENG-REVIEW-HOURS',
    name: 'Code review turnaround',
    description: 'Hours to review a pull request. Engineering only. Lower is better.',
    categoryId: 9,
    departmentId: 4,
    measurementType: 'Hours',
    unit: 'hours',
    direction: 'Lower Is Better',
    scoringMethod: 'Capped Percentage',
    target: 24,
    weight: 30,
    cap: 110,
    frequency: 'Quarterly',
  },
];

/** SAMPLE DATA - one template for every direction and scoring method. Fictional. */
export const MOCK_KPI_TEMPLATES: KpiTemplate[] = SEED.map((seed, i) => ({
  id: i + 1,
  code: seed.code,
  name: seed.name,
  description: seed.description,
  categoryId: seed.categoryId,
  departmentId: seed.departmentId ?? null,
  measurementType: seed.measurementType,
  unit: seed.unit,
  direction: seed.direction,
  scoringMethod: seed.scoringMethod,
  defaultTarget: seed.target,
  rangeMin: seed.rangeMin ?? null,
  rangeMax: seed.rangeMax ?? null,
  maxRating: seed.maxRating ?? null,
  defaultWeight: seed.weight,
  maxAchievementPct: seed.cap ?? null,
  frequency: seed.frequency,
  status: 'Active',
  rowVersion: '1',
}));
