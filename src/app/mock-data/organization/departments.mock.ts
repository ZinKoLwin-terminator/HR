import { Department } from '../../core/models/organization.models';

type DepartmentSeed = [
  id: number,
  companyId: number,
  branchId: number,
  code: string,
  name: string,
  headEmployeeId: number | null,
  description: string,
];

const SEED: DepartmentSeed[] = [
  [1, 1, 1, 'EXEC', 'Executive Office', 1, 'Corporate leadership and strategy.'],
  [
    2,
    1,
    1,
    'HR',
    'Human Resources',
    2,
    'People operations, talent, payroll and employee relations.',
  ],
  [3, 1, 1, 'FIN', 'Finance & Accounts', 4, 'Accounting, treasury, budgeting and reporting.'],
  [4, 1, 1, 'ENG', 'Engineering', 3, 'Software design, development and quality assurance.'],
  [5, 1, 1, 'PRD', 'Product Management', 10, 'Product strategy, roadmap and UX design.'],
  [6, 1, 2, 'SAL', 'Sales & Business Development', 7, 'Enterprise sales and partner channels.'],
  [7, 1, 1, 'IT', 'IT Infrastructure', 9, 'Networks, security and internal IT support.'],
  [8, 1, 3, 'CS', 'Customer Support', 26, 'Customer help desk and service quality.'],
  [9, 2, 4, 'OPS', 'Warehouse Operations', 12, 'Inbound and outbound warehouse operations.'],
  [10, 2, 5, 'LOG', 'Fleet & Logistics', 40, 'Fleet scheduling, dispatch and delivery.'],
  [11, 1, 6, 'MKT', 'Marketing', 11, 'Brand, digital marketing and communications.'],
];

/** SAMPLE DATA */
export const MOCK_DEPARTMENTS: Department[] = SEED.map(
  ([id, companyId, branchId, code, name, headEmployeeId, description]) => ({
    id,
    companyId,
    branchId,
    code,
    name,
    description,
    headEmployeeId,
    costCenter: `CC-${String(id * 100).padStart(4, '0')}`,
    status: 'Active',
  }),
);
