import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LeaveBalance } from '../../../../core/models/leave.models';
import {
  LeaveApplicationDataService,
  LeaveBalanceDataService,
  LeaveTypeDataService,
} from '../../../../core/services/data/leave-data.services';
import { ProfileLeaveTabComponent } from './profile-leave-tab.component';

const THIS_YEAR = new Date().getFullYear();
const NEXT_YEAR = THIS_YEAR + 1;

const annual = (taken: number): LeaveBalance => ({
  leaveTypeId: 1,
  code: 'AL',
  name: 'Annual Leave',
  color: '#3f6fd8',
  entitled: 15,
  taken,
  pending: 0,
  available: 15 - taken,
});

async function render() {
  // This year has no leave taken; next year has the 5 days of a leave applied for in advance.
  const getForEmployee = vi.fn((_id: number, year: number) =>
    of([annual(year === NEXT_YEAR ? 5 : 0)]),
  );
  const query = vi.fn(() => of({ items: [], total: 0, pageIndex: 0, pageSize: 200 }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: LeaveTypeDataService, useValue: { getAll: () => of([]) } },
      { provide: LeaveApplicationDataService, useValue: { query } },
      { provide: LeaveBalanceDataService, useValue: { getForEmployee } },
    ],
  });
  const fixture = TestBed.createComponent(ProfileLeaveTabComponent);
  fixture.componentRef.setInput('employeeId', 6);
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, getForEmployee, query };
}

const annualRow = (el: HTMLElement) =>
  [...el.querySelectorAll('tr')].find((r) => r.textContent?.includes('Annual Leave'))!;

describe('ProfileLeaveTabComponent', () => {
  it("shows this year's balances first", async () => {
    const { fixture, getForEmployee } = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(getForEmployee).toHaveBeenCalledWith(6, THIS_YEAR);
    expect(el.querySelector('#lv-balance')?.textContent).toContain(String(THIS_YEAR));
    expect(annualRow(el).textContent).toMatch(/15\s*0\s*0\s*15/);
  });

  it('switches to next year, where leave that starts next year is counted', async () => {
    const { fixture, getForEmployee, query } = await render();
    const el = fixture.nativeElement as HTMLElement;
    const component = fixture.componentInstance as unknown as {
      yearOptions: number[];
      year: { set(value: number): void };
    };
    expect(component.yearOptions).toEqual([NEXT_YEAR, THIS_YEAR, THIS_YEAR - 1]);

    component.year.set(NEXT_YEAR);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getForEmployee).toHaveBeenLastCalledWith(6, NEXT_YEAR);
    expect(el.querySelector('#lv-balance')?.textContent).toContain(String(NEXT_YEAR));
    expect(annualRow(el).textContent).toMatch(/15\s*5\s*0\s*10/);
    // Changing the year reloads the balances only, not the leave history.
    expect(query).toHaveBeenCalledTimes(1);
  });
});
