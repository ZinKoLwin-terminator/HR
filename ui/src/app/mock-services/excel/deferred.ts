import { Observable, from, map, switchMap, timer } from 'rxjs';

/**
 * Like `mockResponse`, but for work that is asynchronous (spreadsheet parsing/generation).
 * Results are deep-cloned unless they hold a Blob (files are returned as they are).
 */
export function deferred<T>(
  work: () => Promise<T> | T,
  latencyMs: number,
  clone = true,
): Observable<T> {
  return timer(latencyMs).pipe(
    switchMap(() => from(Promise.resolve().then(work))),
    map((value) => (clone ? structuredClone(value) : value)),
  );
}
