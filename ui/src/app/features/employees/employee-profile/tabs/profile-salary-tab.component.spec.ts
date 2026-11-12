import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/models/common.models';
import { EmployeeSalarySummary } from '../../../../core/models/employee-extras.models';
import { EmployeeSalaryDataService } from '../../../../core/services/data/employee-extras-data.services';
import { ProfileSalaryTabComponent } from './profile-salary-tab.component';

const SUMMARY: EmployeeSalarySummary = {
  employeeId: 7,
  basic: 1000,
  allowances: 200,
  gross: 1200,
  deductions: 100,
  net: 1100,
  gradeCode: 'G3',
  gradeName: 'Associate',
  bandMin: 800,
  bandMax: 1600,
  bandPosition: 25,
  assumptions: ['Sample rule for the test'],
};

async function render(result: Observable<EmployeeSalarySummary>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: EmployeeSalaryDataService, useValue: { getSummary: () => result } },
    ],
  });
  const fixture = TestBed.createComponent(ProfileSalaryTabComponent);
  fixture.componentRef.setInput('employeeId', 7);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('ProfileSalaryTabComponent', () => {
  it('shows the server message in an alert when the summary cannot be loaded', async () => {
    const error: AppError = {
      code: 'NotFound',
      message: 'No salary is assigned to this employee.',
    };
    const el = await render(throwError(() => error));

    const alert = el.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('No salary is assigned to this employee.');
    expect(el.textContent).not.toContain('Monthly salary');
  });

  it('shows the salary figures and no alert on success', async () => {
    const el = await render(of(SUMMARY));

    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(el.textContent).toContain('Monthly salary (sample)');
    expect(el.textContent).toContain('Grade G3');
    expect(el.textContent).toContain('Sample rule for the test');
    expect(el.textContent).toContain('25%');
  });
});
