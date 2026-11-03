import {
  SalaryAssignment,
  SalaryComponent,
  SalaryStructure,
} from '../../core/models/payroll.models';
import { addMonths, todayIso } from '../../shared/utilities/date.util';
import { MOCK_EMPLOYEES } from '../employees/employees.mock';
import { seededRandom } from '../mock-random';
import { MOCK_JOB_GRADES } from '../organization/job-grades.mock';

type ComponentSeed = Omit<SalaryComponent, 'id'>;

const COMPONENT_SEED: ComponentSeed[] = [
  {
    code: 'HOUSING',
    name: 'Housing allowance',
    type: 'Earning',
    calculation: 'PercentOfBasic',
    value: 10,
    description: 'Contribution towards housing costs.',
    status: 'Active',
  },
  {
    code: 'TRANSPORT',
    name: 'Transport allowance',
    type: 'Earning',
    calculation: 'Fixed',
    value: 50,
    description: 'Fixed monthly commuting allowance.',
    status: 'Active',
  },
  {
    code: 'MEAL',
    name: 'Meal allowance',
    type: 'Earning',
    calculation: 'Fixed',
    value: 30,
    description: 'Fixed monthly meal allowance.',
    status: 'Active',
  },
  {
    code: 'PERFORMANCE',
    name: 'Performance allowance',
    type: 'Earning',
    calculation: 'PercentOfBasic',
    value: 5,
    description: 'Recurring allowance for management roles.',
    status: 'Active',
  },
  {
    code: 'EXECUTIVE',
    name: 'Executive allowance',
    type: 'Earning',
    calculation: 'Fixed',
    value: 300,
    description: 'Fixed monthly allowance for executive roles.',
    status: 'Active',
  },
  {
    code: 'TAX',
    name: 'Sample income tax',
    type: 'Deduction',
    calculation: 'PercentOfGross',
    value: 5,
    description: 'Demonstration rate only — not a real tax calculation.',
    status: 'Active',
  },
  {
    code: 'SOCIAL',
    name: 'Sample social contribution',
    type: 'Deduction',
    calculation: 'PercentOfGross',
    value: 2,
    description: 'Demonstration rate only — not a statutory contribution.',
    status: 'Active',
  },
  {
    code: 'PENSION',
    name: 'Sample pension (employee)',
    type: 'Deduction',
    calculation: 'PercentOfBasic',
    value: 3,
    description: 'Not used by any structure yet.',
    status: 'Inactive',
  },
];

/** SAMPLE DATA — demonstration components. */
export const MOCK_SALARY_COMPONENTS: SalaryComponent[] = COMPONENT_SEED.map((c, i) => ({
  id: i + 1,
  ...c,
}));

const id = (code: string) => MOCK_SALARY_COMPONENTS.find((c) => c.code === code)!.id;

/** SAMPLE DATA */
export const MOCK_SALARY_STRUCTURES: SalaryStructure[] = [
  {
    id: 1,
    code: 'STAFF',
    name: 'Staff structure',
    description: 'Grades G1–G5.',
    componentIds: [id('HOUSING'), id('TRANSPORT'), id('MEAL'), id('TAX'), id('SOCIAL')],
    status: 'Active',
  },
  {
    id: 2,
    code: 'MGMT',
    name: 'Management structure',
    description: 'Grades G6–G7.',
    componentIds: [id('HOUSING'), id('TRANSPORT'), id('PERFORMANCE'), id('TAX'), id('SOCIAL')],
    status: 'Active',
  },
  {
    id: 3,
    code: 'EXEC',
    name: 'Executive structure',
    description: 'Grades G8–G9.',
    componentIds: [id('HOUSING'), id('TRANSPORT'), id('EXECUTIVE'), id('TAX'), id('SOCIAL')],
    status: 'Active',
  },
];

/** Deterministic sample basic salary inside the employee's job-grade band. */
export function sampleBasicSalary(employee: { id: number; jobGradeId: number }): number {
  const grade = MOCK_JOB_GRADES.find((g) => g.id === employee.jobGradeId) ?? MOCK_JOB_GRADES[0];
  return (
    Math.round(
      (grade.minSalary +
        (grade.maxSalary - grade.minSalary) * seededRandom(`salary-${employee.id}`)) /
        10,
    ) * 10
  );
}

/** Structure used for a job-grade level. */
export function defaultStructureId(gradeLevel: number): number {
  return gradeLevel >= 8 ? 3 : gradeLevel >= 6 ? 2 : 1;
}

function generateAssignments(): SalaryAssignment[] {
  const today = todayIso();
  const thisYear = `${today.slice(0, 4)}-01-01`;
  const list: SalaryAssignment[] = [];
  for (const e of MOCK_EMPLOYEES) {
    const level = MOCK_JOB_GRADES.find((g) => g.id === e.jobGradeId)?.level ?? 1;
    const structureId = defaultStructureId(level);
    const current = sampleBasicSalary(e);
    const longServing = e.joiningDate <= addMonths(thisYear, -12);
    if (longServing) {
      list.push({
        id: list.length + 1,
        employeeId: e.id,
        structureId,
        basicSalary: Math.round((current * 0.92) / 10) * 10,
        effectiveFrom: e.joiningDate,
        remarks: 'Starting salary',
      });
    }
    list.push({
      id: list.length + 1,
      employeeId: e.id,
      structureId,
      basicSalary: current,
      effectiveFrom: longServing ? thisYear : e.joiningDate,
      remarks: longServing ? 'Annual revision' : 'Starting salary',
    });
  }
  return list;
}

/** SAMPLE DATA — one or two assignments per employee (long-serving staff got a revision this year). */
export const MOCK_SALARY_ASSIGNMENTS: SalaryAssignment[] = generateAssignments();
