import { Type, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialogModule } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { SERVER_SESSION } from '../../core/services/auth.service';
import { CurrentUserService } from '../../core/services/current-user.service';
import { provideMockDataServices } from '../../mock-services/provide-mock-data-services';
import { CrudPageComponent } from '../../shared/components/crud/crud-page.component';
import { AdjustmentsPageComponent } from './adjustments/adjustments-page.component';
import { OvertimePageComponent } from './overtime/overtime-page.component';

interface Action {
  id: string;
  visible?: (row: { status: string }) => boolean;
}

async function editVisibleForPending(component: Type<unknown>, apiMode: boolean) {
  TestBed.configureTestingModule({
    imports: [MatDialogModule],
    providers: [
      provideRouter([]),
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: SERVER_SESSION, useValue: apiMode },
      {
        provide: CurrentUserService,
        useValue: {
          current: signal({
            id: 1,
            employeeId: null,
            displayName: 'Test User',
            email: 'test@example.test',
            jobTitle: '',
            roles: ['Tester'],
          }),
        },
      },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { data: { permission: 'attendance:View' }, parent: null } },
      },
    ],
  });
  const fixture = TestBed.createComponent(component);
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 15));
    fixture.detectChanges();
  }
  const crud = fixture.debugElement.query(By.directive(CrudPageComponent)).componentInstance as {
    actions(): Action[];
  };
  const edit = crud.actions().find((a) => a.id === 'edit')!;
  return {
    pending: edit.visible?.({ status: 'Pending' }),
    decided: edit.visible?.({ status: 'Approved' }),
  };
}

describe('Edit of pending adjustments and overtime', () => {
  for (const [name, component] of [
    ['adjustments', AdjustmentsPageComponent],
    ['overtime', OvertimePageComponent],
  ] as const) {
    it(`${name}: is offered on a pending row in the mock build`, async () => {
      expect(await editVisibleForPending(component, false)).toEqual({
        pending: true,
        decided: false,
      });
    });

    it(`${name}: is hidden in API mode (the API has no update)`, async () => {
      expect(await editVisibleForPending(component, true)).toEqual({
        pending: false,
        decided: false,
      });
    });
  }
});
