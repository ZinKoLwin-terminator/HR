/**
 * Deterministic pseudo-random helpers for generating stable sample data.
 * The same seed always yields the same value, so mock data does not change
 * between reloads (only dates relative to "today" move forward).
 */

function hashString(value: string): number {
  let hash = 2166136261;
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

/** Returns a float in [0, 1) derived from the seed. */
export function seededRandom(seed: string): number {
  // mulberry32 step on the hashed seed
  let t = (hashString(seed) + 0x6d2b79f5) >>> 0;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

/** Integer in the inclusive range [min, max]. */
export function seededInt(seed: string, min: number, max: number): number {
  return min + Math.floor(seededRandom(seed) * (max - min + 1));
}

export function seededPick<T>(seed: string, items: readonly T[]): T {
  return items[seededInt(seed, 0, items.length - 1)];
}

export function seededDigits(seed: string, length: number): string {
  let result = '';
  for (let i = 0; i < length; i++) {
    result += String(seededInt(`${seed}:${i}`, 0, 9));
  }
  return result;
}
