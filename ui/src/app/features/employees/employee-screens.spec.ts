import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { EmployeeDataService } from '../../core/services/data/employee-data.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { EmployeeFormPageComponent } from './employee-form/employee-form-page.component';
import { EmployeeFormComponent } from './employee-form/employee-form.component';
import { EmployeeListComponent } from './employee-list/employee-list.component';
import { EmployeeProfileComponent } from './employee-profile/employee-profile.component';

async function settle(fixture: ComponentFixture<unknown>, rounds = 5) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

function configure() {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
    ],
  });
}

describe('employee list', () => {
  beforeEach(configure);

  it('shows the first page of employees with paging info', async () => {
    const fixture = TestBed.createComponent(EmployeeListComponent);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(fixture.debugElement.queryAll(By.css('tr.mat-mdc-row'))).toHaveLength(10);
    expect(el.querySelector('mat-paginator')?.textContent).toContain('1 – 10 of 60');
    expect(el.textContent).toContain('Thura Aung');
    expect(el.querySelector('a[href="/employees/new"]')?.textContent).toContain('Add employee');
  });

  it('filters by the search input (code or name)', async () => {
    const fixture = TestBed.createComponent(EmployeeListComponent);
    fixture.componentRef.setInput('search', 'carter');
    await settle(fixture);
    const rows = fixture.debugElement.queryAll(By.css('tr.mat-mdc-row'));
    expect(rows).toHaveLength(1);
    expect(rows[0].nativeElement.textContent).toContain('Daniel Carter');
  });
});

describe('employee profile', () => {
  beforeEach(configure);

  async function render(id: string, tab?: string) {
    const fixture = TestBed.createComponent(EmployeeProfileComponent);
    fixture.componentRef.setInput('id', id);
    if (tab) fixture.componentRef.setInput('tab', tab);
    await settle(fixture, 6);
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the header summary and overview', async () => {
    const el = await render('1');
    expect(el.querySelector('h1')?.textContent).toContain('Thura Aung');
    expect(el.textContent).toContain('Chief Executive Officer');
    expect(el.textContent).toContain('EMP001');
    expect(el.textContent).toContain('Personal information');
    // Bank number is masked until revealed.
    expect(el.textContent).toContain('••••');
  });

  it.each([
    ['employment', 'Reporting manager'],
    ['documents', 'National ID'],
    ['history', 'Employment history'],
    ['attendance', 'Attendance rate'],
    ['leave', 'Leave balances'],
    ['salary', 'sample data'],
  ])('renders the %s tab', async (tab, text) => {
    const el = await render('5', tab);
    expect(el.textContent, tab).toContain(text);
  });

  it('shows a not-found state for unknown employees', async () => {
    const el = await render('999');
    expect(el.textContent).toContain('Employee not found');
  });
});

describe('employee form', () => {
  beforeEach(configure);

  async function openForm(id?: string) {
    const fixture = TestBed.createComponent(EmployeeFormPageComponent);
    if (id) fixture.componentRef.setInput('id', id);
    await settle(fixture, 6);
    const form = fixture.debugElement.query(By.directive(EmployeeFormComponent));
    // Protected members are reachable through element access in tests.
    const instance = form.componentInstance as EmployeeFormComponent & Record<string, never>;
    return { fixture, instance, el: fixture.nativeElement as HTMLElement };
  }

  interface FormInternals {
    form: import('@angular/forms').FormGroup;
    submit(): void;
    hasUnsavedChanges(): boolean;
  }
  const internals = (i: unknown) => i as FormInternals;

  it('renders all sections and suggests defaults when adding', async () => {
    const { instance, el } = await openForm();
    const form = internals(instance).form;
    expect(form.getRawValue()['employeeCode']).toBe('EMP061');
    expect(form.getRawValue()['employmentStatus']).toBe('Probation');
    for (const title of [
      'Personal information',
      'Contact information',
      'Address information',
      'Emergency contact',
      'Employment information',
      'Organization assignment',
      'Bank information',
      'Profile photo',
      'Documents',
    ]) {
      expect(el.textContent, title).toContain(title);
    }
  });

  it('pre-fills and locks the code when editing', async () => {
    const { instance } = await openForm('1');
    const form = internals(instance).form;
    expect(form.getRawValue()['firstName']).toBe('Thura');
    expect(form.controls['employeeCode'].disabled).toBe(true);
    expect(internals(instance).hasUnsavedChanges()).toBe(false);
    form.controls['firstName'].markAsDirty();
    expect(internals(instance).hasUnsavedChanges()).toBe(true);
  });

  it('cascades dependent dropdowns and suggests the job grade', async () => {
    const { instance } = await openForm();
    const form = internals(instance).form;
    form.patchValue({ companyId: 1 });
    form.patchValue({ branchId: 1 });
    form.patchValue({ departmentId: 4 });
    form.patchValue({ positionId: 11 });
    expect(form.controls['jobGradeId'].value).toBe(4); // Senior Frontend Engineer → G4

    // Switching company clears branch, department and position.
    form.patchValue({ companyId: 2 });
    expect(form.controls['branchId'].value).toBeNull();
    expect(form.controls['departmentId'].value).toBeNull();
    expect(form.controls['positionId'].value).toBeNull();
  });

  it('validates dates, age and exit date rules', async () => {
    const { instance } = await openForm();
    const form = internals(instance).form;

    form.patchValue({ dateOfBirth: '2020-01-01' });
    expect(form.controls['dateOfBirth'].errors).toHaveProperty('minAge');

    form.patchValue({ joiningDate: '2026-05-01', probationEndDate: '2026-04-01' });
    expect(form.controls['probationEndDate'].errors).toHaveProperty('dateRange');
    form.patchValue({ probationEndDate: '2026-08-01' });
    expect(form.controls['probationEndDate'].errors).toBeNull();

    form.patchValue({ employmentStatus: 'Resigned' });
    expect(form.controls['exitDate'].errors).toHaveProperty('required');
    form.patchValue({ exitDate: '2026-10-01' });
    expect(form.controls['exitDate'].errors).toBeNull();

    form.patchValue({ nationalId: '', phone: 'abc', bankAccountNumber: '12ab' });
    expect(form.controls['nationalId'].errors).toHaveProperty('required');
    expect(form.controls['phone'].errors).toHaveProperty('phone');
    expect(form.controls['bankAccountNumber'].errors).toBeTruthy();
  });

  it('refuses to save an invalid form and saves a valid one', async () => {
    const { fixture, instance, el } = await openForm();
    const api = TestBed.inject(EmployeeDataService);
    const form = internals(instance).form;

    internals(instance).submit();
    fixture.detectChanges();
    expect(el.querySelector('.form-banner')?.textContent).toMatch(/Please correct \d+ fields?/);
    expect((await firstValueFrom(api.getAll())).length).toBe(60);

    form.patchValue({
      firstName: 'Nilar',
      lastName: 'Htay',
      gender: 'Female',
      dateOfBirth: '1996-04-12',
      maritalStatus: 'Single',
      nationalId: '12/KaMaYa(N)123456',
      workEmail: 'nilar.htay@apexhorizon.example',
      phone: '+95 9 123 456 789',
      addressLine1: 'No. 5, Parami Road',
      city: 'Yangon',
      emergencyName: 'Htay Win',
      emergencyRelationship: 'Father',
      emergencyPhone: '+95 9 987 654 321',
      companyId: 1,
    });
    form.patchValue({ branchId: 1 });
    form.patchValue({ departmentId: 2 });
    form.patchValue({ positionId: 6, managerId: 3 });
    form.patchValue({
      bankName: 'KBZ Bank',
      bankAccountName: 'NILAR HTAY',
      bankAccountNumber: '123456789012',
    });
    expect(
      form.valid,
      JSON.stringify(form.errors) +
        Object.entries(form.controls)
          .filter(([, c]) => c.invalid)
          .map(([k]) => k),
    ).toBe(true);

    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    internals(instance).submit();
    await settle(fixture, 6);

    const all = await firstValueFrom(api.getAll());
    expect(all).toHaveLength(61);
    const created = all.find((e) => e.employeeCode === 'EMP061')!;
    expect(created.firstName).toBe('Nilar');
    expect(created.address.city).toBe('Yangon');
    expect(created.jobGradeId).toBe(2); // HR Executive → G2
    expect(created.employmentStatus).toBe('Probation');
    expect(navigate).toHaveBeenCalledWith(['/employees', created.id]);
    expect(internals(instance).hasUnsavedChanges()).toBe(false);
  });

  it('maps duplicate work email back onto the field', async () => {
    const { fixture, instance } = await openForm('2');
    const form = internals(instance).form;
    form.patchValue({ workEmail: 'thura.aung@apexhorizon.example' });
    internals(instance).submit();
    await settle(fixture);
    expect(form.controls['workEmail'].errors).toHaveProperty('server');
  });
});
