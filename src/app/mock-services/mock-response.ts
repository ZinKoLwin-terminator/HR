import { Observable, map, timer } from 'rxjs';
import { AppError } from '../core/models/common.models';

/**
 * Simulates an asynchronous data call. The work runs AFTER the simulated
 * latency (like a server would), its result is deep-cloned so callers can
 * never mutate the in-memory store, and thrown `AppError`s become
 * Observable errors.
 */
export function mockResponse<T>(work: () => T, latencyMs: number): Observable<T> {
  return timer(latencyMs).pipe(map(() => structuredClone(work())));
}

export function mockError(
  code: AppError['code'],
  message: string,
  fieldErrors?: Record<string, string>,
): AppError {
  return { code, message, fieldErrors };
}
