import { Position } from '../../core/models/organization.models';

type PositionSeed = [
  id: number,
  departmentId: number,
  sectionId: number | null,
  jobGradeId: number,
  title: string,
  headcount: number,
  reportsToPositionId: number | null,
];

const SEED: PositionSeed[] = [
  [1, 1, null, 9, 'Chief Executive Officer', 1, null],
  [2, 2, null, 8, 'Director of Human Resources', 1, 1],
  [3, 2, 3, 6, 'HR Manager', 1, 2],
  [4, 2, 1, 3, 'Talent Acquisition Specialist', 2, 3],
  [5, 2, 2, 3, 'Payroll Officer', 2, 3],
  [6, 2, 3, 2, 'HR Executive', 2, 3],
  [7, 3, null, 8, 'Finance Director', 1, 1],
  [8, 3, 4, 3, 'Accountant', 4, 9],
  [9, 3, 5, 6, 'Finance Manager', 1, 7],
  [10, 4, null, 8, 'Head of Engineering', 1, 1],
  [11, 4, 6, 4, 'Senior Frontend Engineer', 2, 14],
  [12, 4, 7, 4, 'Senior Backend Engineer', 2, 14],
  [13, 4, 8, 3, 'QA Engineer', 3, 14],
  [14, 4, null, 6, 'Engineering Manager', 1, 10],
  [15, 4, 7, 3, 'Software Engineer', 6, 14],
  [16, 5, null, 6, 'Product Manager', 1, 10],
  [17, 5, null, 3, 'UI/UX Designer', 2, 16],
  [18, 6, null, 6, 'Sales Manager', 1, 1],
  [19, 6, 9, 3, 'Account Executive', 4, 18],
  [20, 6, 10, 4, 'Partner Manager', 2, 18],
  [21, 7, null, 6, 'IT Manager', 1, 10],
  [22, 7, 11, 3, 'Network Engineer', 2, 21],
  [23, 7, 12, 2, 'IT Support Technician', 2, 21],
  [24, 8, null, 5, 'Customer Support Lead', 1, 18],
  [25, 8, 13, 1, 'Customer Support Agent', 6, 24],
  [26, 9, null, 6, 'Warehouse Manager', 1, 1],
  [27, 9, 14, 5, 'Warehouse Supervisor', 2, 26],
  [28, 9, 15, 1, 'Warehouse Associate', 5, 27],
  [29, 10, null, 4, 'Fleet Coordinator', 1, 26],
  [30, 10, 16, 1, 'Delivery Driver', 4, 29],
  [31, 11, null, 6, 'Marketing Manager', 1, 1],
  [32, 11, null, 3, 'Digital Marketing Specialist', 2, 31],
  [33, 4, null, 1, 'Engineering Intern', 3, 14],
];

/** SAMPLE DATA */
export const MOCK_POSITIONS: Position[] = SEED.map(
  ([id, departmentId, sectionId, jobGradeId, title, headcount, reportsToPositionId]) => ({
    id,
    departmentId,
    sectionId,
    jobGradeId,
    code: `POS-${String(id).padStart(3, '0')}`,
    title,
    description: `${title} role.`,
    headcount,
    reportsToPositionId,
    status: 'Active',
  }),
);
