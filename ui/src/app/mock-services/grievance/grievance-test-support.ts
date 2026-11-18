import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../../core/configuration/app-config';
import { AppError } from '../../core/models/common.models';
import { CurrentUser, CurrentUserService } from '../../core/services/current-user.service';
import { provideMockDataServices } from '../provide-mock-data-services';

// Shared helpers of the grievance mock specs. Not used by the application.

const user = (
  id: number,
  employeeId: number | null,
  displayName: string,
  roles: string[],
): CurrentUser => ({ id, employeeId, displayName, email: '', jobTitle: '', roles });

/** Su Myat Noe, EMP002, user 1: HR Administrator (management and review, every grant). */
export const HR_ADMIN = user(1, 2, 'Su Myat Noe', ['HR Administrator']);
/** Ye Yint Naing, EMP009, user 6: HR Officer (investigator; Lead of the sample cases; subject of #5). */
export const HR_OFFICER = user(6, 9, 'Ye Yint Naing', ['HR Officer']);
/** Daniel Carter, EMP005, user 11: Employee. Owns #1, #2 (anonymous draft), #4 (anonymous), #10. */
export const DANIEL = user(11, 5, 'Daniel Carter', ['Employee']);
/** Thura Aung, EMP001, user 2: Department Manager (own grievances only). */
export const MANAGER = user(2, 1, 'Thura Aung', ['Department Manager']);
/** Kaung Myat Thu, EMP023 (no seeded account): submitter of the named case #3. */
export const EMPLOYEE_23 = user(40, 23, 'Kaung Myat Thu', ['Employee']);
/** Thant Zin Phyo, EMP036 (no seeded account): the anonymous submitter of #9. */
export const EMPLOYEE_36 = user(41, 36, 'Thant Zin Phyo', ['Employee']);
/** An administrator account without a linked employee. */
export const BUILT_IN_ADMIN = user(99, null, 'Built-in admin', ['HR Administrator']);
/** A session with no role at all. */
export const NOBODY = user(98, 50, 'No Role', []);

/** Mock services without latency and a switchable signed-in user. */
export function configureGrievanceTest(initial: CurrentUser = HR_ADMIN): {
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

/** The AppError a call fails with. */
export async function failure(source: Observable<unknown>): Promise<AppError> {
  try {
    await firstValueFrom(source);
  } catch (error) {
    return error as AppError;
  }
  throw new Error('Expected the call to fail.');
}

export const PAGE = { pageIndex: 0, pageSize: 200 } as const;

/** A tiny valid PDF-looking file. */
export function pdfFile(name = 'note.pdf'): File {
  const bytes = new Uint8Array(16);
  bytes.set([0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34]);
  return new File([bytes], name, { type: 'application/pdf' });
}

/** A file whose content is not an allowed type. */
export function textFile(name = 'note.pdf'): File {
  return new File([new TextEncoder().encode('plain text')], name, { type: 'application/pdf' });
}
