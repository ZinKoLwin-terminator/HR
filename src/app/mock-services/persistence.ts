import { WritableSignal, effect, signal } from '@angular/core';

/**
 * Optional browser persistence for the in-memory mock stores.
 *
 * When enabled (`provideMockDataServices({ persist: true })`), every store saves itself to
 * `localStorage` after each change and reloads from there on the next visit, so records a user
 * creates or edits survive a page reload. {@link resetPersistedData} puts everything back to the
 * seed data. Off by default, so unit tests always start from the seed.
 *
 * Change `STORAGE_PREFIX` when a model changes shape: old saved data is then ignored.
 */
const STORAGE_PREFIX = 'hrms.data.v1.';

interface Entry {
  value: WritableSignal<unknown>;
  initial: () => unknown;
}

let enabled = false;
const entries = new Map<string, Entry>();
const resetHooks = new Set<() => void>();
let warned = false;

export function setPersistence(value: boolean): void {
  enabled = value;
  entries.clear();
  resetHooks.clear();
}

export function isPersistenceEnabled(): boolean {
  return enabled;
}

function read(key: string): unknown {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + key);
    return raw === null ? undefined : JSON.parse(raw);
  } catch {
    return undefined;
  }
}

function write(key: string, value: unknown): void {
  try {
    localStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(value));
  } catch {
    if (!warned) {
      warned = true;
      console.warn('Browser storage is unavailable or full: changes will be lost on reload.');
    }
  }
}

function clearStorage(): void {
  try {
    for (const name of Object.keys(localStorage)) {
      if (name.startsWith(STORAGE_PREFIX)) {
        localStorage.removeItem(name);
      }
    }
  } catch {
    // Storage unavailable: nothing was saved in the first place.
  }
}

/**
 * A signal that starts from the saved copy (when persistence is on and a valid copy exists),
 * otherwise from `initial()`, and saves itself on every change.
 * Must be called in an injection context (it registers an effect).
 */
export function persistedSignal<T>(key: string, initial: () => T): WritableSignal<T> {
  const seed = initial();
  if (!enabled) {
    return signal(seed);
  }
  const saved = read(key);
  // A saved value of the wrong kind (array vs object) is ignored rather than trusted.
  const usable = saved !== undefined && Array.isArray(saved) === Array.isArray(seed);
  const value = signal<T>(usable ? (saved as T) : seed);
  entries.set(key, { value: value as WritableSignal<unknown>, initial });
  effect(() => write(key, value()));
  return value;
}

/** Registers extra work to run on reset, for state that is not kept in a persisted signal. */
export function onDataReset(hook: () => void): void {
  resetHooks.add(hook);
}

/**
 * Throws away everything the user entered: clears the saved copies (including stores that have
 * not been created yet in this session), puts the live stores back to their seed data and runs
 * the registered hooks.
 */
export function resetPersistedData(): void {
  clearStorage();
  for (const { value, initial } of entries.values()) {
    value.set(initial());
  }
  for (const hook of resetHooks) {
    hook();
  }
}
