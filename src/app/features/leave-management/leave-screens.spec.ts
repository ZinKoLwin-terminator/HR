import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import {
  LeaveApplicationsPageComponent,
  LeaveApprovalsPageComponent,
} from './leave-applications/leave-pages.component';
import { LeaveBalancesComponent } from './leave-balances/leave-balances.component';
import { LeaveCalendarComponent } from './leave-calendar/leave-calendar.component';
import { LeavePoliciesPageComponent } from './leave-policies/leave-policies-page.component';
import { LeaveTypesPageComponent } from './leave-types/leave-types-page.component';

async function settle(fixture: ComponentFixture<unknown>, rounds = 8) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

async function render<T>(
  component: Type<T>,
  inputs: Record<string, unknown> = {},
  rounds?: number,
) {
  const fixture = TestBed.createComponent(component);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  await settle(fixture, rounds);
  return { fixture, el: fixture.nativeElement as HTMLElement, instance: fixture.componentInstance };
}

const rows = (fixture: ComponentFixture<unknown>) =>
  fixture.debugElement.queryAll(By.css('tr.mat-mdc-row'));
const text = (node: { nativeElement: HTMLElement }) => node.nativeElement.textContent ?? '';

describe('leave screens', () => {
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

  it('lists leave types with entitlements and usage', async () => {
    const { fixture, el } = await render(LeaveTypesPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Leave types');
    expect(rows(fixture)).toHaveLength(7);
    const annual = rows(fixture).find((r) => text(r).includes('Annual Leave'))!;
    expect(text(annual)).toContain('Paid');
    expect(text(annual)).toContain('Up to 5 days');
    const maternity = rows(fixture).find((r) => text(r).includes('Maternity'))!;
    expect(text(maternity)).toContain('Women only');
  });

  it('lists leave policies with their targeting and accrual', async () => {
    const { fixture, el } = await render(LeavePoliciesPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Leave policies');
    expect(rows(fixture)).toHaveLength(9);
    const standard = rows(fixture).find((r) => text(r).includes('standard (after 3 months)'))!;
    expect(text(standard)).toContain('3 mo');
    expect(text(standard)).toContain('All grades');
    expect(text(rows(fixture).find((r) => text(r).includes('monthly accrual'))!)).toContain(
      'Monthly',
    );
  });

  it('shows applications with their workflow state and row actions', async () => {
    const { fixture, el } = await render(LeaveApplicationsPageComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Leave applications');
    expect(rows(fixture)).toHaveLength(10);
    expect(el.textContent).toContain('Leave from');
    expect(el.textContent).toContain('Leave to');
    expect(el.textContent).toMatch(/LV-\d{4}-\d{4}/);
    expect(el.querySelector('button[mat-flat-button]')?.textContent).toContain(
      'Add leave application',
    );
  });

  it('shows the pending queue and decision history on the approvals screen', async () => {
    const pending = await render(LeaveApprovalsPageComponent);
    expect(pending.el.querySelector('h1')?.textContent).toContain('Leave approvals');
    expect(rows(pending.fixture)).toHaveLength(7); // seeded submitted requests
    expect(rows(pending.fixture).every((r) => text(r).includes('Submitted'))).toBe(false); // no status column in the queue
    expect(pending.el.textContent).not.toContain('Add leave application');

    const history = await render(LeaveApprovalsPageComponent, { tab: 'history' });
    expect(rows(history.fixture).length).toBeGreaterThan(5);
    const statuses = rows(history.fixture).map(text);
    expect(statuses.every((t) => /Approved|Rejected|Cancelled/.test(t))).toBe(true);
    expect(statuses.some((t) => t.includes('Rejected'))).toBe(true);
    expect(history.el.textContent).toContain('Decided by');
  });

  it('shows balances per employee as available / entitled', async () => {
    const { fixture, el } = await render(LeaveBalancesComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Leave balances');
    expect(rows(fixture)).toHaveLength(10);
    expect(el.querySelector('mat-paginator')?.textContent).toContain('1 – 10 of 58');
    const first = text(rows(fixture)[0]);
    expect(first).toContain('Thura Aung');
    expect(first).toContain('/ 25'); // executive annual leave entitlement
    expect(el.textContent).toContain('Annual Leave');
    expect(el.textContent).not.toContain('Maternity Leave'); // gender-specific types appear in the details only
  });

  it('draws the calendar for the current month with leave and a compact list', async () => {
    const { fixture, el } = await render(LeaveCalendarComponent);
    expect(el.querySelector('h1')?.textContent).toContain('Leave calendar');
    expect(el.querySelectorAll('table.grid thead th')).toHaveLength(7);
    expect(
      fixture.debugElement.queryAll(By.css('table.grid tbody tr')).length,
    ).toBeGreaterThanOrEqual(5);
    expect(fixture.debugElement.queryAll(By.css('.chip')).length).toBeGreaterThan(3);
    expect(fixture.debugElement.queryAll(By.css('.agenda__day')).length).toBeGreaterThan(0);
    expect(el.querySelector('.month-label')?.textContent?.trim()).toMatch(/^[A-Z][a-z]+ \d{4}$/);
  });
});
