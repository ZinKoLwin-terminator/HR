import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    }).compileComponents();
  });

  it('renders KPI cards, widgets and a greeting from mock data', async () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 20));
    fixture.detectChanges();
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toMatch(/Good (morning|afternoon|evening), Su/);
    expect(el.querySelectorAll('app-stat-card')).toHaveLength(5);
    expect(el.textContent).toContain('Total employees');
    expect(el.querySelector('app-payroll-summary-card')?.textContent).toContain('Sample data');
    expect(el.querySelectorAll('app-pending-leaves-card li').length).toBe(5);
  });
});
