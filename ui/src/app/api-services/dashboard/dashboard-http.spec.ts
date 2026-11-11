import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import {
  ActivityDataService,
  NotificationDataService,
} from '../../core/services/data/activity-data.service';
import { DashboardDataService } from '../../core/services/data/dashboard-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { provideApiDataServices } from '../provide-api-data-services';
import { ActivityHttpService } from '../activity/activity-http.service';
import { NotificationHttpService } from '../activity/notification-http.service';
import { DashboardHttpService } from './dashboard-http.service';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      provideApiDataServices({ enabled: true }),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  http
    .match((r) => r.url.endsWith('/auth/me'))
    .forEach((r) => r.flush(null, { status: 401, statusText: 'Unauthorized' }));
  return http;
}

async function rejection(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

const growth = Array.from({ length: 12 }, (_, i) => ({
  month: `2026-${String(i + 1).padStart(2, '0')}`,
  headcount: 100 + i,
  joiners: 2,
  exits: 1,
}));

function summary(payroll: object | null) {
  return {
    generatedAt: '2026-11-10T08:00:00Z',
    headcount: {
      total: 10,
      active: 9,
      probation: 1,
      newThisMonth: 1,
      onLeaveToday: 2,
      exitsThisMonth: 0,
    },
    attendanceToday: {
      date: '2026-11-10',
      scheduled: 9,
      present: 6,
      late: 1,
      absent: 1,
      onLeave: 1,
      halfDay: 0,
      attendanceRate: 77.8,
    },
    pendingLeaveApprovals: 1,
    pendingLeaves: [
      {
        id: 3,
        employeeName: 'Test Person',
        leaveTypeName: 'Annual',
        startDate: '2026-11-12',
        endDate: '2026-11-13',
        days: 2,
        appliedOn: '2026-11-08',
      },
    ],
    payroll,
    recruitment: {
      openJobs: 2,
      openPositions: 3,
      activeCandidates: 4,
      upcomingInterviews: 1,
      funnel: [{ label: 'Applied', count: 4 }],
    },
    departmentDistribution: [{ label: 'Operations', count: 5 }],
    attendanceTrend: [{ date: '2026-11-10', present: 6, late: 1, absent: 1, onLeave: 1 }],
    headcountGrowth: growth,
    recentActivities: [],
    upcomingEvents: [
      { date: '2026-11-15', title: 'Holiday', subtitle: 'Fictional', type: 'Holiday' },
    ],
  };
}

describe('Dashboard, activity and notification HTTP adapters', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('replace the mocks only when API mode is on', () => {
    setup();
    expect(TestBed.inject(DashboardDataService)).toBeInstanceOf(DashboardHttpService);
    expect(TestBed.inject(ActivityDataService)).toBeInstanceOf(ActivityHttpService);
    expect(TestBed.inject(NotificationDataService)).toBeInstanceOf(NotificationHttpService);
  });

  describe('dashboard', () => {
    it('loads the summary with one call and keeps the 12-month series', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(DashboardDataService).getSummary());
      const payroll = {
        periodName: 'Oct 2026',
        status: 'Paid',
        payDate: '2026-10-31',
        employeeCount: 9,
        totalGross: 100,
        totalDeductions: 20,
        totalNet: 80,
        netChangePercent: 1.5,
      };
      http.expectOne('/api/dashboard/summary').flush(summary(payroll));
      const result = await promise;
      expect(result.payroll).toEqual(payroll);
      expect(result.headcountGrowth).toHaveLength(12);
      expect(result.attendanceToday.date).toBe('2026-11-10');
      expect(result.pendingLeaves[0].startDate).toBe('2026-11-12');
    });

    it('keeps a null payroll as null', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(DashboardDataService).getSummary());
      http.expectOne('/api/dashboard/summary').flush(summary(null));
      expect((await promise).payroll).toBeNull();
    });

    it('maps 403 to a readable error', async () => {
      const http = setup();
      const promise = rejection(firstValueFrom(TestBed.inject(DashboardDataService).getSummary()));
      http
        .expectOne('/api/dashboard/summary')
        .flush({ detail: 'No access.' }, { status: 403, statusText: 'Forbidden' });
      expect((await promise).code).toBe('Forbidden');
    });
  });

  describe('activity', () => {
    it('builds the query with filters, date range, supported sort and search', async () => {
      const http = setup();
      const promise = firstValueFrom(
        (TestBed.inject(ActivityDataService) as ActivityHttpService).query({
          pageIndex: 2,
          pageSize: 25,
          sort: { active: 'actor', direction: 'asc' },
          search: ' payroll ',
          filters: {
            category: 'leave',
            actor: 'Test Admin',
            dateFrom: '2026-11-01',
            dateTo: 'all',
          },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/activity/query');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('sort')).toBe('actor:asc');
      expect(req.request.params.get('search')).toBe('payroll');
      expect(req.request.params.get('category')).toBe('leave');
      expect(req.request.params.get('actor')).toBe('Test Admin');
      expect(req.request.params.get('dateFrom')).toBe('2026-11-01');
      expect(req.request.params.has('dateTo')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 2, pageSize: 25 });
      await promise;
    });

    it('sends no sort for an unsupported key so the timestamp default applies', async () => {
      const http = setup();
      const promise = firstValueFrom(
        (TestBed.inject(ActivityDataService) as ActivityHttpService).query({
          pageIndex: 0,
          pageSize: 10,
          sort: { active: 'message', direction: 'desc' },
        }),
      );
      const req = http.expectOne((r) => r.url === '/api/activity/query');
      expect(req.request.params.has('sort')).toBe(false);
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
      await promise;
    });

    it('maps getRecent to pageSize and returns the items', async () => {
      const http = setup();
      const entry = {
        id: 9,
        category: 'system',
        message: 'Seeded',
        actor: 'System',
        timestamp: '2026-11-10T08:00:00Z',
      };
      const promise = firstValueFrom(TestBed.inject(ActivityDataService).getRecent(6));
      const req = http.expectOne((r) => r.url === '/api/activity/query');
      expect(req.request.params.get('pageSize')).toBe('6');
      expect(req.request.params.get('pageIndex')).toBe('0');
      req.flush({ items: [entry], total: 1, pageIndex: 0, pageSize: 6 });
      expect(await promise).toEqual([entry]);
    });

    it('is read only: only GET requests are made', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(ActivityDataService).getRecent(3));
      const req = http.expectOne((r) => r.url === '/api/activity/query');
      expect(req.request.method).toBe('GET');
      req.flush({ items: [], total: 0, pageIndex: 0, pageSize: 3 });
      await promise;
      http.expectNone((r) => r.method !== 'GET');
    });
  });

  describe('notifications', () => {
    const note = {
      id: 5,
      title: 'Leave approved',
      message: 'Fictional',
      icon: 'event',
      link: null,
      timestamp: '2026-11-10T08:00:00Z',
      read: false,
    };

    it('lists with pageSize 200 and returns the items', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(NotificationDataService).getNotifications());
      const req = http.expectOne((r) => r.url === '/api/notifications/query');
      expect(req.request.params.get('pageSize')).toBe('200');
      expect(req.request.params.get('pageIndex')).toBe('0');
      req.flush({ items: [note], total: 1, pageIndex: 0, pageSize: 200 });
      expect(await promise).toEqual([note]);
    });

    it('marks one as read with a POST and no body', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(NotificationDataService).markAsRead(5));
      const req = http.expectOne('/api/notifications/5/read');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBeNull();
      req.flush({ ...note, read: true });
      expect(await promise).toBeUndefined();
    });

    it('marks all as read with a POST and no body', async () => {
      const http = setup();
      const promise = firstValueFrom(TestBed.inject(NotificationDataService).markAllAsRead());
      const req = http.expectOne('/api/notifications/read-all');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBeNull();
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(await promise).toBeUndefined();
    });

    it("maps 404 for another user's notification to a readable error", async () => {
      const http = setup();
      const promise = rejection(
        firstValueFrom(TestBed.inject(NotificationDataService).markAsRead(77)),
      );
      http
        .expectOne('/api/notifications/77/read')
        .flush({ detail: 'Notification not found.' }, { status: 404, statusText: 'Not Found' });
      const error = await promise;
      expect(error.code).toBe('NotFound');
      expect(error.message).toBeTruthy();
    });
  });
});
