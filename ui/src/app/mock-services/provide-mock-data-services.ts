import { EnvironmentProviders, Provider, Type, makeEnvironmentProviders } from '@angular/core';
import {
  ActivityDataService,
  NotificationDataService,
} from '../core/services/data/activity-data.service';
import {
  AttendanceAdjustmentDataService,
  AttendanceOverviewDataService,
  AttendanceRecordDataService,
  HolidayDataService,
  OvertimeDataService,
  ShiftDataService,
  WorkScheduleDataService,
} from '../core/services/data/attendance-data.services';
import { AuthDataService } from '../core/services/data/auth-data.service';
import { AuthMockService } from './auth/auth-mock.service';
import { DashboardDataService } from '../core/services/data/dashboard-data.service';
import { BlacklistDataService } from '../core/services/data/blacklist-data.service';
import {
  EmployeeDataService,
  EmployeeLookupDataService,
} from '../core/services/data/employee-data.service';
import {
  EmployeeDocumentDataService,
  EmployeeSalaryDataService,
  EmploymentHistoryDataService,
} from '../core/services/data/employee-extras-data.services';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveCalendarDataService,
  LeavePolicyDataService,
  LeaveTypeDataService,
} from '../core/services/data/leave-data.services';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../core/services/data/organization-data.services';
import {
  MyPayslipDataService,
  PayrollRunDataService,
  PayslipDataService,
  SalaryAssignmentDataService,
  SalaryComponentDataService,
  SalaryStructureDataService,
} from '../core/services/data/payroll-data.services';
import {
  ApplicationDataService,
  CandidateDataService,
  InterviewDataService,
  JobOpeningDataService,
} from '../core/services/data/recruitment-data.services';
import {
  ApplicationMockService,
  CandidateMockService,
  InterviewMockService,
  JobOpeningMockService,
} from './recruitment/recruitment-mock.services';
import {
  GeneralSettingsDataService,
  RoleDataService,
  RolePermissionDataService,
  UserDataService,
} from '../core/services/data/settings-data.services';
import {
  GeneralSettingsMockService,
  RoleMockService,
  RolePermissionMockService,
  UserMockService,
} from './settings/settings-mock.services';
import {
  ExcelExportDataService,
  ExcelImportDataService,
  ExcelModuleCatalogDataService,
} from '../core/services/data/excel-data.services';
import { ExcelCatalogMockService, ExcelExportMockService } from './excel/excel-export.mock.service';
import { ExcelImportMockService } from './excel/excel-import.mock.service';
import { EXCEL_MODULES } from './excel/excel-module';
import { employeeExcelRegistration } from './excel/modules/employee-excel.module';
import { ORGANIZATION_EXCEL_REGISTRATIONS } from './excel/modules/organization-excel.modules';
import { OPERATIONS_EXCEL_REGISTRATIONS } from './excel/modules/operations-excel.modules';
import { ActivityMockService } from './activity/activity-mock.service';
import { NotificationMockService } from './activity/notification-mock.service';
import {
  AttendanceRecordMockService,
  HolidayMockService,
  ShiftMockService,
} from './attendance/attendance-mock.services';
import {
  AttendanceAdjustmentMockService,
  OvertimeMockService,
} from './attendance/attendance-workflow-mock.services';
import {
  AttendanceOverviewMockService,
  WorkScheduleMockService,
} from './attendance/attendance-views-mock.services';
import { DashboardMockService } from './dashboard/dashboard-mock.service';
import { DemoDataDataService } from '../core/services/data/demo-data-data.service';
import { DemoDataMockService } from './demo-data/demo-data-mock.service';
import { setPersistence } from './persistence';
import { setEmptyData } from './seed-mode';
import { BlacklistMockService } from './employees/blacklist-mock.service';
import { EmployeeLookupMockService } from './employees/employee-lookup-mock.service';
import { EmployeeMockService } from './employees/employee-mock.service';
import {
  EmployeeDocumentMockService,
  EmployeeSalaryMockService,
  EmploymentHistoryMockService,
} from './employees/employee-extras-mock.services';
import {
  LeaveApplicationMockService,
  LeaveBalanceMockService,
  LeaveCalendarMockService,
  LeavePolicyMockService,
  LeaveTypeMockService,
} from './leave/leave-mock.services';
import {
  BranchMockService,
  CompanyMockService,
  DepartmentMockService,
  JobGradeMockService,
  PositionMockService,
  SectionMockService,
} from './organization/organization-mock.services';
import {
  MyPayslipMockService,
  PayrollRunMockService,
  PayslipMockService,
} from './payroll/payroll-mock.services';
import {
  SalaryAssignmentMockService,
  SalaryComponentMockService,
  SalaryStructureMockService,
} from './payroll/payroll-setup-mock.services';

export interface MockDataOptions {
  /** Start every list empty (except roles, permissions, settings and one admin user). */
  emptyData?: boolean;
  /** Keep created and edited records in the browser across reloads (see persistence.ts). */
  persist?: boolean;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AbstractType<T> = abstract new (...args: any[]) => T;

/** Binds an abstract data contract to its mock singleton. */
function bind<T>(contract: AbstractType<T>, implementation: Type<T>): Provider {
  return { provide: contract, useExisting: implementation };
}

/**
 * Registers the in-memory mock implementations for every data contract.
 * Future phase: add `provideApiDataServices()` with HTTP implementations and
 * swap it in `app.config.ts` — components stay unchanged.
 */
export function provideMockDataServices(options: MockDataOptions = {}): EnvironmentProviders {
  setEmptyData(options.emptyData ?? false);
  setPersistence(options.persist ?? false);
  return makeEnvironmentProviders([
    bind(AuthDataService, AuthMockService),
    bind(DemoDataDataService, DemoDataMockService),
    bind(CompanyDataService, CompanyMockService),
    bind(BranchDataService, BranchMockService),
    bind(DepartmentDataService, DepartmentMockService),
    bind(SectionDataService, SectionMockService),
    bind(JobGradeDataService, JobGradeMockService),
    bind(PositionDataService, PositionMockService),
    bind(EmployeeDataService, EmployeeMockService),
    bind(EmployeeLookupDataService, EmployeeLookupMockService),
    bind(BlacklistDataService, BlacklistMockService),
    bind(EmployeeDocumentDataService, EmployeeDocumentMockService),
    bind(EmploymentHistoryDataService, EmploymentHistoryMockService),
    bind(EmployeeSalaryDataService, EmployeeSalaryMockService),
    bind(ShiftDataService, ShiftMockService),
    bind(HolidayDataService, HolidayMockService),
    bind(AttendanceRecordDataService, AttendanceRecordMockService),
    bind(AttendanceAdjustmentDataService, AttendanceAdjustmentMockService),
    bind(OvertimeDataService, OvertimeMockService),
    bind(AttendanceOverviewDataService, AttendanceOverviewMockService),
    bind(WorkScheduleDataService, WorkScheduleMockService),
    bind(LeaveTypeDataService, LeaveTypeMockService),
    bind(LeaveApplicationDataService, LeaveApplicationMockService),
    bind(LeavePolicyDataService, LeavePolicyMockService),
    bind(LeaveBalanceDataService, LeaveBalanceMockService),
    bind(LeaveCalendarDataService, LeaveCalendarMockService),
    bind(PayrollRunDataService, PayrollRunMockService),
    bind(PayslipDataService, PayslipMockService),
    bind(MyPayslipDataService, MyPayslipMockService),
    bind(SalaryComponentDataService, SalaryComponentMockService),
    bind(SalaryStructureDataService, SalaryStructureMockService),
    bind(SalaryAssignmentDataService, SalaryAssignmentMockService),
    bind(JobOpeningDataService, JobOpeningMockService),
    bind(CandidateDataService, CandidateMockService),
    bind(ApplicationDataService, ApplicationMockService),
    bind(InterviewDataService, InterviewMockService),
    bind(UserDataService, UserMockService),
    bind(RoleDataService, RoleMockService),
    bind(RolePermissionDataService, RolePermissionMockService),
    bind(GeneralSettingsDataService, GeneralSettingsMockService),
    // Excel import/export modules (each module registers itself here).
    { provide: EXCEL_MODULES, multi: true, useFactory: employeeExcelRegistration },
    ...[...ORGANIZATION_EXCEL_REGISTRATIONS, ...OPERATIONS_EXCEL_REGISTRATIONS].map(
      (useFactory) => ({
        provide: EXCEL_MODULES,
        multi: true,
        useFactory,
      }),
    ),
    bind(ExcelModuleCatalogDataService, ExcelCatalogMockService),
    bind(ExcelImportDataService, ExcelImportMockService),
    bind(ExcelExportDataService, ExcelExportMockService),
    bind(DashboardDataService, DashboardMockService),
    bind(ActivityDataService, ActivityMockService),
    bind(NotificationDataService, NotificationMockService),
  ]);
}
