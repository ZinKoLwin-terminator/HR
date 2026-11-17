import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { provideMockDataServices } from '../provide-mock-data-services';

// Shared helpers of the KPI mock specs. Not used by the application.

const user = (
  id: number,
  employeeId: number | null,
  displayName: string,
  roles: string[],
): CurrentUser => ({ id, employeeId, displayName, email: '', jobTitle: '', roles });

/** Su Myat Noe, EMP002: the HR Administrator of the sample data (user id 1). */
export const HR_ADMIN = user(1, 2, 'Su Myat Noe', ['HR Administrator']);
/** Thura Aung, EMP001: a second HR person, for the separation of duties (user id 2). */
export const HR_OFFICER = user(2, 1, 'Thura Aung', ['HR Officer']);
/** Daniel Carter, EMP005: manages EMP046, EMP017, EMP014, EMP019 and EMP023 (user id 11). */
export const MANAGER = user(11, 5, 'Daniel Carter', ['Department Manager']);
/** Ye Yint Naing, EMP009: the IT manager, outside Daniel Carter's team (user id 6). */
export const OTHER_MANAGER = user(6, 9, 'Ye Yint Naing', ['Department Manager']);
/** Kaung Myat Thu, EMP023 (Software Engineer in Daniel Carter's team). */
export const EMPLOYEE_23 = user(40, 23, 'Kaung Myat Thu', ['Employee']);
/** Hein Thant Zin, EMP046 (Software Engineer in Daniel Carter's team). */
export const EMPLOYEE_46 = user(41, 46, 'Hein Thant Zin', ['Employee']);
/** Sai Htoo Aung, EMP056 (IT, not in Daniel Carter's team). */
export const EMPLOYEE_56 = user(42, 56, 'Sai Htoo Aung', ['Employee']);

/**
 * Configures the mock data services with no latency and a switchable signed-in user (the mock
 * decides "HR", "manager" and "own" from the session, so each test picks who is acting).
 */
export function configureKpiTest(initial: CurrentUser = HR_ADMIN): {
  as: (next: CurrentUser) => void;
} {
  let current = initial;
  TestBed.configureTestingModule({
    providers: [
      provideMockDataServices(),
      { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, mockLatencyMs: 0 } },
      { provide: CurrentUserService, useValue: { current: () => current } },
    ],
  });
  return {
    as: (next) => {
      current = next;
    },
  };
}

export const wait = <T>(source: Observable<T>): Promise<T> => firstValueFrom(source);

/** The AppError a promise fails with. */
export async function failure(promise: Promise<unknown>): Promise<AppError> {
  try {
    await promise;
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

export const PAGE = { pageIndex: 0, pageSize: 200 } as const;

/** A tiny valid PDF-looking file. */
export function pdfFile(name = 'evidence.pdf', extra = 0): File {
  const bytes = new Uint8Array(16 + extra);
  bytes.set([0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34]);
  return new File([bytes], name, { type: 'application/pdf' });
}
