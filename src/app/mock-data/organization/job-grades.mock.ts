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
  [1, 'G1', 'Entry Level', 400, 700, 'Interns, trainees and entry-level operational roles.'],
  [2, 'G2', 'Associate', 600, 1000, 'Junior individual contributors.'],
  [3, 'G3', 'Professional', 900, 1500, 'Experienced individual contributors.'],
  [4, 'G4', 'Senior Professional', 1400, 2200, 'Senior specialists and subject-matter experts.'],
  [5, 'G5', 'Lead / Supervisor', 2000, 3000, 'Team leads and first-line supervisors.'],
  [6, 'G6', 'Manager', 2800, 4200, 'Department and functional managers.'],
  [7, 'G7', 'Senior Manager', 4000, 6000, 'Senior managers with multiple teams.'],
  [8, 'G8', 'Director', 5500, 8500, 'Heads of business functions.'],
  [9, 'G9', 'Executive', 8000, 14000, 'C-level executives.'],
];

/** SAMPLE DATA — salary bands are illustrative monthly figures only. */
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
