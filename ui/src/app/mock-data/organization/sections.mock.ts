import { Section } from '../../core/models/organization.models';

type SectionSeed = [
  id: number,
  departmentId: number,
  code: string,
  name: string,
  supervisorEmployeeId: number | null,
];

const SEED: SectionSeed[] = [
  [1, 2, 'HR-TA', 'Talent Acquisition', 13],
  [2, 2, 'HR-PB', 'Payroll & Benefits', 15],
  [3, 2, 'HR-ER', 'Employee Relations', 6],
  [4, 3, 'FIN-AP', 'Accounts Payable', 24],
  [5, 3, 'FIN-FP', 'Financial Planning', 8],
  [6, 4, 'ENG-FE', 'Frontend Development', 14],
  [7, 4, 'ENG-BE', 'Backend Development', 16],
  [8, 4, 'ENG-QA', 'Quality Assurance', 17],
  [9, 6, 'SAL-ENT', 'Enterprise Sales', 21],
  [10, 6, 'SAL-CH', 'Channel Partners', 25],
  [11, 7, 'IT-NS', 'Network & Security', 27],
  [12, 7, 'IT-HD', 'IT Helpdesk', 31],
  [13, 8, 'CS-T1', 'Tier 1 Support', 26],
  [14, 9, 'OPS-IN', 'Inbound Operations', 29],
  [15, 9, 'OPS-OUT', 'Outbound Operations', 29],
  [16, 10, 'LOG-FL', 'Fleet Management', 40],
];

/** SAMPLE DATA */
export const MOCK_SECTIONS: Section[] = SEED.map(
  ([id, departmentId, code, name, supervisorEmployeeId]) => ({
    id,
    departmentId,
    code,
    name,
    description: `${name} team.`,
    supervisorEmployeeId,
    status: 'Active',
  }),
);
