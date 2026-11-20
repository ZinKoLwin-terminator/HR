import { Injector, linkedSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AppError } from '../../core/models/common.models';
import { safeValue } from './resource-value';

const FORBIDDEN: AppError = {
  code: 'Forbidden',
  message: 'You do not have permission to do that.',
};

async function settle() {
  await new Promise((resolve) => setTimeout(resolve, 0));
  TestBed.tick();
}

function make<T>(stream: () => Observable<T>) {
  const injector = TestBed.inject(Injector);
  return rxResource({ stream, injector });
}

describe('safeValue', () => {
  it('returns the value of a loaded resource', async () => {
    const res = make(() => of([1, 2]));
    await settle();
    expect(safeValue(res)).toEqual([1, 2]);
  });

  it('returns undefined while loading', () => {
    const pending = new Subject<number[]>();
    const res = make(() => pending);
    TestBed.tick();
    expect(res.isLoading()).toBe(true);
    expect(safeValue(res)).toBeUndefined();
  });

  it('never throws for a resource in error (where value() does)', async () => {
    const res = make(() => throwError(() => FORBIDDEN));
    await settle();
    expect(res.status()).toBe('error');
    expect(() => res.value()).toThrow();
    expect(safeValue(res)).toBeUndefined();
  });

  it('makes a linkedSignal over a failed resource readable', async () => {
    const res = make(() => throwError(() => FORBIDDEN));
    const unsafe = TestBed.runInInjectionContext(() =>
      linkedSignal({ source: res.value, computation: (v: unknown) => v }),
    );
    const safe = TestBed.runInInjectionContext(() =>
      linkedSignal({ source: () => safeValue(res), computation: (v: unknown) => v ?? 'kept' }),
    );
    await settle();
    expect(() => unsafe()).toThrow();
    expect(safe()).toBe('kept');
  });
});
