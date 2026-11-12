import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../../core/configuration/app-config';
import { CurrentUser, CurrentUserService } from '../../../core/services/current-user.service';
import { UserDataService } from '../../../core/services/data/settings-data.services';
import { ActivityMockService } from '../../../mock-services/activity/activity-mock.service';
import { provideMockDataServices } from '../../../mock-services/provide-mock-data-services';
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
    const active = (await firstValueFrom(users.getAll())).find((u) => u.status === 'Active')!;
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
});
