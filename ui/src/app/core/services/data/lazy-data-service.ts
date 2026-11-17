import { Injector, Type, inject } from '@angular/core';
import { Observable, defer, from, map, shareReplay, switchMap } from 'rxjs';

/**
 * A provider factory for a data contract whose implementation lives in a lazy chunk, so binding it in
 * `provideMockDataServices()` or `provideApiDataServices()` does not add it to the initial bundle.
 * The returned object has every method of `methods`; the first call loads the implementation (an
 * `@Injectable({ providedIn: 'root' })` class) and every call is forwarded to it. Each method must
 * return an Observable; nothing runs until it is subscribed, as with the real service.
 *
 *   { provide: Contract, useFactory: lazyDataService(() => import('./impl').then((m) => m.Impl), METHODS) }
 */
export function lazyDataService<T extends object>(
  load: () => Promise<Type<T>>,
  methods: readonly (keyof T)[],
): () => T {
  return () => {
    const injector = inject(Injector);
    const instance$ = defer(() => from(load())).pipe(
      map((type) => injector.get(type)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    const delegate: Record<string, unknown> = {};
    for (const method of methods) {
      delegate[method as string] = (...args: unknown[]): Observable<unknown> =>
        instance$.pipe(
          switchMap((instance) =>
            (instance as Record<string, (...a: unknown[]) => Observable<unknown>>)[
              method as string
            ](...args),
          ),
        );
    }
    return delegate as T;
  };
}
