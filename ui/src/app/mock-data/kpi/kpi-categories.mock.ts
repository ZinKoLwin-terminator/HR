import { KpiCategory } from '../../core/models/kpi.models';

type CategorySeed = [code: string, name: string, description: string];

const SEED: CategorySeed[] = [
  ['PRODUCTIVITY', 'Productivity', 'Output and volume of work delivered.'],
  ['QUALITY', 'Quality', 'Accuracy and defect-free delivery.'],
  ['ATTENDANCE', 'Attendance', 'Presence at work as scheduled.'],
  ['PUNCTUALITY', 'Punctuality', 'Starting and delivering on time.'],
  ['COMMUNICATION', 'Communication', 'Clear and timely communication.'],
  ['TEAMWORK', 'Teamwork', 'Collaboration and support for colleagues.'],
  ['LEADERSHIP', 'Leadership', 'Guiding, coaching and decision making.'],
  ['CUSTOMER-SAT', 'Customer Satisfaction', 'How customers rate the service they receive.'],
  ['TECHNICAL', 'Technical Skills', 'Depth and currency of job-related skills.'],
  ['PROBLEM-SOLVING', 'Problem Solving', 'Finding and fixing root causes.'],
  ['PROJECT-DELIVERY', 'Project Delivery', 'Delivering projects on scope, time and budget.'],
];

/** SAMPLE DATA - the example categories of the KPI design. Fictional. */
export const MOCK_KPI_CATEGORIES: KpiCategory[] = SEED.map(([code, name, description], i) => ({
  id: i + 1,
  code,
  name,
  description,
  status: 'Active',
  rowVersion: '1',
}));
