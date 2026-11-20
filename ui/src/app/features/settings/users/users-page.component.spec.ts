import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, lastValueFrom, of } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { Employee } from '../../../core/models/employee.models';
import { AppUser, BUILT_IN_ADMIN_EMPLOYEE_ID } from '../../../core/models/settings.models';
import { AuthService } from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { CurrentUser, CurrentUserService } from '../../../core/services/current-user.service';
import { UserDataService } from '../../../core/services/data/settings-data.services';
import { UiNotificationService } from '../../../core/services/ui-notification.service';
import { isCurrentEmployee, isUnlinkedAccount } from '../../../core/utilities/account-link';
import { ActivityMockService } from '../../../mock-services/activity/activity-mock.service';
import { EmployeeMockService } from '../../../mock-services/employees/employee-mock.service';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
import { UserMockService } from '../../../mock-services/settings/settings-mock.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { UsersPageComponent } from './users-page.component';

function user(permissions?: string[]): CurrentUser {
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

function setup(permissions?: string[]) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: signal(user(permissions)) } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { data: { permission: 'settings:View' }, parent: null } },
      },
    ],
  });
}

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
}

interface Action {
  id: string;
  visible?: (row: { id: number }) => boolean;
}

async function actionsFor(permissions: string[]): Promise<Action[]> {
  setup(permissions);
  const fixture = TestBed.createComponent(UsersPageComponent);
  await settle(fixture);
  const crud = fixture.debugElement.query(By.directive(CrudPageComponent)).componentInstance as {
    actions(): Action[];
  };
  return crud.actions();
}

describe('UsersPageComponent password reset', () => {
  it('shows "Send password reset" only with settings:Edit', async () => {
    const without = await actionsFor(['settings:View']);
    expect(without.map((a) => a.id)).not.toContain('extra:password-reset');
    TestBed.resetTestingModule();
    const withEdit = await actionsFor(['settings:View', 'settings:Edit']);
    expect(withEdit.map((a) => a.id)).toContain('extra:password-reset');
  });

  it('is hidden on the signed-in user own row', async () => {
    const actions = await actionsFor(['settings:View', 'settings:Edit']);
    const reset = actions.find((a) => a.id === 'extra:password-reset')!;
    expect(reset.visible?.({ id: 1 })).toBe(false);
    expect(reset.visible?.({ id: 2 })).toBe(true);
  });
});

describe('UserMockService.sendPasswordReset', () => {
  beforeEach(() => setup());

  it('succeeds for an active user and records an activity entry', async () => {
    const users = TestBed.inject(UserDataService);
    const activity = TestBed.inject(ActivityMockService);
    const own = TestBed.inject(CurrentUserService).current().id;
    const active = (await firstValueFrom(users.getAll())).find(
      (u) => u.status === 'Active' && u.id !== own,
    )!;
    await expect(firstValueFrom(users.sendPasswordReset(active.id))).resolves.toBeUndefined();
    expect(
      activity
        .snapshot(5)
        .some(
          (e) => e.message.includes('Password reset') && e.message.includes(active.displayName),
        ),
    ).toBe(true);
  });

  it('refuses an inactive account with Conflict', async () => {
    const users = TestBed.inject(UserDataService);
    const all = await firstValueFrom(users.getAll());
    const target = all.find((u) => u.status === 'Active' && u.id !== 1)!;
    await firstValueFrom(users.update(target.id, { status: 'Inactive' }));
    await expect(firstValueFrom(users.sendPasswordReset(target.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: 'Activate the account before sending a password reset.',
    });
  });

  it('refuses a locked account and asks to unlock it first', async () => {
    const users = TestBed.inject(UserDataService);
    const own = TestBed.inject(CurrentUserService).current().id;
    const target = (await firstValueFrom(users.getAll())).find(
      (u) => u.status === 'Active' && u.id !== own,
    )!;
    await firstValueFrom(users.update(target.id, { status: 'Locked' }));
    await expect(firstValueFrom(users.sendPasswordReset(target.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: 'Unlock the account before sending a password reset.',
    });
  });

  it('refuses your own account', async () => {
    const users = TestBed.inject(UserDataService);
    const own = TestBed.inject(CurrentUserService).current().id;
    await expect(firstValueFrom(users.sendPasswordReset(own))).rejects.toMatchObject({
      code: 'Conflict',
    });
  });
});

// ---- Link to employee -------------------------------------------------------------------------

/** A current employee without an account, other than `exceptId` (the target of a link). */
function freeEmployee(exceptId = 0): Employee {
  const users = TestBed.inject(UserMockService).records();
  return TestBed.inject(EmployeeMockService)
    .records()
    .find(
      (e) => e.id !== exceptId && isCurrentEmployee(e) && !users.some((u) => u.employeeId === e.id),
    )!;
}

/** An account linked to no employee, as the bootstrap command leaves it (API null, here `0`). */
async function unlinkedAccount(email: string): Promise<AppUser> {
  return firstValueFrom(
    TestBed.inject(UserDataService).create({
      employeeId: BUILT_IN_ADMIN_EMPLOYEE_ID,
      displayName: 'Bootstrap Admin',
      email,
      roleId: 1,
      status: 'Active',
      lastLoginOn: null,
    }),
  );
}

describe('UserMockService.linkEmployee', () => {
  beforeEach(() => setup());

  it('links an unlinked account to the current employee with that work email', async () => {
    const users = TestBed.inject(UserDataService);
    const activity = TestBed.inject(ActivityMockService);
    const employee = freeEmployee();
    // Trimmed and case-insensitive, like the server.
    const account = await unlinkedAccount(`  ${employee.workEmail.toUpperCase()} `);
    const linked = await firstValueFrom(users.linkEmployee(account.id));
    expect(linked.employeeId).toBe(employee.id);
    expect(linked.displayName).toBe(`${employee.firstName} ${employee.lastName}`);
    expect((await firstValueFrom(users.getById(account.id))).employeeId).toBe(employee.id);
    expect(activity.snapshot(5).map((e) => e.message)).toContain(
      `User #${account.id} "${employee.firstName} ${employee.lastName}" linked to employee ${employee.employeeCode} (simulation)`,
    );
  });

  it('refuses an account that is already linked', async () => {
    const users = TestBed.inject(UserDataService);
    const linked = (await firstValueFrom(users.getAll())).find((u) => !isUnlinkedAccount(u))!;
    await expect(firstValueFrom(users.linkEmployee(linked.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: 'This account is already linked to an employee.',
    });
  });

  it('refuses when no current employee has the work email', async () => {
    const users = TestBed.inject(UserDataService);
    const account = await unlinkedAccount('nobody@example.test');
    await expect(firstValueFrom(users.linkEmployee(account.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: 'No current employee has the work email nobody@example.test.',
    });
    // A leaver does not count as current.
    const leaver = TestBed.inject(EmployeeMockService)
      .records()
      .find((e) => !isCurrentEmployee(e))!;
    const old = await unlinkedAccount(leaver.workEmail);
    await expect(firstValueFrom(users.linkEmployee(old.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: `No current employee has the work email ${leaver.workEmail}.`,
    });
  });

  it('refuses when that employee already has an account', async () => {
    const users = TestBed.inject(UserDataService);
    const other = (await firstValueFrom(users.getAll())).find((u) => !isUnlinkedAccount(u))!;
    const account = await unlinkedAccount(other.email);
    await expect(firstValueFrom(users.linkEmployee(account.id))).rejects.toMatchObject({
      code: 'Conflict',
      message: expect.stringContaining('already has a user account'),
    });
  });
});

interface LinkAction {
  id: string;
  visible?: (row: AppUser) => boolean;
  run: (row: AppUser, input: string) => Observable<unknown>;
  successMessage: (row: AppUser, result: unknown) => string;
}

/** Builds the page (after `setup`) and returns its "Link to employee" action. */
async function linkAction(): Promise<LinkAction> {
  const fixture = TestBed.createComponent(UsersPageComponent);
  await settle(fixture);
  const page = fixture.componentInstance as unknown as {
    config(): { extraActions: LinkAction[] } | null;
  };
  return page.config()!.extraActions.find((a) => a.id === 'link-employee')!;
}

/** Answers the confirmation with `answer` and records the questions asked. */
function answerConfirm(answer: boolean): string[] {
  const asked: string[] = [];
  vi.spyOn(TestBed.inject(ConfirmDialogService), 'confirm').mockImplementation((data) => {
    asked.push(data.message);
    return of(answer);
  });
  return asked;
}

/** Runs the action to the end; `null` when it emitted nothing. */
function runToEnd(action: LinkAction, row: AppUser): Promise<unknown> {
  return lastValueFrom(action.run(row, ''), { defaultValue: null });
}

const ADMIN = ['settings:View', 'settings:Edit', 'employees:View'];

describe('UsersPageComponent link to employee', () => {
  it('shows "Link to employee" only with settings:Edit, and only on unlinked rows', async () => {
    const without = await actionsFor(['settings:View', 'employees:View']);
    expect(without.map((a) => a.id)).not.toContain('extra:link-employee');
    TestBed.resetTestingModule();
    const actions = await actionsFor(ADMIN);
    const link = actions.find((a) => a.id === 'extra:link-employee')!;
    const visible = link.visible as unknown as (row: { employeeId: number }) => boolean;
    expect(visible({ employeeId: BUILT_IN_ADMIN_EMPLOYEE_ID })).toBe(true);
    expect(visible({ employeeId: 5 })).toBe(false);
  });

  it('names the employee from a read made when the dialog opens, not from the page load', async () => {
    setup(ADMIN);
    const account = await unlinkedAccount('new.address@example.test');
    const action = await linkAction();
    // The work email changes after the page loaded: the question must follow it.
    const employee = freeEmployee();
    TestBed.inject(EmployeeMockService).updateNow(employee.id, {
      workEmail: 'NEW.address@example.test',
    });
    const asked = answerConfirm(false);
    expect(await runToEnd(action, account)).toBeNull();
    expect(asked).toEqual([
      `Link Bootstrap Admin to ${employee.firstName} ${employee.lastName} (${employee.employeeCode})? They must sign in again.`,
    ]);
  });

  it('says the server decides when no current employee matches, or without employees:View', async () => {
    setup(ADMIN);
    const account = await unlinkedAccount('nobody@example.test');
    const asked = answerConfirm(false);
    await runToEnd(await linkAction(), account);
    TestBed.resetTestingModule();

    // Without employees:View the page cannot read work emails, even for a matching account.
    setup(['settings:View', 'settings:Edit']);
    const matching = await unlinkedAccount(freeEmployee().workEmail);
    const askedWithout = answerConfirm(false);
    await runToEnd(await linkAction(), matching);
    expect([...asked, ...askedWithout]).toEqual([
      'The employee with the work email nobody@example.test will be linked. They must sign in again.',
      `The employee with the work email ${matching.email} will be linked. They must sign in again.`,
    ]);
  });

  it('sends nothing when the confirmation is cancelled', async () => {
    setup(ADMIN);
    const account = await unlinkedAccount(freeEmployee().workEmail);
    answerConfirm(false);
    const link = vi.spyOn(TestBed.inject(UserDataService), 'linkEmployee');
    expect(await runToEnd(await linkAction(), account)).toBeNull();
    expect(link).not.toHaveBeenCalled();
  });

  it('builds the success text from the server answer, not from the question', async () => {
    setup(ADMIN);
    const account = await unlinkedAccount('nobody@example.test');
    // The server linked another employee than the page could guess (none matched here).
    const linkedTo = freeEmployee();
    const answer: AppUser = { ...account, employeeId: linkedTo.id, displayName: 'Server Name' };
    vi.spyOn(TestBed.inject(UserDataService), 'linkEmployee').mockReturnValue(of(answer));
    const action = await linkAction();
    const asked = answerConfirm(true);
    const result = await runToEnd(action, account);
    expect(asked[0]).toContain('The employee with the work email nobody@example.test');
    expect(result).toBe(
      `nobody@example.test is now linked to Server Name (${linkedTo.employeeCode}).`,
    );
    expect(action.successMessage(account, result)).toBe(result);
  });

  it('shows "Not linked" in the Employee column of an unlinked account', async () => {
    setup(ADMIN);
    await unlinkedAccount('nobody@example.test');
    const fixture = TestBed.createComponent(UsersPageComponent);
    await settle(fixture);
    const page = fixture.componentInstance as unknown as {
      config(): { columns: { key: string; value?: (row: unknown) => unknown }[] } | null;
    };
    const column = page.config()!.columns.find((c) => c.key === 'employeeId')!;
    expect(column.value!({ employeeId: BUILT_IN_ADMIN_EMPLOYEE_ID })).toBe('Not linked');
    expect(column.value!({ employeeId: 2 })).not.toBe('Not linked');
  });

  it('signs you out after linking your own account, and not after linking another', async () => {
    setup(ADMIN);
    const employee = freeEmployee();
    const own = await unlinkedAccount(employee.workEmail);
    const otherEmployee = freeEmployee(employee.id);
    const other = await unlinkedAccount(otherEmployee.workEmail);
    const me = TestBed.inject(CurrentUserService) as unknown as {
      current: WritableSignal<CurrentUser>;
    };
    me.current.set({ ...user(ADMIN), id: own.id });
    const signOut = vi.spyOn(TestBed.inject(AuthService), 'signOut').mockReturnValue(undefined);
    const info = vi.spyOn(TestBed.inject(UiNotificationService), 'info');
    answerConfirm(true);
    const action = await linkAction();

    expect(await runToEnd(action, other)).toBe(
      `${other.email} is now linked to ${otherEmployee.firstName} ${otherEmployee.lastName} (${otherEmployee.employeeCode}).`,
    );
    expect(signOut).not.toHaveBeenCalled();

    // No value: the page shows no success toast and does not reload (the session is ending).
    expect(await runToEnd(action, own)).toBeNull();
    expect(info).toHaveBeenCalledWith(expect.stringContaining('signed out'));
    expect(signOut).toHaveBeenCalledTimes(1);
  });
});
