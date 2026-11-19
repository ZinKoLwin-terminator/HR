import { Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { NAVIGATION, visibleNavigation } from '../../core/constants/navigation';
import { GrievanceCaseDetail } from '../../core/models/grievance.models';
import { PermissionAction } from '../../core/models/settings.models';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import {
  GrievanceCaseDataService,
  GrievanceCategoryDataService,
} from '../../core/services/data/grievance-data.services';
import { RolePermissionDataService } from '../../core/services/data/settings-data.services';
import { PermissionService } from '../../core/services/permission.service';
import {
  grievanceAvailableActions,
  grievanceStateOf,
  grievanceViewerRights,
} from '../../core/utilities/grievance-workflow';
import { GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE } from '../../mock-services/grievance/grievance-case-domain';
import {
  HR_ADMIN,
  HR_OFFICER,
  MANAGER,
} from '../../mock-services/grievance/grievance-test-support';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { GrievanceCaseDetailComponent } from './cases/grievance-case-detail.component';
import { GrievanceCaseListComponent } from './cases/grievance-case-list.component';
import { GrievanceIntakeComponent } from './cases/grievance-intake.component';
import { buildGrievanceCategoriesConfig } from './categories/grievance-categories.config';
import { GRIEVANCE_ROUTES } from './grievance.routes';
import { GrievanceAssignDialogComponent } from './shared/grievance-assign-dialog.component';
import { GrievanceCaseActionsComponent } from './shared/grievance-case-actions.component';
import {
  GrievanceCommandDialogComponent,
  GrievanceCommandDialogData,
} from './shared/grievance-command-dialog.component';

// Seed facts (mock-data/grievance): #3 Submitted (named), #5 Acknowledged with EMP009 as subject,
// #6 UnderReview (anonymous), #7 Assigned and #8 UnderInvestigation led by EMP009 (HR Officer),
// #9 anonymous Critical with a Restricted file, #11 ResolutionProposed by EMP009.

async function settle(fixture: ComponentFixture<unknown>, rounds = 6) {
  for (let i = 0; i < rounds; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 10));
    fixture.detectChanges();
  }
  await fixture.whenStable();
}

const current = { user: signal<CurrentUser>(HR_ADMIN) };
const dialogRef = { close: vi.fn() };

function configure(user: CurrentUser = HR_ADMIN, extra: unknown[] = []) {
  current.user = signal(user);
  dialogRef.close.mockReset();
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: current.user } },
      { provide: ConfirmDialogService, useValue: { confirm: () => of(true) } },
      ...(extra as never[]),
    ],
  });
  vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
}

async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(component);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  await settle(fixture);
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const text = (el: HTMLElement) => el.textContent?.replace(/\s+/g, ' ') ?? '';
const actionsOf = (el: HTMLElement) =>
  Array.from(el.querySelectorAll('[data-action]')).map((b) => b.getAttribute('data-action'));

async function grant(roleId: number, extra: Record<string, PermissionAction[]>) {
  const permissions = TestBed.inject(RolePermissionDataService);
  const now = await firstValueFrom(permissions.getForRole(roleId));
  await firstValueFrom(permissions.save(roleId, { ...now.grants, ...extra }));
}

const detailOf = (id: number) =>
  firstValueFrom(TestBed.inject(GrievanceCaseDataService).getById(id));

describe('grievance case-team navigation and routes', () => {
  const labels = (held: string[]) =>
    visibleNavigation(NAVIGATION, (k) => held.includes(k)).flatMap((i) => [
      i.label,
      ...(i.children ?? []).map((c) => c.label),
    ]);

  it('shows the group to an investigator-only and a reviewer-only role', () => {
    expect(labels(['grievance-investigation:View'])).toEqual([
      'Grievances',
      'Investigations',
      'My Payslips',
    ]);
    expect(labels(['grievance-review:View'])).toEqual([
      'Grievances',
      'Reviews',
      'Review Summary',
      'My Payslips',
    ]);
  });

  it('shows Case Management and Categories with grievance-management:View', () => {
    const seen = labels(['grievance:View', 'grievance-management:View']);
    expect(seen).toEqual(
      expect.arrayContaining(['Overview', 'My Grievances', 'Case Management', 'Categories']),
    );
    expect(seen).not.toContain('Investigations');
  });

  it('keys every case-team route and passes the workspace', () => {
    const keyed = (path: string) => GRIEVANCE_ROUTES.find((r) => r.path === path)!.data!;
    expect(keyed('cases')).toMatchObject({
      permission: 'grievance-management:View',
      workspace: 'management',
    });
    expect(keyed('investigations')).toMatchObject({
      permission: 'grievance-investigation:View',
      workspace: 'investigation',
    });
    expect(keyed('reviews')).toMatchObject({
      permission: 'grievance-review:View',
      workspace: 'review',
    });
    expect(keyed('categories')).toMatchObject({ permission: 'grievance-management:View' });
  });
});

describe('action availability per tier and status', () => {
  const all = () => true;

  it('offers management the triage commands of a submitted case', async () => {
    configure(HR_ADMIN);
    const d = await detailOf(3);
    const actions = grievanceAvailableActions(
      grievanceStateOf(d),
      grievanceViewerRights(d, all, 2),
    );
    expect(actions).toEqual(expect.arrayContaining(['acknowledge', 'reject', 'escalate']));
    expect(actions).not.toContain('proposeResolution');
  });

  it('lets only the Lead investigator propose a resolution', async () => {
    configure(HR_OFFICER);
    const d = await detailOf(8);
    const lead = grievanceViewerRights(d, all, 9);
    expect(grievanceAvailableActions(grievanceStateOf(d), lead)).toContain('proposeResolution');
    const member = grievanceViewerRights({ ...d, isLeadInvestigator: false }, all, 9);
    expect(grievanceAvailableActions(grievanceStateOf(d), member)).not.toContain(
      'proposeResolution',
    );
  });

  it('renders the investigator’s commands only, from the table', async () => {
    configure(HR_OFFICER);
    const d = await detailOf(7);
    const { el } = await render(GrievanceCaseActionsComponent, {
      detail: d,
      workspace: 'investigation',
    });
    expect(actionsOf(el)).toEqual(['startInvestigation', 'requestInfo', 'escalate']);
  });

  it('explains the proposer rule instead of offering Approve', async () => {
    configure(HR_ADMIN);
    const service = TestBed.inject(GrievanceCaseDataService);
    const six = await detailOf(6);
    const proposed = await firstValueFrom(
      service.proposeResolution(6, { rowVersion: six.rowVersion, summary: 'Sample summary.' }),
    );
    const { el } = await render(GrievanceCaseActionsComponent, {
      detail: proposed,
      workspace: 'review',
    });
    expect(actionsOf(el)).not.toContain('approveResolution');
    expect(text(el)).toContain('You proposed this resolution');
  });

  it('shows Archive disabled with an explanation under legal hold', async () => {
    configure(HR_ADMIN);
    const d = await detailOf(15);
    const { el } = await render(GrievanceCaseActionsComponent, {
      detail: { ...d, legalHold: true } as GrievanceCaseDetail,
    });
    expect(actionsOf(el)).not.toContain('archive');
    expect(text(el)).toContain('Under legal hold');
  });
});

describe('command dialog', () => {
  function openDialog(data: Partial<GrievanceCommandDialogData>) {
    const run = vi.fn(() => of({}));
    configure(HR_ADMIN, [
      {
        provide: MAT_DIALOG_DATA,
        useValue: { title: 'T', message: 'M', confirmText: 'OK', run, ...data },
      },
      { provide: MatDialogRef, useValue: dialogRef },
    ]);
    const fixture = TestBed.createComponent(GrievanceCommandDialogComponent);
    fixture.detectChanges();
    return { component: fixture.componentInstance, run };
  }

  it('requires the reason before running the command', () => {
    const { component, run } = openDialog({
      text: { field: 'reason', label: 'Reason', required: true, max: 1000 },
    });
    component.confirm();
    expect(run).not.toHaveBeenCalled();
    expect(component.form.controls.text.hasError('required')).toBe(true);
    component.form.controls.text.setValue('Because.');
    component.confirm();
    expect(run).toHaveBeenCalledWith(expect.objectContaining({ text: 'Because.' }));
    expect(dialogRef.close).toHaveBeenCalledWith({ done: {} });
  });

  it('caps an information request at 1000 characters', () => {
    const { component, run } = openDialog({
      text: { field: 'message', label: 'Question', required: true, max: 1000 },
    });
    component.form.controls.text.setValue('x'.repeat(1001));
    component.confirm();
    expect(run).not.toHaveBeenCalled();
    expect(component.form.controls.text.hasError('maxlength')).toBe(true);
  });
});

describe('case assignment dialog', () => {
  it('shows the generic conflict-of-interest refusal on the investigator field', async () => {
    configure(HR_ADMIN);
    const d = await detailOf(5);
    TestBed.resetTestingModule();
    configure(HR_ADMIN, [
      { provide: MAT_DIALOG_DATA, useValue: { detail: d } },
      { provide: MatDialogRef, useValue: dialogRef },
    ]);
    const { fixture } = await render(GrievanceAssignDialogComponent);
    const component = fixture.componentInstance;
    // EMP009 is the subject of #5.
    component.form.controls.investigatorEmployeeId.setValue(9);
    component.confirm();
    await settle(fixture);
    expect(component.form.controls.investigatorEmployeeId.errors).toEqual({
      server: GRIEVANCE_CONFLICT_OF_INTEREST_MESSAGE,
    });
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});

describe('case screens', () => {
  it('lists management’s cases and the investigator’s own', async () => {
    configure(HR_ADMIN);
    const management = await render(GrievanceCaseListComponent, { workspace: 'management' });
    expect(management.el.querySelectorAll('tr.mat-mdc-row').length).toBeGreaterThan(5);
    expect(text(management.el)).toContain('Anonymous');
    TestBed.resetTestingModule();
    configure(HR_OFFICER);
    const mine = await render(GrievanceCaseListComponent, { workspace: 'investigation' });
    expect(text(mine.el)).toContain('My investigations');
    // EMP009's cases, less the archived #14 and the reopened #16 (reopening ends assignments).
    expect(mine.el.querySelectorAll('tr.mat-mdc-row').length).toBe(9);
  });

  it('hides the anonymous submitter and shows the access log to an administrator', async () => {
    configure(HR_ADMIN);
    const { el } = await render(GrievanceCaseDetailComponent, { id: '9', workspace: 'management' });
    expect(text(el)).toContain('reported this anonymously');
    expect(text(el)).not.toContain('Thant Zin');
    expect(text(el)).toContain('Access log');
    expect(text(el)).toContain('Corrective actions');
  });

  it('gives the investigator no corrective actions, no restricted files and no access log', async () => {
    configure(HR_OFFICER);
    const { el } = await render(GrievanceCaseDetailComponent, {
      id: '9',
      workspace: 'investigation',
    });
    expect(text(el)).not.toContain('Access log');
    expect(text(el)).not.toContain('Corrective actions');
    expect(text(el)).not.toContain('witness-list.pdf');
  });

  // Regression: reading value() of a resource in error throws (ResourceValueError), so these
  // screens used to stay blank or keep loading instead of showing their error state.
  it('shows the error state when the list is refused (Case Management and Reviews)', async () => {
    for (const workspace of ['management', 'review']) {
      configure(HR_OFFICER);
      const { el } = await render(GrievanceCaseListComponent, { workspace });
      expect(text(el)).toContain('The cases could not be loaded');
      expect(text(el)).toContain('You do not have permission to do this.');
      expect(el.querySelector('tr.mat-mdc-row')).toBeNull();
      TestBed.resetTestingModule();
    }
  });

  it('shows the not-found state for a case outside the caller’s scope', async () => {
    configure(HR_OFFICER);
    for (const id of ['3', '5']) {
      const { el } = await render(GrievanceCaseDetailComponent, { id, workspace: 'investigation' });
      expect(text(el)).toContain('This case could not be opened');
      expect(text(el)).toContain(`Grievance case #${id} was not found.`);
      expect(el.querySelector('mat-progress-bar')).toBeNull();
    }
  });

  it('offers the file upload on an open case only', async () => {
    configure(HR_ADMIN);
    const canUpload = (fixture: ComponentFixture<unknown>) =>
      (fixture.componentInstance as { canUpload: () => boolean }).canUpload();
    const open = await render(GrievanceCaseDetailComponent, { id: '8', workspace: 'management' });
    expect(canUpload(open.fixture)).toBe(true);
    // #12 is Resolved: files are closed (the API's EnsureFilesOpen); the list stays readable.
    const resolved = await render(GrievanceCaseDetailComponent, {
      id: '12',
      workspace: 'management',
    });
    expect(text(resolved.el)).toContain('Resolved');
    expect(canUpload(resolved.fixture)).toBe(false);
  });

  it('offers a reviewer-only role the decision on a proposal', async () => {
    configure(MANAGER);
    await grant(3, { 'grievance-review': ['View', 'Approve'] });
    const { el } = await render(GrievanceCaseDetailComponent, { id: '11', workspace: 'review' });
    expect(actionsOf(el)).toEqual(['approveResolution', 'rejectResolution']);
    expect(text(el)).toContain('Findings');
  });
});

describe('grievance categories', () => {
  it('configures the API fields and blocks deleting a category in use', async () => {
    configure(HR_ADMIN);
    const service = TestBed.inject(GrievanceCategoryDataService);
    const config = buildGrievanceCategoriesConfig(service);
    expect(config.fields.map((f) => f.key)).toEqual([
      'code',
      'name',
      'description',
      'allowsAnonymous',
      'defaultTargetDays',
      'sortOrder',
      'status',
    ]);
    const created = await firstValueFrom(
      service.create({
        code: 'TEST-CAT',
        name: 'Test category',
        description: '',
        allowsAnonymous: true,
        defaultTargetDays: 10,
        sortOrder: 50,
        status: 'Active',
      }),
    );
    const updated = await firstValueFrom(
      service.update(created.id, { defaultTargetDays: 12, rowVersion: created.rowVersion }),
    );
    expect(updated.defaultTargetDays).toBe(12);
    await firstValueFrom(service.delete(created.id));
    let refusal: unknown;
    try {
      await firstValueFrom(service.delete(1));
    } catch (error) {
      refusal = error;
    }
    expect(refusal).toMatchObject({ code: 'Conflict' });
    expect((refusal as { message: string }).message).toContain('cannot be deleted');
  });
});

describe('record a grievance for an employee', () => {
  const permissionsHeld = (held: string[]) => ({
    provide: PermissionService,
    useValue: { can: (key: string) => held.includes(key) },
  });

  it('routes cases/new before the case detail, with the Create permission', () => {
    const children = GRIEVANCE_ROUTES.find((r) => r.path === 'cases')!.children!;
    const paths = children.map((c) => c.path);
    expect(paths.indexOf('new')).toBeGreaterThan(-1);
    expect(paths.indexOf('new')).toBeLessThan(paths.indexOf(':id'));
    const route = children.find((c) => c.path === 'new')!;
    expect(route.title).toBe('Record Grievance');
    expect(route.data).toMatchObject({
      breadcrumb: 'Record',
      permission: 'grievance-management:Create',
    });
    expect(route.canDeactivate?.length).toBe(1);
    // Only Case management records grievances.
    for (const other of ['investigations', 'reviews']) {
      const kids = GRIEVANCE_ROUTES.find((r) => r.path === other)!.children!;
      expect(kids.map((c) => c.path)).not.toContain('new');
    }
  });

  it('shows Record grievance in Case management with grievance-management:Create only', async () => {
    configure(HR_ADMIN, [
      permissionsHeld(['grievance-management:View', 'grievance-management:Create']),
    ]);
    const withCreate = await render(GrievanceCaseListComponent, { workspace: 'management' });
    const link = withCreate.el.querySelector('a[href="/grievances/cases/new"]');
    expect(link?.textContent).toContain('Record grievance');
    TestBed.resetTestingModule();

    configure(HR_ADMIN, [permissionsHeld(['grievance-management:View'])]);
    const viewOnly = await render(GrievanceCaseListComponent, { workspace: 'management' });
    expect(viewOnly.el.querySelector('a[href="/grievances/cases/new"]')).toBeNull();
    TestBed.resetTestingModule();

    configure(HR_ADMIN, [
      permissionsHeld(['grievance-review:View', 'grievance-management:Create']),
    ]);
    const review = await render(GrievanceCaseListComponent, { workspace: 'review' });
    expect(review.el.querySelector('a[href="/grievances/cases/new"]')).toBeNull();
  });

  it('records the grievance, then opens the new case', async () => {
    configure(HR_ADMIN);
    const service = TestBed.inject(GrievanceCaseDataService);
    const record = vi.spyOn(service, 'recordForEmployee');
    const { fixture, el } = await render(GrievanceIntakeComponent);
    const component = fixture.componentInstance;
    expect(text(el)).toContain('The grievance is recorded as submitted by this employee.');
    component.form.setValue({
      employee: {
        employeeId: 5,
        employeeCode: 'EMP005',
        displayName: 'Daniel Carter',
        departmentName: null,
        positionName: null,
      },
      receivedVia: 'InPerson',
      categoryId: 1,
      subject: 'Raised in person with HR',
      description: 'Sample account given in person, long enough for the rule.',
      incidentDate: null,
      location: '',
      involvedPersonText: '',
      involvedDepartmentId: null,
      declaration: true,
    });
    component.form.markAsDirty();
    expect(component.hasUnsavedChanges()).toBe(true);
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(record).toHaveBeenCalledWith(
      expect.objectContaining({
        employeeId: 5,
        receivedVia: 'InPerson',
        location: null,
        declarationAccepted: true,
      }),
    );
    const navigate = vi.mocked(TestBed.inject(Router).navigate);
    expect(navigate).toHaveBeenCalledWith(['/grievances/cases', expect.any(Number)]);
    const id = navigate.mock.calls[0][0][1] as number;
    expect(await detailOf(id)).toMatchObject({ status: 'Submitted', isAnonymous: false });
    expect(component.hasUnsavedChanges()).toBe(false);
  });

  it('puts the server’s refusal on the employee field', async () => {
    configure(HR_ADMIN);
    const { fixture, el } = await render(GrievanceIntakeComponent);
    const component = fixture.componentInstance;
    component.form.setValue({
      employee: {
        employeeId: 2,
        employeeCode: 'EMP002',
        displayName: 'Su Myat Noe',
        departmentName: null,
        positionName: null,
      },
      receivedVia: 'Email',
      categoryId: 1,
      subject: 'My own grievance',
      description: 'Sample text long enough to pass the description rule.',
      incidentDate: null,
      location: '',
      involvedPersonText: '',
      involvedDepartmentId: null,
      declaration: true,
    });
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(component.form.controls.employee.errors).toEqual({
      server: 'Use My Grievances to raise your own grievance.',
    });
    expect(TestBed.inject(Router).navigate).not.toHaveBeenCalled();
  });
});
