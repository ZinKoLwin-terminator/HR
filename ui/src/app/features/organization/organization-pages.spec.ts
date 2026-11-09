import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import {
  BranchDataService,
  CompanyDataService,
  DepartmentDataService,
  JobGradeDataService,
  PositionDataService,
  SectionDataService,
} from '../../core/services/data/organization-data.services';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { BranchesPageComponent } from './branches/branches-page.component';
import { CompaniesPageComponent } from './companies/companies-page.component';
import { DepartmentsPageComponent } from './departments/departments-page.component';
import { JobGradesPageComponent } from './job-grades/job-grades-page.component';
import { PositionsPageComponent } from './positions/positions-page.component';
import { ReportingStructureComponent } from './reporting-structure/reporting-structure.component';
import { SectionsPageComponent } from './sections/sections-page.component';

const PAGES = [
  { name: 'companies', component: CompaniesPageComponent, heading: 'Companies', rows: 3 },
  { name: 'branches', component: BranchesPageComponent, heading: 'Branches', rows: 6 },
  { name: 'departments', component: DepartmentsPageComponent, heading: 'Departments', rows: 10 },
  { name: 'sections', component: SectionsPageComponent, heading: 'Sections', rows: 10 },
  { name: 'positions', component: PositionsPageComponent, heading: 'Positions', rows: 10 },
  { name: 'job grades', component: JobGradesPageComponent, heading: 'Job grades', rows: 9 },
] as const;

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

async function expectError(promise: Promise<unknown>, code: AppError['code']): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    expect((error as AppError).code).toBe(code);
    return error as AppError;
  }
  throw new Error(`Expected ${code}`);
}

describe('organization screens', () => {
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

  it.each(PAGES)('renders the $name list from mock data', async ({ component, heading, rows }) => {
    const fixture = TestBed.createComponent(component as Type<unknown>);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toContain(heading);
    expect(fixture.debugElement.queryAll(By.css('tr.mat-mdc-row')).length).toBe(rows);
    expect(el.querySelector('button[mat-flat-button]')?.textContent).toContain('Add');
  });

  it('renders the reporting structure with a single top-level executive', async () => {
    const fixture = TestBed.createComponent(ReportingStructureComponent);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('ul.tree > li')).toHaveLength(1);
    expect(el.textContent).toContain('Thura Aung');
    expect(el.textContent).toContain('People in structure');
  });
});

describe('organization data rules', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideMockDataServices(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      ],
    });
  });

  it('blocks deleting organization records that are still referenced', async () => {
    const companies = TestBed.inject(CompanyDataService);
    const error = await expectError(firstValueFrom(companies.delete(1)), 'Conflict');
    expect(error.message).toMatch(/branch\(es\).*department\(s\).*employee\(s\)/);

    await expectError(firstValueFrom(TestBed.inject(BranchDataService).delete(1)), 'Conflict');
    await expectError(firstValueFrom(TestBed.inject(DepartmentDataService).delete(2)), 'Conflict');
    await expectError(firstValueFrom(TestBed.inject(SectionDataService).delete(1)), 'Conflict');
    await expectError(firstValueFrom(TestBed.inject(JobGradeDataService).delete(1)), 'Conflict');
    await expectError(firstValueFrom(TestBed.inject(PositionDataService).delete(1)), 'Conflict');
  });

  it('allows deleting an unused company after it is created', async () => {
    const companies = TestBed.inject(CompanyDataService);
    const template = await firstValueFrom(companies.getById(3));
    const { id: _unused, ...model } = template;
    void _unused;
    const created = await firstValueFrom(
      companies.create({ ...model, code: 'TMP', name: 'Temp Co' }),
    );
    await firstValueFrom(companies.delete(created.id));
    await expectError(firstValueFrom(companies.getById(created.id)), 'NotFound');
  });

  it('rejects a branch that belongs to a different company than the department', async () => {
    const departments = TestBed.inject(DepartmentDataService);
    const error = await expectError(
      firstValueFrom(departments.update(9, { companyId: 1 })), // branch 4 belongs to company 2
      'Validation',
    );
    expect(error.fieldErrors?.['branchId']).toBeTruthy();
  });

  it('rejects a section from another department and a self-reporting position', async () => {
    const positions = TestBed.inject(PositionDataService);
    const section = await expectError(
      firstValueFrom(positions.update(4, { sectionId: 6 })), // section 6 is in Engineering
      'Validation',
    );
    expect(section.fieldErrors?.['sectionId']).toBeTruthy();
    const self = await expectError(
      firstValueFrom(positions.update(4, { reportsToPositionId: 4 })),
      'Validation',
    );
    expect(self.fieldErrors?.['reportsToPositionId']).toBeTruthy();
  });

  it('enforces job grade salary ordering and unique codes', async () => {
    const grades = TestBed.inject(JobGradeDataService);
    const order = await expectError(
      firstValueFrom(grades.update(1, { minSalary: 900, maxSalary: 500 })),
      'Validation',
    );
    expect(order.fieldErrors?.['maxSalary']).toBeTruthy();
    await expectError(firstValueFrom(grades.update(2, { code: 'G1' })), 'Validation');
  });
});
