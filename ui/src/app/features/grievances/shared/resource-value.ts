/**
 * The value of a resource, or undefined when it has none. Reading `value()` of a resource in error
 * throws (`ResourceValueError`), which kept the error and not-found states from ever rendering;
 * every grievance screen reads its resources through this (the pattern `kpi-dashboard` uses).
 */
export function safeValue<T>(resource: { hasValue(): boolean; value(): T }): T | undefined {
  return resource.hasValue() ? resource.value() : undefined;
}
