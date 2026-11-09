import { JobGrade } from '../../core/models/organization.models';

type GradeSeed = [
  id: number,
  code: string,
  name: string,
  min: number,
  max: number,
  description: string,
];

const SEED: GradeSeed[] = [
  [1, 'G1', 'Entry Level', 400000, 700000, 'Interns, trainees and entry-level operational roles.'],
  [2, 'G2', 'Associate', 600000, 1000000, 'Junior individual contributors.'],
  [3, 'G3', 'Professional', 900000, 1500000, 'Experienced individual contributors.'],
  [
    4,
    'G4',
    'Senior Professional',
    1400000,
    2200000,
    'Senior specialists and subject-matter experts.',
  ],
  [5, 'G5', 'Lead / Supervisor', 2000000, 3000000, 'Team leads and first-line supervisors.'],
  [6, 'G6', 'Manager', 2800000, 4200000, 'Department and functional managers.'],
  [7, 'G7', 'Senior Manager', 4000000, 6000000, 'Senior managers with multiple teams.'],
  [8, 'G8', 'Director', 5500000, 8500000, 'Heads of business functions.'],
  [9, 'G9', 'Executive', 8000000, 14000000, 'C-level executives.'],
];

/** SAMPLE DATA — salary bands are illustrative monthly figures in MMK only. */
export const MOCK_JOB_GRADES: JobGrade[] = SEED.map(
  ([id, code, name, minSalary, maxSalary, description]) => ({
    id,
    code,
    name,
    level: id,
    minSalary,
    maxSalary,
    description,
    status: 'Active',
  }),
);
