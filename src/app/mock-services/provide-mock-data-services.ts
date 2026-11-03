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
import { DashboardDataService } from '../core/services/data/dashboard-data.service';
import { EmployeeDataService } from '../core/services/data/employee-data.service';
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
import { PayrollRunMockService, PayslipMockService } from './payroll/payroll-mock.services';
import {
  SalaryAssignmentMockService,
  SalaryComponentMockService,
  SalaryStructureMockService,
} from './payroll/payroll-setup-mock.services';

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
export function provideMockDataServices(): EnvironmentProviders {
  return makeEnvironmentProviders([
    bind(CompanyDataService, CompanyMockService),
    bind(BranchDataService, BranchMockService),
    bind(DepartmentDataService, DepartmentMockService),
    bind(SectionDataService, SectionMockService),
    bind(JobGradeDataService, JobGradeMockService),
    bind(PositionDataService, PositionMockService),
    bind(EmployeeDataService, EmployeeMockService),
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
    bind(ExcelModuleCatalogDataService, ExcelCatalogMockService),
    bind(ExcelImportDataService, ExcelImportMockService),
    bind(ExcelExportDataService, ExcelExportMockService),
    bind(DashboardDataService, DashboardMockService),
    bind(ActivityDataService, ActivityMockService),
    bind(NotificationDataService, NotificationMockService),
  ]);
}
