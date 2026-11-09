import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { AdjustmentsPageComponent } from './adjustments/adjustments-page.component';
import {
  AttendanceExceptionsPageComponent,
  AttendanceRecordsPageComponent,
} from './attendance-list/attendance-pages.component';
import { AttendanceOverviewComponent } from './attendance-overview/attendance-overview.component';
import { CheckInComponent } from './check-in/check-in.component';
import { OvertimePageComponent } from './overtime/overtime-page.component';
import { ShiftAssignmentComponent } from './shift-assignment/shift-assignment.component';
import { ShiftsPageComponent } from './shifts/shifts-page.component';
import { WorkSchedulesComponent } from './work-schedules/work-schedules.component';

async function settle(fixture: ComponentFixture<unknown>, rounds = 8) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

async function render<T>(component: Type<T>, rounds?: number) {
  const fixture = TestBed.createComponent(component);
  await settle(fixture, rounds);
  return { fixture, el: fixture.nativeElement as HTMLElement, instance: fixture.componentInstance };
}

const rows = (fixture: ComponentFixture<unknown>) =>
  fixture.debugElement.queryAll(By.css('tr.mat-mdc-row'));

describe('attendance screens', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatDialogModule],
      providers: [
        provideRouter([]),
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
  });

  it('renders the dashboard for the latest working day', async () => {
    const { fixture, el } = await render(AttendanceOverviewComponent, 14);
    expect(el.querySelector('h1')?.textContent).toContain('Attendance dashboard');
    expect(el.querySelectorAll('app-stat-card')).toHaveLength(6);
    expect(el.textContent).toContain('Attendance by department');
    expect(el.textContent).toContain('Late arrivals');
    expect(fixture.debugElement.queryAll(By.css('.people li')).length).toBeGreaterThan(0);
  });

  it('lists attendance records with paging and filters', async () => {
    const { fixture, el, instance } = await render(AttendanceRecordsPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Attendance records');
    expect(rows(fixture)).toHaveLength(10);
    expect(el.querySelector('mat-paginator')?.textContent).toMatch(/1 – 10 of \d+/);

    const list = fixture.debugElement.query(By.css('app-attendance-list'))
      .componentInstance as Record<string, never>;
    (list['setFilter'] as (k: string, v: string) => void).call(list, 'status', 'Late');
    await settle(fixture);
    const cells = rows(fixture).map((r) => r.nativeElement.textContent as string);
    expect(cells.length).toBeGreaterThan(0);
    expect(cells.every((t) => t.includes('Late'))).toBe(true);
    expect(instance).toBeTruthy();
  });

  it('shows late arrivals and early departures separately', async () => {
    const { fixture, el } = await render(AttendanceExceptionsPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Late arrivals and early departures');
    expect(rows(fixture)).toHaveLength(10);
    expect(el.textContent).toContain('Late by');
    expect(
      rows(fixture).every((r) => (r.nativeElement.textContent as string).includes('Late')),
    ).toBe(true);
  });

  it('renders the check-in screen with the shift and recent attendance', async () => {
    const { fixture, el } = await render(CheckInComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Check in / check out');
    expect(el.querySelector('.clock')?.textContent).toMatch(/\d{2}:\d{2}:\d{2}/);
    expect(el.textContent).toContain('General Shift');
    expect(fixture.debugElement.queryAll(By.css('tbody tr'))).toHaveLength(8);
    const buttons = Array.from(el.querySelectorAll<HTMLButtonElement>('button.big'));
    expect(buttons).toHaveLength(2);
    expect(buttons[0].textContent).toContain('Check in');
    expect(buttons[1].textContent).toContain('Check out');
    // At most one of the two actions is available (none on leave or once the day is done).
    expect(buttons.filter((b) => !b.disabled).length).toBeLessThanOrEqual(1);
  });

  it('lists shifts with working days and assigned employee counts', async () => {
    const { fixture, el } = await render(ShiftsPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Shift management');
    expect(rows(fixture)).toHaveLength(5);
    const general = rows(fixture).find((r) =>
      (r.nativeElement.textContent as string).includes('General Shift'),
    )!;
    expect(general.nativeElement.textContent).toContain('09:00 – 18:00');
    expect(general.nativeElement.textContent).toContain('Mon–Fri');
  });

  it('assigns a shift to the selected employees', async () => {
    const { fixture, instance } = await render(ShiftAssignmentComponent);
    expect(rows(fixture)).toHaveLength(10);
    expect(fixture.debugElement.queryAll(By.css('mat-checkbox')).length).toBe(11); // header + 10 rows

    // Select via the UI.
    const headerCheckbox = fixture.debugElement.query(By.css('th mat-checkbox input'))
      .nativeElement as HTMLInputElement;
    headerCheckbox.click();
    await settle(fixture, 3);
    const internals = instance as unknown as {
      selected: { (): ReadonlySet<number>; set(v: ReadonlySet<number>): void };
      targetShiftId: { set(v: number | null): void };
      assign(): void;
    };
    expect(internals.selected().size).toBe(10);

    internals.selected.set(new Set([2, 3]));
    internals.targetShiftId.set(2);
    internals.assign();
    await settle(fixture);

    const employees = TestBed.inject(EmployeeDataService);
    expect((await firstValueFrom(employees.getById(2))).shiftId).toBe(2);
    expect((await firstValueFrom(employees.getById(3))).shiftId).toBe(2);
    expect(internals.selected().size).toBe(0);
  });

  it('shows the weekly schedule grid and moves between weeks', async () => {
    const { fixture, el, instance } = await render(WorkSchedulesComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Work schedules');
    expect(fixture.debugElement.queryAll(By.css('table.schedule tbody tr'))).toHaveLength(58);
    expect(el.querySelectorAll('table.schedule thead th')).toHaveLength(8);

    const before = el.querySelector('.week-label')?.textContent;
    (instance as unknown as { shiftWeek(n: number): void }).shiftWeek(1);
    await settle(fixture, 4);
    expect(el.querySelector('.week-label')?.textContent).not.toBe(before);
  });

  it('lists overtime and adjustment requests with approval actions', async () => {
    const overtime = await render(OvertimePageComponent);
    expect(overtime.el.querySelector('h1')?.textContent).toContain('Overtime');
    expect(rows(overtime.fixture)).toHaveLength(10);
    expect(overtime.el.textContent).toContain('Pending');

    const adjustments = await render(AdjustmentsPageComponent);
    expect(adjustments.el.querySelector('h1')?.textContent).toContain('Attendance adjustments');
    expect(rows(adjustments.fixture).length).toBeGreaterThanOrEqual(8);
    expect(adjustments.el.textContent).toContain('Recorded');
    expect(adjustments.el.textContent).toContain('Requested');
  });
});
