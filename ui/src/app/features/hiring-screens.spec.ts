import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../core/configuration/app-config';
import { BLACKLIST_MATCH_LABEL } from '../core/models/blacklist.models';
import { AppError } from '../core/models/common.models';
import { Application, Candidate } from '../core/models/recruitment.models';
import { NAVIGATION, visibleNavigation } from '../core/constants/navigation';
import { ConfirmDialogService } from '../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../core/services/current-user.service';
import {
  ApplicationDataService,
  CandidateDataService,
  JobOpeningDataService,
} from '../core/services/data/recruitment-data.services';
import { UiNotificationService } from '../core/services/ui-notification.service';
import { MOCK_BLACKLIST } from '../mock-data/employees/blacklist.mock';
import { MOCK_EMPLOYEES } from '../mock-data/employees/employees.mock';
import { MOCK_PAYROLL_RUNS, MOCK_PAYSLIPS } from '../mock-data/payroll/payroll-runs.mock';
import { provideMockDataServices } from '../mock-services/provide-mock-data-services';
import { todayIso } from '../shared/utilities/date.util';
import { DoNotRehirePageComponent } from './employees/do-not-rehire/do-not-rehire-page.component';
import { ProfileBlacklistSectionComponent } from './employees/employee-profile/profile-blacklist-section.component';
import { CandidatesPageComponent } from './recruitment/candidates/candidates-page.component';
import { RecruitmentPipelineComponent } from './recruitment/pipeline/recruitment-pipeline.component';
import { MyPayslipsListComponent } from './self-service/my-payslips/my-payslips-list.component';

function user(roles: string[], permissions?: string[], employeeId: number | null = 2): CurrentUser {
  return {
    id: 1,
    employeeId,
    displayName: 'Test User',
    email: 'test@example.test',
    jobTitle: '',
    roles,
    permissions,
  };
}

function configure(current: Signal<CurrentUser>, extra: object[] = []) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current } },
      ...extra,
    ],
  });
}

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  for (let i = 0; i < 5; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
}

const text = (fixture: { nativeElement: unknown }) =>
  (fixture.nativeElement as HTMLElement).textContent ?? '';

const listed = MOCK_EMPLOYEES.find((e) => e.id === MOCK_BLACKLIST[0].employeeId)!;
const unlisted = MOCK_EMPLOYEES.find((e) => !!e.exitDate && e.id !== listed.id)!;
const current = MOCK_EMPLOYEES.find((e) => !e.exitDate)!;

describe('do-not-rehire section of the employee profile', () => {
  async function render(
    permissions: string[] | undefined,
    employee: { id: number; exitDate: string | null },
  ) {
    configure(signal(user(['Tester'], permissions)));
    const fixture = TestBed.createComponent(ProfileBlacklistSectionComponent);
    fixture.componentRef.setInput('employeeId', employee.id);
    fixture.componentRef.setInput('employeeName', 'Sample Person');
    fixture.componentRef.setInput('exitDate', employee.exitDate);
    await settle(fixture);
    return fixture;
  }

  it('is not shown for an employee without an exit date', async () => {
    const fixture = await render(['employees:ViewBlacklist', 'employees:EditBlacklist'], {
      id: current.id,
      exitDate: null,
    });
    expect(text(fixture)).not.toContain('Do-not-rehire');
  });

  it('is not shown, and nothing is read, without either blacklist permission', async () => {
    const fixture = await render(['employees:View'], { id: listed.id, exitDate: listed.exitDate });
    expect(text(fixture)).not.toContain('Do-not-rehire');
    expect(text(fixture)).not.toContain('Sample reason');
  });

  it('shows the entry in force and its reason with ViewBlacklist, but no actions without EditBlacklist', async () => {
    const fixture = await render(['employees:ViewBlacklist'], {
      id: listed.id,
      exitDate: listed.exitDate,
    });
    expect(text(fixture)).toContain('On the do-not-rehire list since');
    expect(text(fixture)).toContain('Sample reason');
    expect(text(fixture)).not.toContain('Lift entry');
    expect(text(fixture)).not.toContain('Add to do-not-rehire list');
  });

  it('offers Lift entry with EditBlacklist when an entry is in force, and no Add', async () => {
    const fixture = await render(['employees:ViewBlacklist', 'employees:EditBlacklist'], {
      id: listed.id,
      exitDate: listed.exitDate,
    });
    expect(text(fixture)).toContain('Lift entry');
    expect(text(fixture)).not.toContain('Add to do-not-rehire list');
  });

  it('offers Add to do-not-rehire list for a former employee with no entry', async () => {
    const fixture = await render(['employees:ViewBlacklist', 'employees:EditBlacklist'], {
      id: unlisted.id,
      exitDate: unlisted.exitDate,
    });
    expect(text(fixture)).toContain('Not on the do-not-rehire list.');
    expect(text(fixture)).toContain('Add to do-not-rehire list');
  });

  it('hides Add without EditBlacklist', async () => {
    const fixture = await render(['employees:ViewBlacklist'], {
      id: unlisted.id,
      exitDate: unlisted.exitDate,
    });
    expect(text(fixture)).not.toContain('Add to do-not-rehire list');
  });

  it('shows only the Add button, and never a reason, to EditBlacklist without ViewBlacklist', async () => {
    const fixture = await render(['employees:EditBlacklist'], {
      id: listed.id,
      exitDate: listed.exitDate,
    });
    expect(text(fixture)).toContain('Add to do-not-rehire list');
    expect(text(fixture)).not.toContain('Sample reason');
  });

  it('allows everything in the mock build, which has no permission list', async () => {
    const fixture = await render(undefined, { id: unlisted.id, exitDate: unlisted.exitDate });
    expect(text(fixture)).toContain('Add to do-not-rehire list');
  });
});

describe('do-not-rehire list screen', () => {
  const route = {
    provide: ActivatedRoute,
    useValue: { snapshot: { data: { permission: 'employees:ViewBlacklist' }, parent: null } },
  };

  async function render(permissions: string[]) {
    configure(signal(user(['Tester'], permissions)), [route]);
    const fixture = TestBed.createComponent(DoNotRehirePageComponent);
    await settle(fixture);
    return fixture;
  }

  it('lists entries without the reason, and never offers edit or delete', async () => {
    const fixture = await render(['employees:ViewBlacklist', 'employees:EditBlacklist']);
    expect(text(fixture)).toContain('Do-not-rehire list');
    expect(text(fixture)).toContain(
      `${MOCK_BLACKLIST[0].employeeName} (${MOCK_BLACKLIST[0].employeeCode})`,
    );
    expect(text(fixture)).toContain('In force');
    expect(text(fixture)).not.toContain(MOCK_BLACKLIST[0].reason);
    expect(text(fixture)).toContain('Add do-not-rehire entry');
  });

  it('hides the Add button without EditBlacklist', async () => {
    const fixture = await render(['employees:ViewBlacklist']);
    expect(text(fixture)).not.toContain('Add do-not-rehire entry');
  });
});

describe('navigation entries', () => {
  const labels = (held: string[]) =>
    visibleNavigation(NAVIGATION, (k) => held.includes(k)).flatMap((i) => [
      i.label,
      ...(i.children ?? []).map((c) => c.label),
    ]);

  it('shows the do-not-rehire list only with ViewBlacklist', () => {
    expect(labels(['employees:View'])).not.toContain('Do-not-rehire list');
    expect(labels(['employees:View', 'employees:ViewBlacklist'])).toContain('Do-not-rehire list');
  });

  it('shows My Payslips to every signed-in user, even with no permissions', () => {
    expect(labels([])).toEqual(['My Payslips']);
    expect(labels(['employees:View'])).toContain('My Payslips');
  });
});

describe('recruitment pipeline and the do-not-rehire flag', () => {
  async function flaggedCard() {
    const candidates = TestBed.inject(CandidateDataService);
    const applications = TestBed.inject(ApplicationDataService);
    const jobs = TestBed.inject(JobOpeningDataService);
    const job = (await firstValueFrom(jobs.getAll())).find((j) => j.status === 'Open')!;
    const make = async (email: string, phone: string, name: string) => {
      const c = await firstValueFrom(
        candidates.create({
          firstName: name,
          lastName: 'Sample',
          email,
          phone,
          source: 'Referral',
          currentEmployer: '',
          experienceYears: 1,
          resumeName: null,
          notes: '',
        } as Omit<Candidate, 'id'>),
      );
      return firstValueFrom(
        applications.create({
          jobOpeningId: job.id,
          candidateId: c.id,
          stage: 'Applied',
          appliedOn: todayIso(),
          rejectionReason: null,
          notes: '',
        } as Omit<Application, 'id'>),
      );
    };
    const flagged = await make(listed.workEmail, '09000000011', 'Flagged');
    const plain = await make('clean.applicant@example.test', '09000000012', 'Clean');
    return { flagged, plain };
  }

  it('shows the badge on a matching candidate only, and nothing about who or why', async () => {
    configure(signal(user(['HR Administrator'])));
    await flaggedCard();
    const fixture = TestBed.createComponent(RecruitmentPipelineComponent);
    await settle(fixture);
    const html = fixture.nativeElement as HTMLElement;
    const cards = Array.from(html.querySelectorAll('li.card'));
    const flaggedCardEl = cards.find((c) => c.textContent?.includes('Flagged Sample'))!;
    const plainCardEl = cards.find((c) => c.textContent?.includes('Clean Sample'))!;
    expect(flaggedCardEl.textContent).toContain(BLACKLIST_MATCH_LABEL);
    expect(plainCardEl.textContent).not.toContain(BLACKLIST_MATCH_LABEL);
    expect(html.textContent).not.toContain(listed.employeeCode);
    expect(html.textContent).not.toContain('Sample reason');
  });

  const blockError: AppError = {
    code: 'Conflict',
    message:
      'This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason.',
  };

  async function selectFlagged(roles: string[]) {
    configure(signal(user(roles)));
    const { flagged } = await flaggedCard();
    const applications = TestBed.inject(ApplicationDataService);
    const moveStage = vi
      .spyOn(applications, 'moveStage')
      .mockReturnValueOnce(throwError(() => blockError))
      .mockReturnValue(of({ ...flagged, stage: 'Selected' }));
    const prompt = vi
      .spyOn(TestBed.inject(ConfirmDialogService), 'prompt')
      .mockReturnValue(of('Approved after review'));
    const toastError = vi.spyOn(TestBed.inject(UiNotificationService), 'error');
    const fixture = TestBed.createComponent(RecruitmentPipelineComponent);
    await settle(fixture);
    const component = fixture.componentInstance as unknown as {
      move(card: { application: Application; name: string }, stage: string): void;
    };
    component.move({ application: flagged, name: 'Flagged Sample' }, 'Selected');
    await settle(fixture);
    return { moveStage, prompt, toastError };
  }

  it('offers the approval dialog to an HR Administrator and resends with the reason', async () => {
    const { moveStage, prompt, toastError } = await selectFlagged(['HR Administrator']);
    expect(prompt).toHaveBeenCalledTimes(1);
    expect(prompt.mock.calls[0][0].maxLength).toBe(300);
    expect(prompt.mock.calls[0][0].required).toBe(true);
    expect(moveStage).toHaveBeenCalledTimes(2);
    expect(moveStage.mock.calls[1].slice(1)).toEqual(['Selected', '', 'Approved after review']);
    expect(toastError).not.toHaveBeenCalled();
  });

  it('only shows the refusal message to anyone else, without a dialog', async () => {
    const { moveStage, prompt, toastError } = await selectFlagged(['Recruiter']);
    expect(prompt).not.toHaveBeenCalled();
    expect(moveStage).toHaveBeenCalledTimes(1);
    expect(toastError).toHaveBeenCalledWith(blockError);
  });
});

describe('My payslips screen', () => {
  const released = new Set(
    MOCK_PAYROLL_RUNS.filter((r) => r.status === 'Approved' || r.status === 'Paid').map(
      (r) => r.id,
    ),
  );
  const owner = MOCK_PAYSLIPS.find((p) => released.has(p.runId))!;

  it("lists the signed-in employee's released payslips, without any payroll permission", async () => {
    configure(signal(user(['Employee'], ['attendance:View'], owner.employeeId)));
    const fixture = TestBed.createComponent(MyPayslipsListComponent);
    await settle(fixture);
    const mine = MOCK_PAYSLIPS.filter(
      (p) => p.employeeId === owner.employeeId && released.has(p.runId),
    );
    expect(text(fixture)).toContain('My payslips');
    expect(text(fixture)).toContain(mine[0].periodName);
    expect(fixture.nativeElement.querySelectorAll('tr[mat-row], tr.mat-mdc-row').length).toBe(
      Math.min(mine.length, 12),
    );
  });

  it('shows the empty state for a user without a linked employee', async () => {
    configure(signal(user(['Employee'], [], null)));
    const fixture = TestBed.createComponent(MyPayslipsListComponent);
    await settle(fixture);
    expect(text(fixture)).toContain('No payslips yet');
  });
});

describe('candidates screen and the do-not-rehire flag', () => {
  it('shows the badge on a matching candidate only', async () => {
    configure(signal(user(['Recruiter'])));
    const candidates = TestBed.inject(CandidateDataService);
    const base = {
      lastName: 'Flagtest',
      phone: '09000000021',
      source: 'Referral',
      currentEmployer: '',
      experienceYears: 1,
      resumeName: null,
      notes: '',
    } as const;
    await firstValueFrom(
      candidates.create({ ...base, firstName: 'Zed', email: listed.workEmail } as Omit<
        Candidate,
        'id'
      >),
    );
    await firstValueFrom(
      candidates.create({
        ...base,
        firstName: 'Yan',
        phone: '09000000022',
        email: 'yan.flagtest@example.test',
      } as Omit<Candidate, 'id'>),
    );
    const fixture = TestBed.createComponent(CandidatesPageComponent);
    await settle(fixture);
    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'),
    );
    const zed = rows.find((r) => r.textContent?.includes('Zed Flagtest'))!;
    const yan = rows.find((r) => r.textContent?.includes('Yan Flagtest'))!;
    expect(zed.textContent).toContain(BLACKLIST_MATCH_LABEL);
    expect(yan.textContent).not.toContain(BLACKLIST_MATCH_LABEL);
  });
});
