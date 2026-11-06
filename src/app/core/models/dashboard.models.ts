import { ActivityEntry } from './activity.models';
import { PayrollRunStatus } from './payroll.models';

export interface HeadcountSummary {
  total: number;
  active: number;
  probation: number;
  newThisMonth: number;
  onLeaveToday: number;
  exitsThisMonth: number;
}

export interface TodayAttendanceSummary {
  date: string;
  scheduled: number;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
  halfDay: number;
  /** Percentage of scheduled employees who checked in (present + late + half day). */
  attendanceRate: number;
}

export interface PayrollSummary {
  periodName: string;
  status: PayrollRunStatus;
  payDate: string;
  employeeCount: number;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
  /** % change of net payroll versus the previous period. */
  netChangePercent: number;
}

export interface CountByLabel {
  label: string;
  count: number;
}

export interface AttendanceTrendPoint {
  date: string;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
}

export interface HeadcountGrowthPoint {
  /** yyyy-MM */
  month: string;
  headcount: number;
  joiners: number;
  exits: number;
}

export type UpcomingEventType = 'Holiday' | 'Birthday' | 'Work Anniversary' | 'Leave' | 'Probation';

export interface UpcomingEvent {
  date: string;
  title: string;
  subtitle: string;
  type: UpcomingEventType;
}

export interface PendingLeaveItem {
  id: number;
  employeeName: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  days: number;
  appliedOn: string;
}

export interface RecruitmentSummary {
  openJobs: number;
  openPositions: number;
  activeCandidates: number;
  upcomingInterviews: number;
  /** Applications per pipeline stage (Applied → Selected). */
  funnel: CountByLabel[];
}

export interface DashboardSummary {
  generatedAt: string;
  headcount: HeadcountSummary;
  attendanceToday: TodayAttendanceSummary;
  pendingLeaveApprovals: number;
  pendingLeaves: PendingLeaveItem[];
  payroll: PayrollSummary | null;
  recruitment: RecruitmentSummary;
  departmentDistribution: CountByLabel[];
  attendanceTrend: AttendanceTrendPoint[];
  headcountGrowth: HeadcountGrowthPoint[];
  recentActivities: ActivityEntry[];
  upcomingEvents: UpcomingEvent[];
}
