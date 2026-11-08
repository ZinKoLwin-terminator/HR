/**
 * Seed mode for the in-memory mock services.
 *
 * With `emptyData` on, mock stores start without sample records so a user can enter their own data
 * from scratch. System configuration (roles, permissions, general settings) and a single
 * administrator account are still seeded, otherwise nobody could sign in.
 *
 * Set by `provideMockDataServices({ emptyData: true })`; defaults to off, so unit tests keep the
 * full sample data.
 */
let emptyData = false;

export function setEmptyData(value: boolean): void {
  emptyData = value;
}

export function isEmptyData(): boolean {
  return emptyData;
}

/** Picks the records a store starts with: the full sample set, or `whenEmpty` in empty mode. */
export function initialRecords<T>(
  sample: readonly T[],
  whenEmpty: readonly T[] = [],
): readonly T[] {
  return emptyData ? whenEmpty : sample;
}
