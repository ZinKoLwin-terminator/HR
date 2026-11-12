import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { CurrentUser, CurrentUserService } from '../../../core/services/current-user.service';
import { EmployeeDocumentDataService } from '../../../core/services/data/employee-extras-data.services';
import { MOCK_EMPLOYEES } from '../../../mock-data/employees/employees.mock';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import { OrganizationLookupService } from '../../organization/organization-lookups';
import { firstValueFrom } from 'rxjs';
import { ProfilePrintComponent } from './profile-print.component';

function user(permissions: string[]): CurrentUser {
  return {
    id: 1,
    employeeId: null,
    displayName: 'Test User',
    email: 'test@example.test',
    jobTitle: '',
    roles: ['Tester'],
    permissions,
  };
}

async function render(permissions: string[]) {
  const current: Signal<CurrentUser> = signal(user(permissions));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current } },
    ],
  });
  const documents = TestBed.inject(EmployeeDocumentDataService);
  const querySpy = vi.spyOn(documents, 'query');
  const org = await firstValueFrom(TestBed.inject(OrganizationLookupService).load());
  const fixture = TestBed.createComponent(ProfilePrintComponent);
  const ready = vi.fn();
  fixture.componentInstance.ready.subscribe(ready);
  fixture.componentRef.setInput('employee', MOCK_EMPLOYEES[0]);
  fixture.componentRef.setInput('org', org);
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  return { text: (fixture.nativeElement as HTMLElement).textContent ?? '', querySpy, ready };
}

describe('ProfilePrintComponent', () => {
  it('prints employment details only when the role has no personal or bank access', async () => {
    const { text, querySpy, ready } = await render(['employees:View']);
    expect(text).toContain('Employment');
    expect(text).toContain(MOCK_EMPLOYEES[0].employeeCode);
    expect(text).not.toContain('Personal information');
    expect(text).not.toContain('National ID');
    expect(text).not.toContain(MOCK_EMPLOYEES[0].nationalId);
    expect(text).not.toContain('Bank information');
    expect(text).not.toContain(MOCK_EMPLOYEES[0].bank.accountNumber);
    expect(text).not.toContain('Documents');
    // Hidden data is not even requested.
    expect(querySpy).not.toHaveBeenCalled();
    expect(ready).toHaveBeenCalledTimes(1);
  });

  it('adds personal data and the document list with ViewSensitive, bank with ViewBank', async () => {
    const sensitive = await render(['employees:View', 'employees:ViewSensitive']);
    expect(sensitive.text).toContain('Personal information');
    expect(sensitive.text).toContain(MOCK_EMPLOYEES[0].nationalId);
    expect(sensitive.text).toContain('Documents');
    expect(sensitive.text).not.toContain('Bank information');
    expect(sensitive.querySpy).toHaveBeenCalledTimes(1);
    expect(sensitive.ready).toHaveBeenCalledTimes(1);
    TestBed.resetTestingModule();

    const bank = await render(['employees:View', 'employees:ViewBank']);
    expect(bank.text).toContain('Bank information');
    expect(bank.text).not.toContain('Personal information');
    // Only the last four digits are ever printed.
    const full = MOCK_EMPLOYEES[0].bank.accountNumber;
    expect(bank.text).toContain(`•••• •••• ${full.slice(-4)}`);
    expect(bank.text).not.toContain(full);
  });
});
