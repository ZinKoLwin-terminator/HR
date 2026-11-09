import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../core/configuration/app-config';
import {
  AttendanceOverviewDataService,
  AttendanceRecordDataService,
  HolidayDataService,
  ShiftDataService,
  WorkScheduleDataService,
} from '../core/services/data/attendance-data.services';
import { BUILT_IN_ADMIN_EMPLOYEE_ID } from '../core/models/settings.models';
import { AuthDataService } from '../core/services/data/auth-data.service';
import { DashboardDataService } from '../core/services/data/dashboard-data.service';
import { EmployeeDataService } from '../core/services/data/employee-data.service';
import { LeaveTypeDataService } from '../core/services/data/leave-data.services';
import { CompanyDataService } from '../core/services/data/organization-data.services';
import { PayrollRunDataService } from '../core/services/data/payroll-data.services';
import { JobOpeningDataService } from '../core/services/data/recruitment-data.services';
import { RoleDataService, UserDataService } from '../core/services/data/settings-data.services';
import { todayIso } from '../shared/utilities/date.util';
import { DEMO_PASSWORD } from './auth/auth-mock.service';
import { provideMockDataServices } from './provide-mock-data-services';

describe('empty-data mode', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices({ emptyData: true }),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
  });

  afterEach(() => {
    // The flag is module state: restore the default for anything that runs after this spec.
    provideMockDataServices();
  });

  it('starts every business list empty', async () => {
    const lists: Observable<unknown[]>[] = [
      TestBed.inject(CompanyDataService).getAll(),
      TestBed.inject(EmployeeDataService).getAll(),
      TestBed.inject(ShiftDataService).getAll(),
      TestBed.inject(HolidayDataService).getAll(),
      TestBed.inject(LeaveTypeDataService).getAll(),
      TestBed.inject(PayrollRunDataService).getAll(),
      TestBed.inject(JobOpeningDataService).getAll(),
    ];
    for (const list of lists) {
      expect(await firstValueFrom(list)).toEqual([]);
    }
  });

  it('keeps roles and exactly one administrator so someone can sign in', async () => {
    const roles = await firstValueFrom(TestBed.inject(RoleDataService).getAll());
    const users = await firstValueFrom(TestBed.inject(UserDataService).getAll());
    expect(roles.length).toBeGreaterThan(0);
    expect(users).toHaveLength(1);

    const signedIn = await firstValueFrom(
      TestBed.inject(AuthDataService).signIn(users[0].email, DEMO_PASSWORD),
    );
    expect(signedIn.roles).toEqual(['HR Administrator']);
  });

  it('links the built-in administrator to no employee, so employee id 2 stays free', async () => {
    const users = await firstValueFrom(TestBed.inject(UserDataService).getAll());
    expect(users[0].employeeId).toBe(BUILT_IN_ADMIN_EMPLOYEE_ID);
  });

  it('renders the dashboard and attendance views without records', async () => {
    const summary = await firstValueFrom(TestBed.inject(DashboardDataService).getSummary());
    expect(summary).toBeDefined();

    const today = todayIso();
    const overview = await firstValueFrom(
      TestBed.inject(AttendanceOverviewDataService).getOverview(today, null),
    );
    expect(overview.summary.scheduled).toBe(0);

    const week = await firstValueFrom(
      TestBed.inject(WorkScheduleDataService).getWeek({
        weekStart: today,
        departmentId: null,
        branchId: null,
      }),
    );
    expect(week.rows).toEqual([]);

    const records = await firstValueFrom(TestBed.inject(AttendanceRecordDataService).getAll());
    expect(records).toEqual([]);
  });
});
