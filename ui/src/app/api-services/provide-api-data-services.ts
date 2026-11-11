import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import {
  EnvironmentProviders,
  Provider,
  Type,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { AuthService, SERVER_SESSION } from '../core/services/auth.service';
import { AuthDataService } from '../core/services/data/auth-data.service';
import {
  AttendanceAdjustmentDataService,
  AttendanceOverviewDataService,
  AttendanceRecordDataService,
  HolidayDataService,
  OvertimeDataService,
  ShiftDataService,
  WorkScheduleDataService,
} from '../core/services/data/attendance-data.services';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../core/services/data/organization-data.services';
import { EmployeeDataService } from '../core/services/data/employee-data.service';
import { EmploymentHistoryDataService } from '../core/services/data/employee-extras-data.services';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../core/services/data/leave-data.services';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../core/services/data/recruitment-data.services';
import {
  ActivityDataService,
  NotificationDataService,
} from '../core/services/data/activity-data.service';
import { DashboardDataService } from '../core/services/data/dashboard-data.service';
import { API_BASE_URL } from './api-base-url';
import { ActivityHttpService } from './activity/activity-http.service';
import { NotificationHttpService } from './activity/notification-http.service';
import { DashboardHttpService } from './dashboard/dashboard-http.service';
import {
  AttendanceAdjustmentHttpService,
  OvertimeHttpService,
} from './attendance/attendance-approval-http.services';
import { HolidayHttpService, ShiftHttpService } from './attendance/attendance-http.services';
import { AttendanceRecordHttpService } from './attendance/attendance-record-http.service';
import {
  AttendanceOverviewHttpService,
  WorkScheduleHttpService,
} from './attendance/attendance-views-http.services';
import { EmployeeHttpService } from './employees/employee-http.service';
import { EmploymentHistoryHttpService } from './employees/employment-history-http.service';
import { LeaveApplicationHttpService } from './leave/leave-application-http.service';
import { LeaveBalanceHttpService } from './leave/leave-balance-http.service';
import { LeaveCalendarHttpService } from './leave/leave-calendar-http.service';
import { LeavePolicyHttpService, LeaveTypeHttpService } from './leave/leave-http.services';
import { ApplicationHttpService } from './recruitment/application-http.service';
import { CandidateHttpService } from './recruitment/candidate-http.service';
import { InterviewHttpService } from './recruitment/interview-http.service';
import { JobOpeningHttpService } from './recruitment/job-opening-http.service';
import { apiAuthInterceptor } from './auth/api-auth.interceptor';
import { AuthHttpService } from './auth/auth-http.service';
import {
  BranchHttpService,
  CompanyHttpService,
  DepartmentHttpService,
  JobGradeHttpService,
  PositionHttpService,
  SectionHttpService,
} from './organization/organization-http.services';

export interface ApiDataOptions {
  /** Master switch. When false nothing is registered and the mocks stay in charge. */
  enabled: boolean;
  /** API root. Defaults to `/api` (dev proxy). */
  baseUrl?: string;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AbstractType<T> = abstract new (...args: any[]) => T;

function bind<T>(contract: AbstractType<T>, implementation: Type<T>): Provider {
  return { provide: contract, useExisting: implementation };
}

/**
 * Binds ONLY the Organization, Employee (employees, employment history), Leave, Attendance, Recruitment, Dashboard, Activity and Notification contracts to HTTP. Every other contract stays on its mock
 * (the adapter is per dependency cluster). Register it AFTER `provideMockDataServices()` so
 * these bindings win; `app.config.ts` does that and the switch defaults to off.
 */
export function provideApiDataServices(options: ApiDataOptions): EnvironmentProviders {
  if (!options.enabled) {
    return makeEnvironmentProviders([]);
  }
  return makeEnvironmentProviders([
    provideHttpClient(
      withInterceptors([apiAuthInterceptor]),
      // The API sets the XSRF-TOKEN cookie (GET auth/antiforgery); Angular echoes it as X-XSRF-TOKEN.
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    { provide: API_BASE_URL, useValue: options.baseUrl ?? '/api' },
    bind(CompanyDataService, CompanyHttpService),
    bind(BranchDataService, BranchHttpService),
    bind(DepartmentDataService, DepartmentHttpService),
    bind(SectionDataService, SectionHttpService),
    bind(JobGradeDataService, JobGradeHttpService),
    bind(PositionDataService, PositionHttpService),
    // Employees: list, record, personal, bank, status and read-only history.
    bind(EmployeeDataService, EmployeeHttpService),
    bind(EmploymentHistoryDataService, EmploymentHistoryHttpService),
    // Leave: types, policies, applications (with the submit/approve/reject/cancel commands), balances, calendar.
    bind(LeaveTypeDataService, LeaveTypeHttpService),
    bind(LeavePolicyDataService, LeavePolicyHttpService),
    bind(LeaveApplicationDataService, LeaveApplicationHttpService),
    bind(LeaveBalanceDataService, LeaveBalanceHttpService),
    bind(LeaveCalendarDataService, LeaveCalendarHttpService),
    // Attendance: shifts, holidays, read-only records with punches, adjustments, overtime, overview, schedule.
    bind(ShiftDataService, ShiftHttpService),
    bind(HolidayDataService, HolidayHttpService),
    bind(AttendanceRecordDataService, AttendanceRecordHttpService),
    bind(AttendanceAdjustmentDataService, AttendanceAdjustmentHttpService),
    bind(OvertimeDataService, OvertimeHttpService),
    bind(AttendanceOverviewDataService, AttendanceOverviewHttpService),
    bind(WorkScheduleDataService, WorkScheduleHttpService),
    // Recruitment: job openings (status commands), candidates, applications (stage command), interviews (outcome and cancel commands).
    bind(JobOpeningDataService, JobOpeningHttpService),
    bind(CandidateDataService, CandidateHttpService),
    bind(ApplicationDataService, ApplicationHttpService),
    bind(InterviewDataService, InterviewHttpService),
    // Dashboard summary (one call), append-only activity feed (read only) and notifications.
    bind(DashboardDataService, DashboardHttpService),
    bind(ActivityDataService, ActivityHttpService),
    bind(NotificationDataService, NotificationHttpService),
    // Real sign-in: cookie session owned by the server, kept in memory only.
    bind(AuthDataService, AuthHttpService),
    { provide: SERVER_SESSION, useValue: true },
    provideAppInitializer(() => inject(AuthService).initialize()),
  ]);
}
