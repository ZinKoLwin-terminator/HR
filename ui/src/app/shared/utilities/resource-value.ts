/**
 * Safe reads of an Angular resource (`resource`, `rxResource`, `httpResource`).
 *
 * Reading `value()` of a resource in the error state throws `ResourceValueError`, and so does a
 * `linkedSignal({ source: res.value, ... })`. A throw inside a template or `computed()` aborts
 * change detection, so the screen freezes (often on its loading bar) and its error state never
 * renders. A 403 from the server for a role without the permission is the usual cause. Read every
 * resource through `safeValue` (or `res.hasValue() ? res.value() : fallback`).
 */

/** Anything with the resource read API (`hasValue()` and `value()`). */
export interface ReadableResource<T> {
  hasValue(): boolean;
  value(): T;
}

/** The value of a resource, or undefined when it has none (loading, idle or in error). */
export function safeValue<T>(resource: ReadableResource<T>): T | undefined {
  return resource.hasValue() ? resource.value() : undefined;
}
