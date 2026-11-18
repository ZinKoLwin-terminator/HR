import { GrievanceCategory } from '../../core/models/grievance.models';

type CategorySeed = [
  code: string,
  name: string,
  description: string,
  allowsAnonymous: boolean,
  defaultTargetDays: number,
];

/**
 * The 10 default categories, as the database seed (`Script.PostDeployment.sql`) inserts them: same
 * codes, names, descriptions, anonymity, target days and order (sort order 1 to 10). Salary and
 * Compensation Issues does not allow anonymous reports: HR has to see whose pay is concerned.
 */
const SEED: CategorySeed[] = [
  [
    'HARASSMENT',
    'Workplace Harassment',
    'Bullying, intimidation, sexual or other harassment at work.',
    true,
    14,
  ],
  [
    'DISCRIMINATION',
    'Discrimination',
    'Unequal treatment because of a protected characteristic.',
    true,
    14,
  ],
  [
    'COMPENSATION',
    'Salary and Compensation Issues',
    'Pay, allowances, deductions or benefits.',
    false,
    21,
  ],
  ['SAFETY', 'Workplace Safety', 'Unsafe conditions, equipment or practices.', true, 7],
  ['RELATIONS', 'Employee Relations', 'Conflicts between colleagues or within a team.', true, 30],
  [
    'MISCONDUCT',
    'Management Misconduct',
    'Abuse of authority or improper conduct by a manager.',
    true,
    21,
  ],
  [
    'UNFAIR-TREATMENT',
    'Unfair Treatment',
    'Unfair decisions on assignments, appraisals, promotion or discipline.',
    true,
    30,
  ],
  [
    'POLICY',
    'Policy Violations',
    'A company policy or procedure that is not being followed.',
    true,
    30,
  ],
  [
    'CONDITIONS',
    'Working Conditions',
    'Working hours, workload, facilities or the work environment.',
    true,
    30,
  ],
  ['OTHER', 'Other', 'Anything that does not fit another category.', true, 30],
];

/** The id of Salary and Compensation Issues (no anonymous reports), for specs and samples. */
export const GRIEVANCE_COMPENSATION_CATEGORY_ID = 3;

/** SAMPLE DATA - the default grievance categories (the database seed). */
export const MOCK_GRIEVANCE_CATEGORIES: GrievanceCategory[] = SEED.map(
  ([code, name, description, allowsAnonymous, defaultTargetDays], i) => ({
    id: i + 1,
    code,
    name,
    description,
    allowsAnonymous,
    defaultTargetDays,
    sortOrder: i + 1,
    status: 'Active',
    rowVersion: '1',
  }),
);
